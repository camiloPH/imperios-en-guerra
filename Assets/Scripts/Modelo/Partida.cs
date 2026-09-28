using System;

namespace Modelo
{
    public enum EstadoPartida { Preparando, EnCurso, Finalizada }

    /// <summary>
    /// Estado global del juego: los dos jugadores (humano vs. IA), la
    /// dificultad y la cola de eventos hacia el Controlador. No conoce Unity.
    /// </summary>
    public class Partida
    {
        public Jugador JugadorHumano { get; }
        public Jugador JugadorIA { get; }
        public ColaEventos Eventos { get; }
        public Dificultad Dificultad { get; }
        public bool UbicacionManual { get; }

        private readonly object _candadoEstado = new object();
        private volatile EstadoPartida _estado = EstadoPartida.Preparando;
        private volatile string _ganadorId;
        private DateTime _inicio;
        private DateTime _fin;

        public Partida(Jugador jugadorHumano, Jugador jugadorIA, ColaEventos eventos,
                       Dificultad dificultad = Dificultad.Normal, bool ubicacionManual = false)
        {
            JugadorHumano = jugadorHumano ?? throw new ArgumentNullException(nameof(jugadorHumano));
            JugadorIA = jugadorIA ?? throw new ArgumentNullException(nameof(jugadorIA));
            Eventos = eventos ?? new ColaEventos();
            Dificultad = dificultad;
            UbicacionManual = ubicacionManual;
        }

        public EstadoPartida Estado => _estado;
        public bool EnCurso => _estado == EstadoPartida.EnCurso;
        public string GanadorId => _ganadorId;
        public Jugador Ganador => _ganadorId == null ? null : ObtenerJugador(_ganadorId);

        public TimeSpan Duracion
        {
            get
            {
                lock (_candadoEstado)
                {
                    if (_estado == EstadoPartida.Preparando) return TimeSpan.Zero;
                    return (_estado == EstadoPartida.Finalizada ? _fin : DateTime.Now) - _inicio;
                }
            }
        }

        public Jugador ObtenerJugador(string id) =>
            id == JugadorHumano.Id ? JugadorHumano : id == JugadorIA.Id ? JugadorIA : null;

        public Jugador ObtenerOponente(Jugador jugador) =>
            jugador.Id == JugadorHumano.Id ? JugadorIA : JugadorHumano;

        /// <summary>Pasa de Preparando a EnCurso. Ambos jugadores deben tener su Centro Urbano.</summary>
        public ResultadoAccion Comenzar()
        {
            lock (_candadoEstado)
            {
                if (_estado != EstadoPartida.Preparando)
                    return ResultadoAccion.Fallo("La partida ya había comenzado.");
                if (!JugadorHumano.TieneCentroUrbanoVivo() || !JugadorIA.TieneCentroUrbanoVivo())
                    return ResultadoAccion.Fallo("Ambos jugadores necesitan un Centro Urbano para comenzar.");
                _inicio = DateTime.Now;
                _estado = EstadoPartida.EnCurso;
            }
            Notificar(TipoEvento.PartidaIniciada, JugadorHumano,
                $"Partida iniciada contra la IA ({ReglasJuego.Nombre(Dificultad)}).");
            return ResultadoAccion.Ok("Partida iniciada.");
        }

        /// <summary>
        /// Se llama tras cada destrucción. Es idempotente y thread-safe: puede
        /// invocarse desde cualquier hilo del Modelo a la vez sin problema.
        /// </summary>
        public void VerificarGanador()
        {
            lock (_candadoEstado)
            {
                if (_estado != EstadoPartida.EnCurso) return;

                bool humanoDerrotado = JugadorHumano.FueDerrotado();
                bool iaDerrotada = JugadorIA.FueDerrotado();
                if (!humanoDerrotado && !iaDerrotada) return;

                // Empate imposible en la práctica; si ocurre, gana quien atacó último (el humano no pierde por empate).
                var ganador = iaDerrotada ? JugadorHumano : JugadorIA;
                Finalizar(ganador);
            }
        }

        public void Rendirse(Jugador jugador)
        {
            lock (_candadoEstado)
            {
                if (_estado != EstadoPartida.EnCurso) return;
                Notificar(TipoEvento.MensajeSistema, jugador, $"{jugador.Nombre} se rindió.");
                Finalizar(ObtenerOponente(jugador));
            }
        }

        private void Finalizar(Jugador ganador)
        {
            _ganadorId = ganador.Id;
            _fin = DateTime.Now;
            _estado = EstadoPartida.Finalizada;
            Notificar(TipoEvento.FinDePartida, ganador, $"Ganador: {ganador.Nombre}");
        }

        internal void Notificar(TipoEvento tipo, Jugador jugador, string descripcion,
                                Posicion? posicion = null, bool exito = true, int valor = 0, string idEntidad = null)
        {
            Eventos.Encolar(new EventoJuego(tipo, jugador?.Id, jugador?.Nombre ?? "Sistema", descripcion,
                posicion, exito, valor, idEntidad));
        }
    }

    /// <summary>Resultado de validar/ejecutar una acción: éxito o motivo del rechazo.</summary>
    public readonly struct ResultadoAccion
    {
        public readonly bool Exito;
        public readonly string Mensaje;

        private ResultadoAccion(bool exito, string mensaje)
        {
            Exito = exito;
            Mensaje = mensaje;
        }

        public static ResultadoAccion Ok(string mensaje) => new ResultadoAccion(true, mensaje);
        public static ResultadoAccion Fallo(string mensaje) => new ResultadoAccion(false, mensaje);
        public override string ToString() => (Exito ? "OK: " : "RECHAZADA: ") + Mensaje;
    }
}
