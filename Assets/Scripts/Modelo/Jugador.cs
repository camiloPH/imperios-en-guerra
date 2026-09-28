using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Modelo
{
    /// <summary>Estadisticas del jugador (Interlocked: se suman desde varios hilos).</summary>
    public class EstadisticasJugador
    {
        private int _recolectado, _disparosAcertados, _disparosFallados;
        private int _unidadesEnemigasDestruidas, _edificiosEnemigosDestruidos, _unidadesEntrenadas, _edificiosConstruidos;

        public int RecursosRecolectados => Volatile.Read(ref _recolectado);
        public int DisparosAcertados => Volatile.Read(ref _disparosAcertados);
        public int DisparosFallados => Volatile.Read(ref _disparosFallados);
        public int UnidadesEnemigasDestruidas => Volatile.Read(ref _unidadesEnemigasDestruidas);
        public int EdificiosEnemigosDestruidos => Volatile.Read(ref _edificiosEnemigosDestruidos);
        public int UnidadesEntrenadas => Volatile.Read(ref _unidadesEntrenadas);
        public int EdificiosConstruidos => Volatile.Read(ref _edificiosConstruidos);

        internal void SumarRecolectado(int cantidad) => Interlocked.Add(ref _recolectado, cantidad);
        internal void SumarDisparo(bool acierto)
        {
            if (acierto) Interlocked.Increment(ref _disparosAcertados);
            else Interlocked.Increment(ref _disparosFallados);
        }
        internal void SumarUnidadEnemigaDestruida() => Interlocked.Increment(ref _unidadesEnemigasDestruidas);
        internal void SumarEdificioEnemigoDestruido() => Interlocked.Increment(ref _edificiosEnemigosDestruidos);
        internal void SumarUnidadEntrenada() => Interlocked.Increment(ref _unidadesEntrenadas);
        internal void SumarEdificioConstruido() => Interlocked.Increment(ref _edificiosConstruidos);
    }

    /// <summary>Jugador humano o IA con su propio mapa. Listas concurrentes porque varios hilos las modifican.</summary>
    public class Jugador
    {
        public string Id { get; }
        public string Nombre { get; }
        public bool EsIA { get; }
        public Mapa Mapa { get; }
        public Recursos Recursos { get; }
        public EstadisticasJugador Estadisticas { get; } = new EstadisticasJugador();

        private readonly ConcurrentDictionary<string, Unidad> _unidades = new ConcurrentDictionary<string, Unidad>();
        private readonly ConcurrentDictionary<string, Edificio> _edificios = new ConcurrentDictionary<string, Edificio>();

        /// <summary>Casillas del mapa ENEMIGO que este jugador puede ver, y hasta cuando.</summary>
        private readonly ConcurrentDictionary<Posicion, DateTime> _reveladas = new ConcurrentDictionary<Posicion, DateTime>();

        private readonly object _candadoPoblacion = new object();
        private int _reservasPoblacion;
        private int _contadorIds;

        /// <summary>Grecia o Troya.</summary>
        public Civilizacion Civilizacion { get; }

        /// <summary>Multiplicador de recarga de sus tropas (IA en Facil = mas lenta).</summary>
        public double ModificadorRecarga { get; internal set; } = 1.0;

        public Jugador(string id, string nombre, bool esIA, int filasMapa = ReglasJuego.FilasMapa, int columnasMapa = ReglasJuego.ColumnasMapa,
                       Civilizacion civilizacion = Civilizacion.Grecia)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("El jugador necesita un id.");
            Id = id;
            Nombre = string.IsNullOrWhiteSpace(nombre) ? id : nombre.Trim();
            EsIA = esIA;
            Civilizacion = civilizacion;
            Mapa = new Mapa(filasMapa, columnasMapa);
            Recursos = new Recursos();
        }

        // ------------------------------------------------------------ colecciones
        public IReadOnlyList<Unidad> Unidades => _unidades.Values.ToList();
        public IReadOnlyList<Edificio> Edificios => _edificios.Values.ToList();

        public Unidad BuscarUnidad(string id) => id != null && _unidades.TryGetValue(id, out var u) ? u : null;
        public Edificio BuscarEdificio(string id) => id != null && _edificios.TryGetValue(id, out var e) ? e : null;

        internal void AgregarUnidad(Unidad unidad) => _unidades[unidad.Id] = unidad;
        internal void AgregarEdificio(Edificio edificio) => _edificios[edificio.Id] = edificio;
        internal void QuitarUnidad(string id) => _unidades.TryRemove(id, out _);
        internal void QuitarEdificio(string id) => _edificios.TryRemove(id, out _);

        internal string NuevoId(string prefijo) => $"{Id}_{prefijo}{Interlocked.Increment(ref _contadorIds)}";

        public Edificio CentroUrbano => _edificios.Values.FirstOrDefault(e => e.Tipo == TipoEdificio.CentroUrbano && e.EstaVivo());

        public int ContarUnidades(TipoUnidad tipo) => _unidades.Values.Count(u => u.Tipo == tipo && u.EstaVivo());
        public int ContarMilitares() => _unidades.Values.Count(u => u.EsMilitar() && u.EstaVivo());

        // ------------------------------------------------------------ poblacion
        public int Poblacion
        {
            get { lock (_candadoPoblacion) return _unidades.Count + _reservasPoblacion; }
        }

        public int PoblacionMaxima =>
            Math.Min(ReglasJuego.PoblacionTope,
                _edificios.Values.Where(e => e.EstaOperativo()).Sum(e => ReglasJuego.PoblacionQueAporta(e.Tipo)));

        /// <summary>Reserva un cupo de poblacion para una unidad que empieza a entrenarse.</summary>
        internal bool IntentarReservarPoblacion()
        {
            lock (_candadoPoblacion)
            {
                if (_unidades.Count + _reservasPoblacion >= PoblacionMaxima) return false;
                _reservasPoblacion++;
                return true;
            }
        }

        internal void LiberarReservaPoblacion()
        {
            lock (_candadoPoblacion)
            {
                if (_reservasPoblacion > 0) _reservasPoblacion--;
            }
        }

        // ------------------------------------------------------------ visibilidad
        internal void Revelar(Posicion posicionEnemiga) =>
            _reveladas[posicionEnemiga] = DateTime.UtcNow.AddMilliseconds(ReglasJuego.DuracionRevelacionMs);

        /// <summary>Este jugador ve ahora mismo esa casilla del mapa enemigo?</summary>
        public bool TieneRevelada(Posicion posicionEnemiga) =>
            _reveladas.TryGetValue(posicionEnemiga, out var hasta) && hasta > DateTime.UtcNow;

        // ------------------------------------------------------------ derrota
        public bool TieneCentroUrbanoVivo() => CentroUrbano != null;

        public bool TieneUnidadesMilitaresVivas() => _unidades.Values.Any(u => u.EsMilitar() && u.EstaVivo());

        /// <summary>Regla de derrota: sin Centro Urbano Y sin unidades militares.</summary>
        public bool FueDerrotado() => !TieneCentroUrbanoVivo() && !TieneUnidadesMilitaresVivas();
    }
}
