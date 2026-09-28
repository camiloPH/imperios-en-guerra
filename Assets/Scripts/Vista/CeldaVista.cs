using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Modelo;

namespace Vista
{
    /// <summary>Dibuja una casilla y recibe sus clics. Solo lee InfoCasilla.</summary>
    public class CeldaVista : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color ColorNiebla = new Color(0.58f, 0.6f, 0.7f, 1f);

        public Posicion Posicion { get; private set; }

        private VistaMapa _mapa;
        private Image _fondo, _anillo, _contenido, _andamio, _marca, _barraFondo, _barraRelleno, _hover;
        private int _ultimaFirma = int.MinValue;

        public void Construir(VistaMapa mapa, Posicion posicion, float tam, Sprite pasto)
        {
            _mapa = mapa;
            Posicion = posicion;

            _fondo = gameObject.AddComponent<Image>();
            _fondo.sprite = pasto;
            _fondo.raycastTarget = true;  // la casilla es la que recibe el clic

            _anillo = Hijo("Anillo", CatalogoSprites.Obtener("seleccion"), 0, tam * 0.1f, tam, tam * 0.9f);
            _contenido = Hijo("Contenido", null, tam * 0.04f, tam * 0.02f, tam * 0.92f, tam * 0.92f);
            _contenido.preserveAspect = true;
            _andamio = Hijo("Andamio", CatalogoSprites.Obtener("andamio"), tam * 0.08f, tam * 0.1f, tam * 0.84f, tam * 0.84f);
            _marca = Hijo("Marca", null, tam * 0.1f, tam * 0.1f, tam * 0.8f, tam * 0.8f);
            _barraFondo = Hijo("BarraFondo", CatalogoSprites.Obtener("blanco"), tam * 0.1f, tam * 0.86f, tam * 0.8f, tam * 0.09f);
            _barraFondo.color = new Color(0, 0, 0, 0.7f);
            _barraRelleno = Hijo("BarraRelleno", CatalogoSprites.Obtener("blanco"), 1, 1, tam * 0.8f - 2, tam * 0.09f - 2, _barraFondo.transform);
            _barraRelleno.type = Image.Type.Filled;
            _barraRelleno.fillMethod = Image.FillMethod.Horizontal;
            _hover = Hijo("Hover", CatalogoSprites.Obtener("blanco"), 0, 0, tam, tam);
            _hover.color = new Color(1, 1, 1, 0.18f);

            _anillo.enabled = _andamio.enabled = _marca.enabled = _hover.enabled = false;
            _contenido.enabled = false;
            _barraFondo.gameObject.SetActive(false);
        }

        private Image Hijo(string nombre, Sprite sprite, float x, float y, float ancho, float alto, Transform padre = null)
        {
            var img = FabricaUI.CrearImagen(nombre, padre ?? transform, sprite, Color.white);
            FabricaUI.Ubicar(img.rectTransform, x, y, ancho, alto);
            return img;
        }

        /// <summary>Actualiza el dibujo solo si algo cambio (firma distinta).</summary>
        public void Aplicar(in InfoCasilla info, Civilizacion civDueño, bool visible, bool edificioSeleccionado)
        {
            int marcaVisible = info.SegundosDesdeMarca < 12 ? (int)(info.SegundosDesdeMarca * 4) : -1;
            int firma = Firma(info, visible, edificioSeleccionado, marcaVisible);
            if (firma == _ultimaFirma) return;
            _ultimaFirma = firma;

            _fondo.color = visible ? Color.white : ColorNiebla;
            _anillo.enabled = edificioSeleccionado;
            _andamio.enabled = false;
            _barraFondo.gameObject.SetActive(false);

            switch (info.Tipo)
            {
                case TipoCasilla.RecursoNatural when info.Recurso.HasValue:
                    _contenido.enabled = true;
                    _contenido.sprite = CatalogoSprites.DeRecurso(info.Recurso.Value);
                    _contenido.color = visible ? Color.white : ColorNiebla;
                    break;

                case TipoCasilla.Edificio:
                    _contenido.enabled = true;
                    _contenido.sprite = CatalogoSprites.DeEdificio(info.TipoEdificio, civDueño);
                    bool enObra = info.EstadoEdificio == EstadoConstruccion.EnConstruccion;
                    _contenido.color = enObra ? new Color(1, 1, 1, 0.55f) : (visible ? Color.white : ColorNiebla);
                    _andamio.enabled = enObra;
                    if (enObra)
                    {
                        MostrarBarra(info.ProgresoConstruccion / 100f, FabricaUI.Dorado);
                    }
                    else if (info.VidaEdificio < info.VidaMaximaEdificio && info.VidaMaximaEdificio > 0)
                    {
                        float v = (float)info.VidaEdificio / info.VidaMaximaEdificio;
                        MostrarBarra(v, Color.Lerp(FabricaUI.Rojo, FabricaUI.Verde, v));
                    }
                    break;

                default:
                    _contenido.enabled = false;
                    break;
            }

            if (marcaVisible >= 0)
            {
                _marca.enabled = true;
                _marca.sprite = CatalogoSprites.Obtener(info.Marca == MarcaCasilla.Impacto ? "marca_impacto" : "marca_fallo");
                _marca.color = new Color(1, 1, 1, Mathf.Clamp01(1f - (float)info.SegundosDesdeMarca / 12f));
                _marca.transform.SetAsLastSibling();
                _hover.transform.SetAsLastSibling();
            }
            else
            {
                _marca.enabled = false;
            }
        }

        private void MostrarBarra(float valor, Color color)
        {
            _barraFondo.gameObject.SetActive(true);
            _barraRelleno.fillAmount = Mathf.Clamp01(valor);
            _barraRelleno.color = color;
        }

        private static int Firma(in InfoCasilla i, bool visible, bool sel, int marca)
        {
            unchecked
            {
                int h = (int)i.Tipo;
                h = h * 31 + (i.Recurso.HasValue ? (int)i.Recurso.Value + 1 : 0);
                h = h * 31 + (i.IdEdificio?.GetHashCode() ?? 0);
                h = h * 31 + (int)i.EstadoEdificio;
                h = h * 31 + i.ProgresoConstruccion;
                h = h * 31 + i.VidaEdificio;
                h = h * 31 + (int)i.Marca;
                h = h * 31 + marca;
                h = h * 31 + (visible ? 1 : 0);
                h = h * 31 + (sel ? 1 : 0);
                return h;
            }
        }

        public void MostrarHover(bool activo, Color color)
        {
            _hover.enabled = activo;
            _hover.color = color;
        }

        public void OnPointerClick(PointerEventData datos) =>
            _mapa.NotificarClic(Posicion, datos.button == PointerEventData.InputButton.Right);

        public void OnPointerEnter(PointerEventData datos) => _mapa.NotificarHover(this, true);
        public void OnPointerExit(PointerEventData datos) => _mapa.NotificarHover(this, false);
    }
}
