using UnityEngine;
using Controlador;

namespace Vista
{
    /// <summary>Al dar Play crea (si falta) el JuegoManager con el Controlador y la Vista grafica.</summary>
    public static class ArranqueVista
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Arrancar()
        {
            var controlador = Object.FindAnyObjectByType<JuegoControlador>();
            if (controlador == null)
            {
                var go = new GameObject("JuegoManager");
                controlador = go.AddComponent<JuegoControlador>();
            }
            if (controlador.GetComponent<VistaJuegoUI>() == null)
                controlador.gameObject.AddComponent<VistaJuegoUI>();
        }
    }
}
