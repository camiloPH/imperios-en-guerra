using Modelo;

namespace Controlador
{
    /// <summary>
    /// Contrato Vista -> Controlador: todo lo que el usuario puede hacer
    /// desde la interfaz (clics sobre los mapas y botones). La Vista solo
    /// informa QUÉ pasó; el Controlador decide y valida.
    /// </summary>
    public interface IEntradaJugador
    {
        void IniciarPartida(Dificultad dificultad, bool ubicacionManual, Civilizacion civilizacion);
        void ClicMapaPropio(Posicion posicion, bool clicDerecho);
        void ClicMapaEnemigo(Posicion posicion, bool clicDerecho);
        void ActivarModoConstruccion(TipoEdificio tipo);
        void EntrenarUnidad(TipoUnidad tipo);
        void SeleccionarMilitares();
        void SeleccionarAldeanosInactivos();
        void CancelarSeleccion();
        void Rendirse();
        void VolverAlMenu();
        void Salir();
    }
}
