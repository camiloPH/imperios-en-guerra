using Modelo;

namespace Controlador
{
    public enum TipoMensaje { Info, Exito, Advertencia, Error, Combate, Enemigo }

    /// <summary>Controlador -> Vista: lo que se puede mostrar. La Vista lee el Modelo pero nunca lo modifica.</summary>
    public interface IVistaJuego
    {
        /// <summary>Se llama una vez: guarda a quien avisar los clics.</summary>
        void Conectar(IEntradaJugador entrada);

        void MostrarMenuInicio();
        void MostrarPartida(Partida partida);
        void MostrarColocacionInicial(bool activa);

        /// <summary>Vuelve a leer el Modelo y actualiza mapas, recursos, seleccion, etc.</summary>
        void Refrescar(Partida partida, Seleccion seleccion, int hilosActivos);

        void MostrarMensaje(string texto, TipoMensaje tipo);
        void MostrarAtaque(bool sobreMapaHumano, Posicion posicion, bool impacto, int daño);
        void MostrarDestruccion(bool sobreMapaHumano, Posicion posicion);
        void MostrarRecoleccion(Posicion posicion, string texto);
        void MostrarFinDePartida(bool ganoHumano, string nombreGanador, string resumen, string carpetaArchivos);
    }
}
