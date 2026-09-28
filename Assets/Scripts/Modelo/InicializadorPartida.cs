using System;
using System.Collections.Generic;

namespace Modelo
{
    /// <summary>Arma la partida: jugadores, mapas, recursos al azar, Centros Urbanos y aldeanos.</summary>
    public static class InicializadorPartida
    {
        public const string IdHumano = "jugador1";
        public const string IdIA = "jugadorIA";

        /// <param name="civHumano">Bando que elige el jugador; la IA juega con el opuesto.</param>
        public static Partida CrearPartida(string nombreHumano, Dificultad dificultad, bool ubicacionManual,
                                           Civilizacion civHumano = Civilizacion.Grecia, int? semilla = null)
        {
            var rng = semilla.HasValue ? new Random(semilla.Value) : new Random();
            var civIA = ReglasJuego.Opuesta(civHumano);
            string nombre = string.IsNullOrWhiteSpace(nombreHumano) ? "Jugador 1" : nombreHumano.Trim();
            var humano = new Jugador(IdHumano, $"{nombre} ({ReglasJuego.Nombre(civHumano)})", esIA: false, civilizacion: civHumano);
            var ia = new Jugador(IdIA, $"{ReglasJuego.Nombre(civIA)} (IA {ReglasJuego.Nombre(dificultad)})", esIA: true, civilizacion: civIA)
            {
                ModificadorRecarga = ReglasJuego.ModificadorRecargaIA(dificultad)
            };
            var partida = new Partida(humano, ia, new ColaEventos(), dificultad, ubicacionManual);

            // IA: siempre aleatoria.
            var centroIA = PosicionAleatoriaCentro(rng, ia.Mapa);
            UbicarCentroYAldeanos(ia, centroIA);
            GenerarRecursos(ia, rng, centroIA);

            if (ubicacionManual)
            {
                // Primero los recursos; el jugador elegira una casilla libre para su Centro Urbano.
                GenerarRecursos(humano, rng, null);
            }
            else
            {
                var centroHumano = PosicionAleatoriaCentro(rng, humano.Mapa);
                UbicarCentroYAldeanos(humano, centroHumano);
                GenerarRecursos(humano, rng, centroHumano);
            }
            return partida;
        }

        /// <summary>Ubicacion manual (clic del usuario) del Centro Urbano, con validacion.</summary>
        public static ResultadoAccion UbicarCentroUrbanoManual(Partida partida, Posicion pos)
        {
            var humano = partida.JugadorHumano;
            if (partida.Estado != EstadoPartida.Preparando)
                return ResultadoAccion.Fallo("La partida ya comenzó.");
            if (humano.TieneCentroUrbanoVivo())
                return ResultadoAccion.Fallo("Ya ubicaste tu Centro Urbano.");
            if (!humano.Mapa.EstaDentroDelMapa(pos))
                return ResultadoAccion.Fallo($"{pos} está fuera del mapa.");
            if (!humano.Mapa.EstaLibre(pos))
                return ResultadoAccion.Fallo($"La casilla {pos} está ocupada por un recurso.");
            if (humano.Mapa.CasillasLibresCercanas(pos, ReglasJuego.AldeanosIniciales, 1, incluirOrigen: false).Count < ReglasJuego.AldeanosIniciales)
                return ResultadoAccion.Fallo("Necesitas al menos 3 casillas libres alrededor para tus aldeanos.");

            UbicarCentroYAldeanos(humano, pos);
            return ResultadoAccion.Ok($"Centro Urbano ubicado en {pos}.");
        }

        private static Posicion PosicionAleatoriaCentro(Random rng, Mapa mapa) =>
            new Posicion(rng.Next(2, mapa.Filas - 2), rng.Next(2, mapa.Columnas - 2));

        private static void UbicarCentroYAldeanos(Jugador jugador, Posicion posCentro)
        {
            var centro = new Edificio(jugador.NuevoId("ed"), TipoEdificio.CentroUrbano, posCentro, jugador.Id,
                yaConstruido: true, civ: jugador.Civilizacion);
            if (!jugador.Mapa.ColocarEdificio(centro))
                throw new InvalidOperationException($"No se pudo ubicar el Centro Urbano en {posCentro}.");
            jugador.AgregarEdificio(centro);

            foreach (var pos in jugador.Mapa.CasillasLibresCercanas(posCentro, ReglasJuego.AldeanosIniciales, 2, incluirOrigen: false))
            {
                var aldeano = new Unidad(jugador.NuevoId("aldeano"), TipoUnidad.Aldeano, pos, jugador.Id, jugador.Civilizacion);
                if (jugador.Mapa.ColocarUnidad(aldeano, pos))
                    jugador.AgregarUnidad(aldeano);
            }
        }

        /// <summary>Reparte recursos al azar; solo se colocan en casillas libres.</summary>
        private static void GenerarRecursos(Jugador jugador, Random rng, Posicion? centro)
        {
            var grupos = new List<(TipoRecurso tipo, int casillas, int cantidad)>
            {
                (TipoRecurso.Madera, 4, 150), (TipoRecurso.Madera, 3, 150), (TipoRecurso.Madera, 3, 150),
                (TipoRecurso.Oro, 2, 200), (TipoRecurso.Oro, 2, 200),
                (TipoRecurso.Comida, 3, 120), (TipoRecurso.Comida, 2, 120),
            };

            foreach (var grupo in grupos)
            {
                for (int intento = 0; intento < 40; intento++)
                {
                    var semilla = new Posicion(rng.Next(0, jugador.Mapa.Filas), rng.Next(0, jugador.Mapa.Columnas));
                    if (centro.HasValue && semilla.DistanciaEnPasos(centro.Value) < 3) continue;
                    if (!jugador.Mapa.EstaLibre(semilla)) continue;

                    jugador.Mapa.ColocarRecursoNatural(semilla, grupo.tipo, grupo.cantidad);
                    int colocadas = 1;
                    foreach (var vecina in Barajar(new List<Posicion>(semilla.Vecinas()), rng))
                    {
                        if (colocadas >= grupo.casillas) break;
                        if (centro.HasValue && vecina.DistanciaEnPasos(centro.Value) < 2) continue;
                        if (jugador.Mapa.ColocarRecursoNatural(vecina, grupo.tipo, grupo.cantidad)) colocadas++;
                    }
                    break;
                }
            }
        }

        private static List<T> Barajar<T>(List<T> lista, Random rng)
        {
            for (int i = lista.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (lista[i], lista[j]) = (lista[j], lista[i]);
            }
            return lista;
        }
    }
}
