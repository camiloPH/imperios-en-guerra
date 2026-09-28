using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vista
{
    /// <summary>
    /// Paleta de colores y "fábrica" de elementos de interfaz (UGUI) creados
    /// por código: paneles, textos, imágenes y botones con el mismo estilo.
    /// Cumple el papel de los prefabs, pero sin depender de assets de escena.
    /// </summary>
    public static class FabricaUI
    {
        public static readonly Color Fondo = new Color32(24, 19, 14, 255);
        public static readonly Color Pergamino = new Color32(243, 230, 200, 255);
        public static readonly Color PergaminoTenue = new Color32(200, 184, 150, 255);
        public static readonly Color Dorado = new Color32(232, 190, 96, 255);
        public static readonly Color Azul = new Color32(96, 156, 255, 255);
        public static readonly Color Rojo = new Color32(240, 96, 84, 255);
        public static readonly Color Verde = new Color32(120, 220, 120, 255);
        public static readonly Color Naranja = new Color32(255, 170, 70, 255);

        /// <summary>Color de cada bando: azul egeo para Grecia, carmesí para Troya.</summary>
        public static Color ColorDe(Modelo.Civilizacion civ) => civ == Modelo.Civilizacion.Grecia ? Azul : Rojo;

        private static Font _fuente;
        public static Font Fuente
        {
            get
            {
                if (_fuente == null) _fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _fuente;
            }
        }

        // ------------------------------------------------------------ layout

        public static RectTransform CrearRect(string nombre, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            return rt;
        }

        /// <summary>Posiciona en píxeles de referencia desde la esquina superior izquierda del padre.</summary>
        public static void Ubicar(RectTransform rt, float x, float y, float ancho, float alto)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(ancho, alto);
        }

        public static void Estirar(RectTransform rt, float margen = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(margen, margen);
            rt.offsetMax = new Vector2(-margen, -margen);
        }

        // ------------------------------------------------------------ elementos

        public static Image CrearImagen(string nombre, Transform padre, Sprite sprite, Color color, bool recibeClics = false)
        {
            var rt = CrearRect(nombre, padre);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = recibeClics;
            img.preserveAspect = false;
            return img;
        }

        public static Image CrearPanel(string nombre, Transform padre, float x, float y, float ancho, float alto, float opacidad = 0.96f)
        {
            var img = CrearImagen(nombre, padre, CatalogoSprites.Obtener("panel"), new Color(1, 1, 1, opacidad), true);
            img.type = Image.Type.Sliced;
            Ubicar(img.rectTransform, x, y, ancho, alto);
            return img;
        }

        public static Text CrearTexto(string nombre, Transform padre, string texto, int tamaño, Color color,
                                      TextAnchor alineacion = TextAnchor.MiddleLeft, FontStyle estilo = FontStyle.Normal, bool sombra = true)
        {
            var rt = CrearRect(nombre, padre);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Fuente;
            t.text = texto;
            t.fontSize = tamaño;
            t.color = color;
            t.alignment = alineacion;
            t.fontStyle = estilo;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.supportRichText = true;
            t.raycastTarget = false;
            if (sombra)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0, 0, 0, 0.75f);
                s.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return t;
        }

        public static Text CrearTexto(string nombre, Transform padre, string texto, int tamaño, Color color,
                                      float x, float y, float ancho, float alto,
                                      TextAnchor alineacion = TextAnchor.MiddleLeft, FontStyle estilo = FontStyle.Normal)
        {
            var t = CrearTexto(nombre, padre, texto, tamaño, color, alineacion, estilo);
            Ubicar(t.rectTransform, x, y, ancho, alto);
            return t;
        }

        public static BotonUI CrearBoton(string nombre, Transform padre, float x, float y, float ancho, float alto,
                                         string texto, Sprite icono, UnityAction alPulsar, int tamañoTexto = 17)
        {
            var fondo = CrearImagen(nombre, padre, CatalogoSprites.Obtener("boton"), Color.white, true);
            fondo.type = Image.Type.Sliced;
            Ubicar(fondo.rectTransform, x, y, ancho, alto);

            var boton = fondo.gameObject.AddComponent<Button>();
            boton.targetGraphic = fondo;
            var colores = boton.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = new Color(1.15f, 1.08f, 0.9f);
            colores.pressedColor = new Color(0.75f, 0.7f, 0.6f);
            colores.selectedColor = Color.white;
            colores.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
            colores.colorMultiplier = 1.2f;
            boton.colors = colores;
            if (alPulsar != null) boton.onClick.AddListener(alPulsar);

            Image imgIcono = null;
            float margenTexto = 10;
            if (icono != null)
            {
                imgIcono = CrearImagen("Icono", fondo.transform, icono, Color.white);
                imgIcono.preserveAspect = true;
                float lado = alto - 14;
                Ubicar(imgIcono.rectTransform, 8, 7, lado, lado);
                margenTexto = lado + 14;
            }

            var etiqueta = CrearTexto("Texto", fondo.transform, texto, tamañoTexto, Pergamino,
                icono != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, FontStyle.Bold);
            Ubicar(etiqueta.rectTransform, margenTexto, 2, ancho - margenTexto - 6, alto - 4);
            etiqueta.lineSpacing = 0.9f;

            var receptor = fondo.gameObject.AddComponent<ReceptorPuntero>();
            return new BotonUI(boton, fondo, etiqueta, imgIcono, receptor);
        }
    }

    /// <summary>Referencias a las partes de un botón creado por la fábrica.</summary>
    public class BotonUI
    {
        public Button Boton { get; }
        public Image Fondo { get; }
        public Text Texto { get; }
        public Image Icono { get; }
        public ReceptorPuntero Puntero { get; }

        public BotonUI(Button boton, Image fondo, Text texto, Image icono, ReceptorPuntero puntero)
        {
            Boton = boton;
            Fondo = fondo;
            Texto = texto;
            Icono = icono;
            Puntero = puntero;
        }

        public void Resaltar(bool activo) => Fondo.color = activo ? new Color(1f, 0.85f, 0.45f) : Color.white;
    }

    /// <summary>Componente mínimo que traduce eventos del puntero (clic, entrar, salir) a delegados C#.</summary>
    public class ReceptorPuntero : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<PointerEventData.InputButton> AlHacerClic;
        public Action AlEntrar;
        public Action AlSalir;

        public void OnPointerClick(PointerEventData datos) => AlHacerClic?.Invoke(datos.button);
        public void OnPointerEnter(PointerEventData datos) => AlEntrar?.Invoke();
        public void OnPointerExit(PointerEventData datos) => AlSalir?.Invoke();
    }
}
