using System;
using System.Threading;

namespace Modelo
{
    public enum TipoUnidad { Aldeano, Infante, Arquero }
    public enum EstadoUnidad { Inactiva, Moviendose, Recolectando }

    /// <summary>
    /// POCO: una unidad (aldeano o militar). Varios hilos la tocan a la vez
    /// (su hilo de movimiento/recolección, hilos de ataque enemigos que le
    /// quitan vida, el hilo principal que la dibuja), por eso la vida y la
    /// posición se protegen con lock y la recarga con Interlocked.
    /// </summary>
    public class Unidad
    {
        private readonly object _candado = new object();
        private Posicion _posicion;
        private int _vida;
        private int _recargando; // 0 = lista, 1 = recargando (Interlocked)
        private volatile EstadoUnidad _estado = EstadoUnidad.Inactiva;
        private volatile string _descripcionEstado = "Inactiva";

        public string Id { get; }
        public TipoUnidad Tipo { get; }
        public string DueñoId { get; }
        public int VidaMaxima { get; }
        public int Daño { get; }
        public int DañoEdificios { get; }
        public double VelocidadCasillasPorSeg { get; }
        public int RecargaMs { get; }

        public Civilizacion Civilizacion { get; }

        /// <summary>Nombre según el bando: "Hoplita", "Lancero troyano", etc.</summary>
        public string Nombre => ReglasJuego.Nombre(Tipo, Civilizacion);

        public Unidad(string id, TipoUnidad tipo, Posicion posicion, string dueñoId, Civilizacion civ = Civilizacion.Grecia)
        {
            var stats = ReglasJuego.Estadisticas(tipo, civ);
            Id = id;
            Tipo = tipo;
            Civilizacion = civ;
            _posicion = posicion;
            DueñoId = dueñoId;
            VidaMaxima = stats.Vida;
            _vida = stats.Vida;
            Daño = stats.Daño;
            DañoEdificios = stats.DañoEdificios;
            VelocidadCasillasPorSeg = stats.VelocidadCasillasPorSeg;
            RecargaMs = stats.RecargaMs;
        }

        public Posicion Posicion
        {
            get { lock (_candado) return _posicion; }
            internal set { lock (_candado) _posicion = value; }
        }

        public int VidaActual
        {
            get { lock (_candado) return _vida; }
        }

        public EstadoUnidad Estado
        {
            get => _estado;
            internal set => _estado = value;
        }

        /// <summary>Texto legible de lo que hace la unidad ("Recolectando madera"...).</summary>
        public string DescripcionEstado
        {
            get => _descripcionEstado;
            internal set => _descripcionEstado = value;
        }

        public bool EstaVivo() => VidaActual > 0;
        public bool EsMilitar() => Tipo != TipoUnidad.Aldeano;
        public bool EstaRecargando => Volatile.Read(ref _recargando) == 1;
        public int MsPorCasilla => (int)(1000 / Math.Max(0.1, VelocidadCasillasPorSeg));

        /// <summary>
        /// Resta vida de forma atómica. Devuelve true solo para el golpe que
        /// la deja en cero: si dos ataques llegan a la vez, únicamente uno de
        /// los hilos "la mata" y registra la baja.
        /// </summary>
        internal bool RecibirDaño(int cantidad)
        {
            lock (_candado)
            {
                if (_vida <= 0) return false;
                _vida = Math.Max(0, _vida - cantidad);
                return _vida == 0;
            }
        }

        /// <summary>Marca la unidad como recargando si estaba lista (compare-and-swap).</summary>
        internal bool IntentarIniciarRecarga() => Interlocked.CompareExchange(ref _recargando, 1, 0) == 0;

        internal void TerminarRecarga() => Volatile.Write(ref _recargando, 0);
    }
}
