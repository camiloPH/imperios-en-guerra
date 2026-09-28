using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Modelo
{
    /// <summary>
    /// Oponente controlado por la computadora. Corre en su propio hilo
    /// dedicado ("Hilo-IA", System.Threading.Thread) y cada cierto intervalo
    /// toma decisiones usando EXACTAMENTE la misma API que el jugador humano
    /// (GestorConcurrencia): así la IA no puede saltarse ninguna regla.
    ///
    /// La IA tampoco hace trampa con la información: del mapa humano solo
    /// "ve" los edificios (visibles para ambos) y las casillas que reveló
    /// con sus disparos, igual que el humano sobre el mapa de la IA.
    ///
    /// Finalización: Detener() cancela el token (que también despierta la
    /// espera entre decisiones) y hace Join() del hilo.
    /// </summary>
    public class LogicaIA
    {
        private readonly Partida _partida;
        private readonly GestorConcurrencia _gestor;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Random _rng = new Random();
        private Thread _hilo;
        private bool _modoAtaque;

        public LogicaIA(Partida partida, GestorConcurrencia gestor)
        {
            _partida = partida ?? throw new ArgumentNullException(nameof(partida));
            _gestor = gestor ?? throw new ArgumentNullException(nameof(gestor));
        }

        private Jugador IA => _partida.JugadorIA;
        private Jugador Humano => _partida.JugadorHumano;

        public int IntervaloDecisionMs
        {
            get
            {
                switch (_partida.Dificultad)
                {
                    case Dificultad.Facil: return 3200;
                    case Dificultad.Dificil: return 1200;
                    default: return 2000;
                }
            }
        }

        /// <summary>Tope de aldeanos: en Fácil la IA tiene una economía más pequeña.</summary>
        private int MaxAldeanos
        {
            get
            {
                switch (_partida.Dificultad)
                {
                    case Dificultad.Facil: return 4;
                    case Dificultad.Dificil: return 7;
                    default: return 6;
                }
            }
        }

        /// <summary>Probabilidad de apuntar a un objetivo conocido en vez de disparar "a ciegas".</summary>
        private double Puntería
        {
            get
            {
                switch (_partida.Dificultad)
                {
                    case Dificultad.Facil: return 0.25;
                    case Dificultad.Dificil: return 0.85;
                    default: return 0.55;
                }
            }
        }

        private int UmbralAtaque
        {
            get
            {
                switch (_partida.Dificultad)
                {
                    case Dificultad.Facil: return 5;
                    case Dificultad.Dificil: return 2;
                    default: return 4;
                }
            }
        }

        public void Iniciar()
        {
            if (_hilo != null) return;
            _hilo = new Thread(Bucle) { Name = "Hilo-IA", IsBackground = true };
            _hilo.Start();
        }

        public void Detener()
        {
            _cts.Cancel();
            if (_hilo != null && _hilo.IsAlive && Thread.CurrentThread != _hilo)
                _hilo.Join(1500);
        }

        private void Bucle()
        {
            while (!_cts.IsCancellationRequested)
            {
                // Esperar el intervalo; el WaitHandle se libera al instante si se cancela.
                if (_cts.Token.WaitHandle.WaitOne(IntervaloDecisionMs)) break;
                if (_partida.Estado == EstadoPartida.Finalizada) break;
                if (!_partida.EnCurso) continue;

                try
                {
                    TomarDecisiones();
                }
                catch (Exception ex)
                {
                    // Un error de la IA nunca debe tumbar el juego.
                    _partida.Notificar(TipoEvento.MensajeSistema, IA, $"La IA tuvo un error y lo ignoró: {ex.Message}", exito: false);
                }
            }
        }

        // ================================================================
        // Decisiones
        // ================================================================

        private void TomarDecisiones()
        {
            GestionarAldeanos();
            GestionarConstrucciones();
            GestionarEntrenamiento();
            GestionarAtaque();
        }

        private void GestionarAldeanos()
        {
            var inactivos = IA.Unidades.Where(u => u.Tipo == TipoUnidad.Aldeano && u.EstaVivo() && u.Estado == EstadoUnidad.Inactiva);
            foreach (var aldeano in inactivos)
            {
                var tipo = RecursoMasNecesario();
                var destino = IA.Mapa.BuscarRecursoMasCercano(aldeano.Posicion, tipo) ?? IA.Mapa.BuscarRecursoMasCercano(aldeano.Posicion);
                if (destino.HasValue) _gestor.Recolectar(IA, aldeano, destino.Value);
            }
        }

        private TipoRecurso RecursoMasNecesario()
        {
            var r = IA.Recursos.Snapshot();
            // Pesos: la comida y la madera se gastan más; el oro se necesita para militares.
            var necesidad = new Dictionary<TipoRecurso, double>
            {
                { TipoRecurso.Comida, r[TipoRecurso.Comida] / 1.2 },
                { TipoRecurso.Madera, r[TipoRecurso.Madera] / 1.3 },
                { TipoRecurso.Oro, r[TipoRecurso.Oro] / 1.0 },
            };
            return necesidad.OrderBy(p => p.Value).First().Key;
        }

        private void GestionarConstrucciones()
        {
            var edificios = IA.Edificios;
            bool construyendo = edificios.Any(e => e.Estado == EstadoConstruccion.EnConstruccion);
            if (construyendo) return;

            TipoEdificio? deseado = null;
            if (!IA.TieneCentroUrbanoVivo())
                deseado = TipoEdificio.CentroUrbano;
            else if (!edificios.Any(e => e.Tipo == TipoEdificio.Cuartel) && IA.ContarUnidades(TipoUnidad.Aldeano) >= 4)
                deseado = TipoEdificio.Cuartel;
            else if (IA.Poblacion >= IA.PoblacionMaxima - 1 && IA.PoblacionMaxima < ReglasJuego.PoblacionTope)
                deseado = TipoEdificio.Casa;

            if (!deseado.HasValue || !IA.Recursos.AlcanzaPara(ReglasJuego.CostoEdificio(deseado.Value))) return;

            var referencia = IA.CentroUrbano?.Posicion
                             ?? IA.Unidades.FirstOrDefault()?.Posicion
                             ?? new Posicion(IA.Mapa.Filas / 2, IA.Mapa.Columnas / 2);
            var lugar = LugarParaConstruir(referencia);
            if (lugar.HasValue) _gestor.Construir(IA, deseado.Value, lugar.Value);
        }

        /// <summary>Casilla libre a 2-4 pasos de la referencia (deja libres las vecinas para que salgan unidades).</summary>
        private Posicion? LugarParaConstruir(Posicion referencia)
        {
            var candidatas = IA.Mapa.CasillasLibresCercanas(referencia, 40, 4, incluirOrigen: false)
                .Where(p => p.DistanciaEnPasos(referencia) >= 2)
                .ToList();
            if (candidatas.Count == 0) return null;
            return candidatas[_rng.Next(Math.Min(6, candidatas.Count))];
        }

        private void GestionarEntrenamiento()
        {
            if (IA.ContarUnidades(TipoUnidad.Aldeano) < MaxAldeanos)
                _gestor.Entrenar(IA, TipoUnidad.Aldeano);

            if (IA.Edificios.Any(e => e.Tipo == TipoEdificio.Cuartel && e.EstaOperativo()))
            {
                var tipo = _rng.NextDouble() < 0.55 ? TipoUnidad.Infante : TipoUnidad.Arquero;
                if (!IA.Recursos.AlcanzaPara(ReglasJuego.CostoUnidad(tipo)))
                    tipo = tipo == TipoUnidad.Infante ? TipoUnidad.Arquero : TipoUnidad.Infante;
                _gestor.Entrenar(IA, tipo);
            }
        }

        private void GestionarAtaque()
        {
            var militares = IA.Unidades.Where(u => u.EsMilitar() && u.EstaVivo()).ToList();
            if (militares.Count == 0)
            {
                _modoAtaque = false;
                return;
            }
            if (militares.Count >= UmbralAtaque) _modoAtaque = true;
            if (!_modoAtaque) return;

            var listos = militares.Where(u => !u.EstaRecargando).ToList();
            if (listos.Count == 0) return;

            // Fuego concentrado: todos los listos disparan al mismo objetivo...
            var objetivo = ElegirObjetivo();
            foreach (var u in listos)
            {
                _gestor.Atacar(IA, u, objetivo);
                if (!_partida.EnCurso) return;
                // ...salvo que ese objetivo ya quedó vacío: entonces se elige otro.
                var tipoCasilla = Humano.Mapa.ObtenerInfo(objetivo).Tipo;
                if (tipoCasilla == TipoCasilla.Libre || tipoCasilla == TipoCasilla.RecursoNatural)
                    objetivo = ElegirObjetivo();
            }
        }

        /// <summary>
        /// Elige dónde disparar usando solo información "legal": edificios
        /// (visibles) y casillas reveladas por sus disparos recientes.
        /// </summary>
        private Posicion ElegirObjetivo()
        {
            var mapa = Humano.Mapa.Instantanea();
            var unidadesVistas = new List<InfoCasilla>();
            var edificios = new List<InfoCasilla>();
            var sospechosas = new List<Posicion>();

            foreach (var c in mapa)
            {
                if (c.TieneEdificio) edificios.Add(c);
                else if (c.TieneUnidad && IA.TieneRevelada(c.Posicion)) unidadesVistas.Add(c);
            }

            // Los aldeanos suelen estar junto a los recursos: son buenas casillas para "adivinar".
            foreach (var c in mapa)
            {
                if (c.Tipo != TipoCasilla.RecursoNatural) continue;
                foreach (var v in c.Posicion.Vecinas())
                {
                    if (!Humano.Mapa.EstaDentroDelMapa(v) || IA.TieneRevelada(v)) continue;
                    var info = mapa[v.Fila, v.Columna];
                    if (!info.TieneEdificio && info.Tipo != TipoCasilla.RecursoNatural) sospechosas.Add(v);
                }
            }

            if (unidadesVistas.Count > 0 && _rng.NextDouble() < Puntería)
            {
                var militares = unidadesVistas.Where(c => c.TipoUnidad != TipoUnidad.Aldeano).ToList();
                var lista = militares.Count > 0 ? militares : unidadesVistas;
                return lista[_rng.Next(lista.Count)].Posicion;
            }

            if (edificios.Count > 0 && _rng.NextDouble() < Puntería)
            {
                // Prioridad: Cuartel (corta su producción) > Centro Urbano > Casa; y el más dañado primero.
                var elegido = edificios
                    .OrderBy(c => c.TipoEdificio == TipoEdificio.Cuartel ? 0 : c.TipoEdificio == TipoEdificio.CentroUrbano ? 1 : 2)
                    .ThenBy(c => c.VidaEdificio)
                    .First();
                return elegido.Posicion;
            }

            if (sospechosas.Count > 0 && _rng.NextDouble() < 0.7)
                return sospechosas[_rng.Next(sospechosas.Count)];

            return new Posicion(_rng.Next(Humano.Mapa.Filas), _rng.Next(Humano.Mapa.Columnas));
        }
    }
}
