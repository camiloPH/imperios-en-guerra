using System;
using System.Collections.Generic;
using System.Text;

namespace Modelo
{
    /// <summary>
    /// Matriz (15x15 por defecto) del mapa de un jugador. TODA lectura o
    /// escritura de la matriz pasa por lock(_candado), porque a la vez
    /// pueden estar corriendo: hilos de recolección, construcción,
    /// entrenamiento, movimiento, ataques del enemigo, la IA y el hilo
    /// principal de Unity tomando una instantánea para dibujar.
    /// </summary>
    public class Mapa
    {
        public int Filas { get; }
        public int Columnas { get; }
        private readonly Casilla[,] _casillas;
        private readonly object _candado = new object();

        public Mapa(int filas = ReglasJuego.FilasMapa, int columnas = ReglasJuego.ColumnasMapa)
        {
            if (filas <= 0 || columnas <= 0) throw new ArgumentException("El mapa debe tener tamaño positivo.");
            Filas = filas;
            Columnas = columnas;
            _casillas = new Casilla[filas, columnas];
            for (int f = 0; f < filas; f++)
                for (int c = 0; c < columnas; c++)
                    _casillas[f, c] = new Casilla();
        }

        public bool EstaDentroDelMapa(Posicion pos) =>
            pos.Fila >= 0 && pos.Fila < Filas && pos.Columna >= 0 && pos.Columna < Columnas;

        // ------------------------------------------------------------ lectura

        public InfoCasilla ObtenerInfo(Posicion pos)
        {
            if (!EstaDentroDelMapa(pos)) throw new ArgumentOutOfRangeException(nameof(pos), $"{pos} está fuera del mapa.");
            lock (_candado)
            {
                return new InfoCasilla(pos, _casillas[pos.Fila, pos.Columna], DateTime.UtcNow);
            }
        }

        /// <summary>Copia consistente de todo el mapa (lo que usa la Vista para dibujar).</summary>
        public InfoCasilla[,] Instantanea()
        {
            var ahora = DateTime.UtcNow;
            var copia = new InfoCasilla[Filas, Columnas];
            lock (_candado)
            {
                for (int f = 0; f < Filas; f++)
                    for (int c = 0; c < Columnas; c++)
                        copia[f, c] = new InfoCasilla(new Posicion(f, c), _casillas[f, c], ahora);
            }
            return copia;
        }

        public bool EstaLibre(Posicion pos)
        {
            if (!EstaDentroDelMapa(pos)) return false;
            lock (_candado) return _casillas[pos.Fila, pos.Columna].EstaLibre();
        }

        public TipoRecurso? RecursoEn(Posicion pos)
        {
            if (!EstaDentroDelMapa(pos)) return null;
            lock (_candado)
            {
                var c = _casillas[pos.Fila, pos.Columna];
                return c.Tipo == TipoCasilla.RecursoNatural && c.CantidadRecursoNatural > 0 ? c.RecursoNatural : null;
            }
        }

        internal (Unidad unidad, Edificio edificio) ObtenerOcupante(Posicion pos)
        {
            if (!EstaDentroDelMapa(pos)) return (null, null);
            lock (_candado)
            {
                var c = _casillas[pos.Fila, pos.Columna];
                return (c.Unidad, c.Edificio);
            }
        }

        // ------------------------------------------------------------ escritura

        internal bool ColocarRecursoNatural(Posicion pos, TipoRecurso tipo, int cantidad)
        {
            if (!EstaDentroDelMapa(pos) || cantidad <= 0) return false;
            lock (_candado)
            {
                var casilla = _casillas[pos.Fila, pos.Columna];
                if (!casilla.EstaLibre()) return false;
                casilla.Tipo = TipoCasilla.RecursoNatural;
                casilla.RecursoNatural = tipo;
                casilla.CantidadRecursoNatural = cantidad;
                return true;
            }
        }

        internal bool ColocarEdificio(Edificio edificio)
        {
            if (!EstaDentroDelMapa(edificio.Posicion)) return false;
            lock (_candado)
            {
                var casilla = _casillas[edificio.Posicion.Fila, edificio.Posicion.Columna];
                if (!casilla.EstaLibre()) return false;
                casilla.Tipo = TipoCasilla.Edificio;
                casilla.Edificio = edificio;
                return true;
            }
        }

        internal bool ColocarUnidad(Unidad unidad, Posicion pos)
        {
            if (!EstaDentroDelMapa(pos)) return false;
            lock (_candado)
            {
                var casilla = _casillas[pos.Fila, pos.Columna];
                if (!casilla.EstaLibre()) return false;
                casilla.Tipo = TipoCasilla.Unidad;
                casilla.Unidad = unidad;
                unidad.Posicion = pos;
                return true;
            }
        }

        /// <summary>
        /// Mueve una unidad a una casilla libre. Comprueba, dentro del mismo
        /// lock, que la unidad siga viva y siga en su casilla de origen (un
        /// ataque pudo matarla un instante antes).
        /// </summary>
        internal bool MoverUnidad(Unidad unidad, Posicion destino)
        {
            if (!EstaDentroDelMapa(destino)) return false;
            lock (_candado)
            {
                var origen = unidad.Posicion;
                var casillaOrigen = _casillas[origen.Fila, origen.Columna];
                if (casillaOrigen.Unidad != unidad || !unidad.EstaVivo()) return false;

                var casillaDestino = _casillas[destino.Fila, destino.Columna];
                if (!casillaDestino.EstaLibre()) return false;

                casillaOrigen.Vaciar();
                casillaDestino.Tipo = TipoCasilla.Unidad;
                casillaDestino.Unidad = unidad;
                unidad.Posicion = destino;
                return true;
            }
        }

        internal void QuitarUnidad(Unidad unidad)
        {
            lock (_candado)
            {
                var pos = unidad.Posicion;
                var casilla = _casillas[pos.Fila, pos.Columna];
                if (casilla.Unidad == unidad) casilla.Vaciar();
            }
        }

        internal void QuitarEdificio(Edificio edificio)
        {
            lock (_candado)
            {
                var pos = edificio.Posicion;
                var casilla = _casillas[pos.Fila, pos.Columna];
                if (casilla.Edificio == edificio) casilla.Vaciar();
            }
        }

        /// <summary>
        /// Extrae hasta 'cantidad' del recurso natural de la casilla de forma
        /// atómica (dos aldeanos sobre el mismo árbol nunca sacan de más). Si
        /// el recurso se agota, la casilla queda libre.
        /// </summary>
        internal int ExtraerRecurso(Posicion pos, int cantidad, out TipoRecurso tipo)
        {
            tipo = TipoRecurso.Madera;
            if (!EstaDentroDelMapa(pos)) return 0;
            lock (_candado)
            {
                var c = _casillas[pos.Fila, pos.Columna];
                if (c.Tipo != TipoCasilla.RecursoNatural || !c.RecursoNatural.HasValue || c.CantidadRecursoNatural <= 0)
                    return 0;

                tipo = c.RecursoNatural.Value;
                int extraido = Math.Min(cantidad, c.CantidadRecursoNatural);
                c.CantidadRecursoNatural -= extraido;
                if (c.CantidadRecursoNatural <= 0) c.Vaciar();
                return extraido;
            }
        }

        internal void MarcarDisparo(Posicion pos, bool impacto)
        {
            if (!EstaDentroDelMapa(pos)) return;
            lock (_candado)
            {
                var c = _casillas[pos.Fila, pos.Columna];
                c.Marca = impacto ? MarcaCasilla.Impacto : MarcaCasilla.Fallo;
                c.MomentoMarca = DateTime.UtcNow;
            }
        }

        // ------------------------------------------------------------ búsquedas

        /// <summary>Casilla libre más cercana a 'origen' (anillos concéntricos), sin contar el origen.</summary>
        public Posicion? BuscarCasillaLibreAdyacente(Posicion origen, int radioMax = 4)
        {
            var lista = CasillasLibresCercanas(origen, 1, radioMax, incluirOrigen: false);
            return lista.Count > 0 ? lista[0] : (Posicion?)null;
        }

        /// <summary>Hasta 'cantidad' casillas libres ordenadas por cercanía a 'origen'.</summary>
        public List<Posicion> CasillasLibresCercanas(Posicion origen, int cantidad, int radioMax = 4, bool incluirOrigen = true)
        {
            var resultado = new List<Posicion>();
            lock (_candado)
            {
                if (incluirOrigen && EstaDentroDelMapa(origen) && _casillas[origen.Fila, origen.Columna].EstaLibre())
                    resultado.Add(origen);

                for (int radio = 1; radio <= radioMax && resultado.Count < cantidad; radio++)
                {
                    for (int df = -radio; df <= radio && resultado.Count < cantidad; df++)
                    {
                        for (int dc = -radio; dc <= radio && resultado.Count < cantidad; dc++)
                        {
                            if (Math.Max(Math.Abs(df), Math.Abs(dc)) != radio) continue; // solo el borde del anillo
                            var candidata = new Posicion(origen.Fila + df, origen.Columna + dc);
                            if (EstaDentroDelMapa(candidata) && _casillas[candidata.Fila, candidata.Columna].EstaLibre())
                                resultado.Add(candidata);
                        }
                    }
                }
            }
            return resultado;
        }

        public Posicion? BuscarRecursoMasCercano(Posicion origen, TipoRecurso? tipo = null)
        {
            Posicion? mejor = null;
            double mejorDistancia = double.MaxValue;
            lock (_candado)
            {
                for (int f = 0; f < Filas; f++)
                {
                    for (int c = 0; c < Columnas; c++)
                    {
                        var casilla = _casillas[f, c];
                        if (casilla.Tipo != TipoCasilla.RecursoNatural || casilla.CantidadRecursoNatural <= 0) continue;
                        if (tipo.HasValue && casilla.RecursoNatural != tipo) continue;
                        var pos = new Posicion(f, c);
                        double d = pos.DistanciaA(origen);
                        if (d < mejorDistancia)
                        {
                            mejorDistancia = d;
                            mejor = pos;
                        }
                    }
                }
            }
            return mejor;
        }

        /// <summary>Ruta (sin incluir el origen) hasta una casilla libre. null si no hay camino.</summary>
        public List<Posicion> BuscarRuta(Posicion origen, Posicion destino)
        {
            if (!EstaDentroDelMapa(destino)) return null;
            if (origen.Equals(destino)) return new List<Posicion>();
            return BuscarRutaBFS(origen, pos => pos.Equals(destino));
        }

        /// <summary>
        /// Ruta hasta cualquier casilla libre vecina de 'objetivo' (para
        /// recolectar un recurso o acercarse a un edificio). Lista vacía si
        /// ya está al lado; null si no hay camino.
        /// </summary>
        public List<Posicion> BuscarRutaHastaAdyacente(Posicion origen, Posicion objetivo)
        {
            if (origen.EsAdyacenteA(objetivo)) return new List<Posicion>();
            return BuscarRutaBFS(origen, pos => pos.EsAdyacenteA(objetivo));
        }

        /// <summary>Búsqueda en anchura sobre casillas libres (8 direcciones, sin cortar esquinas bloqueadas).</summary>
        private List<Posicion> BuscarRutaBFS(Posicion origen, Func<Posicion, bool> esMeta)
        {
            lock (_candado)
            {
                var previo = new Dictionary<Posicion, Posicion>();
                var cola = new Queue<Posicion>();
                cola.Enqueue(origen);
                previo[origen] = origen;

                while (cola.Count > 0)
                {
                    var actual = cola.Dequeue();
                    foreach (var vecina in actual.Vecinas())
                    {
                        if (!EstaDentroDelMapa(vecina) || previo.ContainsKey(vecina)) continue;
                        if (!_casillas[vecina.Fila, vecina.Columna].EstaLibre()) continue;

                        bool diagonal = vecina.Fila != actual.Fila && vecina.Columna != actual.Columna;
                        if (diagonal &&
                            !_casillas[actual.Fila, vecina.Columna].EstaLibre() &&
                            !_casillas[vecina.Fila, actual.Columna].EstaLibre())
                            continue; // no pasar "entre" dos obstáculos en diagonal

                        previo[vecina] = actual;
                        if (esMeta(vecina)) return Reconstruir(previo, origen, vecina);
                        cola.Enqueue(vecina);
                    }
                }
                return null;
            }
        }

        private static List<Posicion> Reconstruir(Dictionary<Posicion, Posicion> previo, Posicion origen, Posicion meta)
        {
            var ruta = new List<Posicion>();
            var paso = meta;
            while (!paso.Equals(origen))
            {
                ruta.Add(paso);
                paso = previo[paso];
            }
            ruta.Reverse();
            return ruta;
        }

        // ------------------------------------------------------------ archivo

        /// <summary>Representación en texto de la matriz, usada en configuracion.txt y resultado_final.txt.</summary>
        public string ComoTexto()
        {
            var sb = new StringBuilder();
            sb.Append("     ");
            for (int c = 0; c < Columnas; c++) sb.Append(c.ToString().PadLeft(3));
            sb.AppendLine();
            lock (_candado)
            {
                for (int f = 0; f < Filas; f++)
                {
                    sb.Append(f.ToString().PadLeft(4)).Append(' ');
                    for (int c = 0; c < Columnas; c++)
                        sb.Append(SimboloDe(_casillas[f, c]).ToString().PadLeft(3));
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        public const string LeyendaTexto =
            ". libre | M madera | O oro | C comida | U centro urbano | Q cuartel | H casa | " +
            "a aldeano | i infante | r arquero | minúscula/mayúscula = en construcción/terminado";

        private static char SimboloDe(Casilla c)
        {
            switch (c.Tipo)
            {
                case TipoCasilla.RecursoNatural:
                    return c.RecursoNatural == TipoRecurso.Oro ? 'O' : c.RecursoNatural == TipoRecurso.Madera ? 'M' : 'C';
                case TipoCasilla.Edificio:
                    char s = c.Edificio.Tipo == TipoEdificio.CentroUrbano ? 'U' : c.Edificio.Tipo == TipoEdificio.Cuartel ? 'Q' : 'H';
                    return c.Edificio.Estado == EstadoConstruccion.Completado ? s : char.ToLower(s);
                case TipoCasilla.Unidad:
                    return c.Unidad.Tipo == TipoUnidad.Aldeano ? 'a' : c.Unidad.Tipo == TipoUnidad.Infante ? 'i' : 'r';
                default:
                    return '.';
            }
        }
    }
}
