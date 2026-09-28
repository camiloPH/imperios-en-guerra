using System;

namespace Modelo
{
    public enum TipoEvento
    {
        PartidaIniciada,
        RecursoRecolectado,
        OrdenRecoleccion,
        OrdenMovimiento,
        ConstruccionIniciada,
        ConstruccionCompletada,
        EntrenamientoIniciado,
        EntrenamientoCompletado,
        Ataque,
        UnidadDestruida,
        EdificioDestruido,
        MensajeSistema,
        FinDePartida
    }

    /// <summary>Nota que un hilo encola cuando algo cambia. Sin nada de Unity.</summary>
    public class EventoJuego
    {
        public TipoEvento Tipo { get; }
        public string IdJugador { get; }
        public string NombreJugador { get; }
        public string Descripcion { get; }
        public Posicion? Posicion { get; }
        public bool Exito { get; }
        public int Valor { get; }
        public string IdEntidad { get; }
        public DateTime Momento { get; }

        public EventoJuego(TipoEvento tipo, string idJugador, string nombreJugador, string descripcion,
                           Posicion? posicion = null, bool exito = true, int valor = 0, string idEntidad = null)
        {
            Tipo = tipo;
            IdJugador = idJugador;
            NombreJugador = nombreJugador;
            Descripcion = descripcion;
            Posicion = posicion;
            Exito = exito;
            Valor = valor;
            IdEntidad = idEntidad;
            Momento = DateTime.Now;
        }

        /// <summary>Nombre de la accion tal como aparece en log_partida.txt.</summary>
        public string AccionLegible
        {
            get
            {
                switch (Tipo)
                {
                    case TipoEvento.PartidaIniciada: return "Inicio de partida";
                    case TipoEvento.RecursoRecolectado: return "Recolección";
                    case TipoEvento.OrdenRecoleccion: return "Recolectar";
                    case TipoEvento.OrdenMovimiento: return "Mover unidad";
                    case TipoEvento.ConstruccionIniciada: return "Construir";
                    case TipoEvento.ConstruccionCompletada: return "Construcción terminada";
                    case TipoEvento.EntrenamientoIniciado: return "Entrenar unidad";
                    case TipoEvento.EntrenamientoCompletado: return "Unidad entrenada";
                    case TipoEvento.Ataque: return "Ataque";
                    case TipoEvento.UnidadDestruida: return "Unidad perdida";
                    case TipoEvento.EdificioDestruido: return "Edificio perdido";
                    case TipoEvento.FinDePartida: return "Fin de partida";
                    default: return "Mensaje";
                }
            }
        }

        /// <summary>La recoleccion (muy frecuente) no se escribe en el log.</summary>
        public bool EsRelevanteParaLog => Tipo != TipoEvento.RecursoRecolectado;

        public override string ToString() => $"[{Momento:HH:mm:ss}] {NombreJugador} - {AccionLegible}: {Descripcion}";
    }
}
