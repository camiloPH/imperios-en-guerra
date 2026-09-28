using UnityEngine;
using UnityEngine.UI;
using Modelo;

namespace Vista
{
    /// <summary>
    /// Sprite de una unidad sobre el mapa. El Modelo mueve la unidad casilla
    /// a casilla (en su hilo); aquí solo se interpola suavemente la posición
    /// dibujada hacia la casilla actual, y se muestran vida, selección y recarga.
    /// </summary>
    public class VistaUnidad : MonoBehaviour
    {
        private RectTransform _rt;
        private Image _sprite, _anillo, _barraFondo, _barraRelleno;
        private Vector2 _destino;
        private float _tam;
        private bool _colocada;
        private float _fase;

        public void Configurar(TipoUnidad tipo, Civilizacion civ, float tam)
        {
            _tam = tam;
            _rt = (RectTransform)transform;
            _rt.anchorMin = _rt.anchorMax = new Vector2(0, 1);
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = new Vector2(tam, tam);

            _anillo = FabricaUI.CrearImagen("Anillo", transform, CatalogoSprites.Obtener("seleccion"), Color.white);
            FabricaUI.Ubicar(_anillo.rectTransform, 0, tam * 0.1f, tam, tam * 0.9f);
            _anillo.enabled = false;

            _sprite = FabricaUI.CrearImagen("Sprite", transform, CatalogoSprites.DeUnidad(tipo, civ), Color.white);
            _sprite.preserveAspect = true;
            FabricaUI.Ubicar(_sprite.rectTransform, tam * 0.06f, tam * 0.02f, tam * 0.88f, tam * 0.88f);

            _barraFondo = FabricaUI.CrearImagen("BarraFondo", transform, CatalogoSprites.Obtener("blanco"), new Color(0, 0, 0, 0.7f));
            FabricaUI.Ubicar(_barraFondo.rectTransform, tam * 0.15f, 0, tam * 0.7f, tam * 0.08f);
            _barraRelleno = FabricaUI.CrearImagen("Relleno", _barraFondo.transform, CatalogoSprites.Obtener("blanco"), FabricaUI.Verde);
            FabricaUI.Ubicar(_barraRelleno.rectTransform, 1, 1, tam * 0.7f - 2, tam * 0.08f - 2);
            _barraRelleno.type = Image.Type.Filled;
            _barraRelleno.fillMethod = Image.FillMethod.Horizontal;
            _barraFondo.gameObject.SetActive(false);

            _fase = Random.value * 10f;
        }

        public void Actualizar(Vector2 destino, float vida01, bool seleccionada, bool recargando, bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            _destino = destino;
            if (!_colocada || !visible)
            {
                _rt.anchoredPosition = destino; // primera vez (o reaparece): sin interpolar
                _colocada = true;
            }

            _anillo.enabled = seleccionada;
            bool mostrarBarra = seleccionada || vida01 < 0.999f;
            if (_barraFondo.gameObject.activeSelf != mostrarBarra) _barraFondo.gameObject.SetActive(mostrarBarra);
            _barraRelleno.fillAmount = vida01;
            _barraRelleno.color = Color.Lerp(FabricaUI.Rojo, FabricaUI.Verde, vida01);
            _sprite.color = recargando ? new Color(0.78f, 0.78f, 0.78f, 1f) : Color.white;
        }

        private void Update()
        {
            var actual = _rt.anchoredPosition;
            bool moviendose = (actual - _destino).sqrMagnitude > 0.5f;
            _rt.anchoredPosition = Vector2.MoveTowards(actual, _destino, _tam * 3.2f * Time.deltaTime);

            // Pequeño "rebote" al caminar para que se note el movimiento.
            _fase += Time.deltaTime * (moviendose ? 14f : 2f);
            float rebote = moviendose ? Mathf.Abs(Mathf.Sin(_fase)) * _tam * 0.06f : Mathf.Sin(_fase) * _tam * 0.01f;
            _sprite.rectTransform.anchoredPosition = new Vector2(_tam * 0.06f, -_tam * 0.02f + rebote);
        }
    }
}
