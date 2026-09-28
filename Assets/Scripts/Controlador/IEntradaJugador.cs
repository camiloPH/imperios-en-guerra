using Modelo;

namespace Controlador
{
    /// <summary>Vista -> Controlador: lo que el usuario puede hacer (clics y botones).</summary>
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
