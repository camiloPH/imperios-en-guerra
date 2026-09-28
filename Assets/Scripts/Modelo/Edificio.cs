using System;
using System.Threading;

namespace Modelo
{
    public enum TipoEdificio { CentroUrbano, Cuartel, Casa }
    public enum EstadoConstruccion { EnConstruccion, Completado, Destruido }

    /// <summary>Edificio. Vida con lock y entrenamiento con Interlocked (varios hilos lo modifican).</summary>
    public class Edificio
    {
        private readonly object _candado = new object();
        private int _vida;
        private int _entrenando;  // 0 libre, 1 entrenando
        private volatile EstadoConstruccion _estado;
        private volatile int _progresoConstruccion;
        private volatile int _progresoEntrenamiento;
        private volatile string _unidadEnEntrenamiento;

        public string Id { get; }
        public TipoEdificio Tipo { get; }
        public Posicion Posicion { get; }
        public string DueñoId { get; }
        public int VidaMaxima { get; }
        public int TiempoConstruccionMs { get; }

        public Civilizacion Civilizacion { get; }

        public Edificio(string id, TipoEdificio tipo, Posicion posicion, string dueñoId, bool yaConstruido = false,
                        Civilizacion civ = Civilizacion.Grecia)
        {
            Id = id;
            Tipo = tipo;
            Posicion = posicion;
            DueñoId = dueñoId;
            Civilizacion = civ;
            VidaMaxima = ReglasJuego.VidaEdificio(tipo, civ);
            _vida = VidaMaxima;
            TiempoConstruccionMs = ReglasJuego.TiempoConstruccionMs(tipo);
            _estado = yaConstruido ? EstadoConstruccion.Completado : EstadoConstruccion.EnConstruccion;
            _progresoConstruccion = yaConstruido ? 100 : 0;
        }

        public int VidaActual
        {
            get { lock (_candado) return _vida; }
        }

        public EstadoConstruccion Estado
        {
            get => _estado;
            internal set => _estado = value;
        }

        /// <summary>0..100</summary>
        public int ProgresoConstruccion
        {
            get => _progresoConstruccion;
            internal set => _progresoConstruccion = Math.Max(0, Math.Min(100, value));
        }

        public bool EstaEntrenando => Volatile.Read(ref _entrenando) == 1;
        public string UnidadEnEntrenamiento => _unidadEnEntrenamiento;
        public int ProgresoEntrenamiento
        {
            get => _progresoEntrenamiento;
            internal set => _progresoEntrenamiento = Math.Max(0, Math.Min(100, value));
        }

        public bool EstaVivo() => VidaActual > 0 && Estado != EstadoConstruccion.Destruido;
        public bool EstaOperativo() => EstaVivo() && Estado == EstadoConstruccion.Completado;

        /// <summary>Devuelve true solo para el golpe que lo destruye.</summary>
        internal bool RecibirDaño(int cantidad)
        {
            lock (_candado)
            {
                if (_vida <= 0) return false;
                _vida = Math.Max(0, _vida - cantidad);
                if (_vida > 0) return false;
                _estado = EstadoConstruccion.Destruido;
                return true;
            }
        }

        /// <summary>Un edificio entrena una unidad a la vez (compare-and-swap).</summary>
        internal bool IntentarIniciarEntrenamiento(TipoUnidad tipo)
        {
            if (Interlocked.CompareExchange(ref _entrenando, 1, 0) != 0) return false;
            _unidadEnEntrenamiento = ReglasJuego.Nombre(tipo, Civilizacion);
            _progresoEntrenamiento = 0;
            return true;
        }

        internal void TerminarEntrenamiento()
        {
            _unidadEnEntrenamiento = null;
            _progresoEntrenamiento = 0;
            Volatile.Write(ref _entrenando, 0);
        }
    }
}
