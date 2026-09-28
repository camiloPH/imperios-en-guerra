using UnityEngine;
using Controlador;
using Modelo;

namespace Vista
{
    /// <summary>
    /// Segunda Vista, solo de depuración: escribe en la Console de Unity los
    /// mensajes y ataques. Demuestra que el Controlador puede avisar a varias
    /// Vistas a la vez sin cambiar nada (todas implementan IVistaJuego).
    /// Se puede quitar del GameObject sin afectar el juego.
    /// </summary>
    public class VistaConsolaTemporal : MonoBehaviour, IVistaJuego
    {
        [SerializeField] private bool registrarMensajes = true;

        public void Conectar(IEntradaJugador entrada) { }
        public void MostrarMenuInicio() => Debug.Log("[Consola] Menú de inicio.");
        public void MostrarPartida(Partida partida) => Debug.Log($"[Consola] Nueva partida vs {partida.JugadorIA.Nombre}.");
        public void MostrarColocacionInicial(bool activa) { }
        public void Refrescar(Partida partida, Seleccion seleccion, int hilosActivos) { }

        public void MostrarMensaje(string texto, TipoMensaje tipo)
        {
            if (registrarMensajes) Debug.Log($"[Consola] {tipo}: {texto}");
        }

        public void MostrarAtaque(bool sobreMapaHumano, Posicion posicion, bool impacto, int daño) =>
            Debug.Log($"[Consola] Disparo sobre el mapa {(sobreMapaHumano ? "humano" : "de la IA")} en {posicion}: {(impacto ? $"impacto (-{daño})" : "fallo")}");

        public void MostrarDestruccion(bool sobreMapaHumano, Posicion posicion) =>
            Debug.Log($"[Consola] Destrucción en el mapa {(sobreMapaHumano ? "humano" : "de la IA")} en {posicion}");

        public void MostrarRecoleccion(Posicion posicion, string texto) { }

        public void MostrarFinDePartida(bool ganoHumano, string nombreGanador, string resumen, string carpetaArchivos) =>
            Debug.Log($"[Consola] Fin de partida. Ganador: {nombreGanador}\n{resumen}\nArchivos en: {carpetaArchivos}");
    }
}
