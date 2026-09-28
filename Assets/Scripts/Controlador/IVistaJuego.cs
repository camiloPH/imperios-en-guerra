using Modelo;

namespace Controlador
{
    public enum TipoMensaje { Info, Exito, Advertencia, Error, Combate, Enemigo }

    /// <summary>
    /// Contrato Controlador -> Vista. La Vista (MonoBehaviour, sprites, UI)
    /// implementa esta interfaz; el Controlador nunca manipula componentes
    /// visuales directamente, solo llama a estos métodos.
    ///
    /// La Vista puede LEER el Modelo que recibe (Partida) para dibujarlo,
    /// pero nunca modificarlo: toda acción del usuario vuelve al Controlador
    /// a través de IEntradaJugador.
    /// </summary>
    public interface IVistaJuego
    {
        /// <summary>Se llama una sola vez: la Vista guarda a quién avisar cuando el usuario haga algo.</summary>
        void Conectar(IEntradaJugador entrada);

        void MostrarMenuInicio();
        void MostrarPartida(Partida partida);
        void MostrarColocacionInicial(bool activa);

        /// <summary>Vuelve a leer el Modelo y actualiza mapas, recursos, selección, etc.</summary>
        void Refrescar(Partida partida, Seleccion seleccion, int hilosActivos);

        void MostrarMensaje(string texto, TipoMensaje tipo);
        void MostrarAtaque(bool sobreMapaHumano, Posicion posicion, bool impacto, int daño);
        void MostrarDestruccion(bool sobreMapaHumano, Posicion posicion);
        void MostrarRecoleccion(Posicion posicion, string texto);
        void MostrarFinDePartida(bool ganoHumano, string nombreGanador, string resumen, string carpetaArchivos);
    }
}
