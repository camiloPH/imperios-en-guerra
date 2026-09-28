using UnityEngine;
using Controlador;

namespace Vista
{
    /// <summary>
    /// Garantiza que la escena tenga un JuegoControlador con la Vista gráfica
    /// al darle Play, aunque la escena esté vacía. Si ya existe un
    /// "JuegoManager" con JuegoControlador, solo le agrega VistaJuegoUI.
    /// </summary>
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
