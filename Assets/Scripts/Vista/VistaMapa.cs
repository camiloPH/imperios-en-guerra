using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Modelo;

namespace Vista
{
    /// <summary>
    /// Dibuja la matriz de un jugador (casillas + unidades + efectos) y
    /// traduce los clics sobre las casillas en coordenadas (fila, columna).
    /// </summary>
    public class VistaMapa : MonoBehaviour
    {
        public Action<Posicion, bool> AlHacerClic;
        public Action<Posicion?> AlCambiarHover;

        public bool EsHumano { get; private set; }
        public float TamCelda { get; private set; }

        private CeldaVista[,] _celdas;
        private RectTransform _capaUnidades, _capaEfectos;
        private readonly Dictionary<string, VistaUnidad> _unidades = new Dictionary<string, VistaUnidad>();
        private readonly HashSet<string> _vistos = new HashSet<string>();
        private CeldaVista _celdaHover;
        private Color _colorHover = new Color(1, 1, 1, 0.18f);

        public Civilizacion Civilizacion { get; private set; }

        public void Construir(int filas, int columnas, float tamCelda, bool esHumano, Civilizacion civ, int semilla)
        {
            EsHumano = esHumano;
            Civilizacion = civ;
            TamCelda = tamCelda;
            _celdas = new CeldaVista[filas, columnas];

            var capaCeldas = FabricaUI.CrearRect("Casillas", transform);
            FabricaUI.Estirar(capaCeldas);
            var rng = new System.Random(semilla);
            for (int f = 0; f < filas; f++)
            {
                for (int c = 0; c < columnas; c++)
                {
                    var rt = FabricaUI.CrearRect($"Casilla_{f}_{c}", capaCeldas);
                    FabricaUI.Ubicar(rt, c * tamCelda, f * tamCelda, tamCelda, tamCelda);
                    var celda = rt.gameObject.AddComponent<CeldaVista>();
                    celda.Construir(this, new Posicion(f, c), tamCelda, CatalogoSprites.Pasto(rng.Next(3)));
                    _celdas[f, c] = celda;
                }
            }

            _capaUnidades = FabricaUI.CrearRect("Unidades", transform);
            FabricaUI.Estirar(_capaUnidades);
            _capaEfectos = FabricaUI.CrearRect("Efectos", transform);
            FabricaUI.Estirar(_capaEfectos);
        }

        public Vector2 CentroDe(Posicion p) =>
            new Vector2(p.Columna * TamCelda + TamCelda / 2f, -(p.Fila * TamCelda + TamCelda / 2f));

        /// <summary>Vuelve a dibujar a partir de una instantánea del Modelo.</summary>
        public void Refrescar(InfoCasilla[,] info, IReadOnlyList<Unidad> unidades, Func<Posicion, bool> esVisible,
                              ICollection<string> unidadesSeleccionadas, string edificioSeleccionado)
        {
            int filas = _celdas.GetLength(0), columnas = _celdas.GetLength(1);
            for (int f = 0; f < filas; f++)
            {
                for (int c = 0; c < columnas; c++)
                {
                    var i = info[f, c];
                    bool visible = esVisible(i.Posicion);
                    _celdas[f, c].Aplicar(i, Civilizacion, visible,
                        edificioSeleccionado != null && i.IdEdificio == edificioSeleccionado);
                }
            }

            _vistos.Clear();
            foreach (var u in unidades)
            {
                if (!u.EstaVivo()) continue;
                _vistos.Add(u.Id);
                if (!_unidades.TryGetValue(u.Id, out var vista))
                {
                    var rt = FabricaUI.CrearRect("Unidad_" + u.Id, _capaUnidades);
                    vista = rt.gameObject.AddComponent<VistaUnidad>();
                    vista.Configurar(u.Tipo, u.Civilizacion, TamCelda);
                    _unidades[u.Id] = vista;
                }
                var pos = u.Posicion;
                vista.Actualizar(CentroDe(pos), (float)u.VidaActual / u.VidaMaxima,
                    unidadesSeleccionadas != null && unidadesSeleccionadas.Contains(u.Id),
                    u.EstaRecargando, esVisible(pos));
            }

            // Unidades que ya no existen (murieron): se quita su dibujo.
            List<string> sobrantes = null;
            foreach (var id in _unidades.Keys)
                if (!_vistos.Contains(id)) (sobrantes ??= new List<string>()).Add(id);
            if (sobrantes != null)
            {
                foreach (var id in sobrantes)
                {
                    Destroy(_unidades[id].gameObject);
                    _unidades.Remove(id);
                }
            }
        }

        // ------------------------------------------------------------ puntero

        internal void NotificarClic(Posicion pos, bool derecho) => AlHacerClic?.Invoke(pos, derecho);

        internal void NotificarHover(CeldaVista celda, bool entra)
        {
            if (entra)
            {
                _celdaHover?.MostrarHover(false, _colorHover);
                _celdaHover = celda;
                celda.MostrarHover(true, _colorHover);
                AlCambiarHover?.Invoke(celda.Posicion);
            }
            else if (_celdaHover == celda)
            {
                celda.MostrarHover(false, _colorHover);
                _celdaHover = null;
                AlCambiarHover?.Invoke(null);
            }
        }

        /// <summary>Color del resaltado bajo el cursor (p. ej. verde/rojo al elegir dónde construir).</summary>
        public void ColorHover(Color color)
        {
            if (_colorHover == color) return;
            _colorHover = color;
            _celdaHover?.MostrarHover(true, color);
        }

        // ------------------------------------------------------------ efectos

        public void Efecto(string sprite, Posicion pos, float duracion, float escalaInicial, float escalaFinal, float tamRelativo = 1.3f)
        {
            var img = FabricaUI.CrearImagen("Efecto", _capaEfectos, CatalogoSprites.Obtener(sprite), Color.white);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * TamCelda * tamRelativo;
            rt.anchoredPosition = CentroDe(pos);
            StartCoroutine(AnimarEfecto(img, duracion, escalaInicial, escalaFinal));
        }

        private static IEnumerator AnimarEfecto(Image img, float duracion, float escalaInicial, float escalaFinal)
        {
            float t = 0;
            while (t < duracion && img != null)
            {
                t += Time.deltaTime;
                float k = t / duracion;
                img.rectTransform.localScale = Vector3.one * Mathf.Lerp(escalaInicial, escalaFinal, Mathf.Sqrt(k));
                img.rectTransform.localRotation = Quaternion.Euler(0, 0, k * 25f);
                img.color = new Color(1, 1, 1, 1f - k * k);
                yield return null;
            }
            if (img != null) Destroy(img.gameObject);
        }

        public void TextoFlotante(Posicion pos, string texto, Color color, int tamaño = 20)
        {
            var t = FabricaUI.CrearTexto("TextoFlotante", _capaEfectos, texto, tamaño, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(TamCelda * 3, TamCelda);
            rt.anchoredPosition = CentroDe(pos) + new Vector2(0, TamCelda * 0.3f);
            StartCoroutine(AnimarTexto(t));
        }

        private IEnumerator AnimarTexto(Text t)
        {
            float duracion = 1.1f, tiempo = 0;
            var inicio = t.rectTransform.anchoredPosition;
            var color = t.color;
            while (tiempo < duracion && t != null)
            {
                tiempo += Time.deltaTime;
                float k = tiempo / duracion;
                t.rectTransform.anchoredPosition = inicio + new Vector2(0, TamCelda * 0.9f * k);
                t.color = new Color(color.r, color.g, color.b, 1f - k * k);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        /// <summary>Sacude el mapa (se usa cuando el enemigo acierta un disparo).</summary>
        public void Sacudir(float intensidad = 6f, float duracion = 0.25f)
        {
            if (_sacudiendo) return; // no acumular sacudidas (desplazarían el mapa)
            StartCoroutine(AnimarSacudida(intensidad, duracion));
        }

        private bool _sacudiendo;

        private IEnumerator AnimarSacudida(float intensidad, float duracion)
        {
            _sacudiendo = true;
            var rt = (RectTransform)transform;
            var original = rt.anchoredPosition;
            float t = 0;
            while (t < duracion)
            {
                t += Time.deltaTime;
                rt.anchoredPosition = original + UnityEngine.Random.insideUnitCircle * intensidad * (1f - t / duracion);
                yield return null;
            }
            rt.anchoredPosition = original;
            _sacudiendo = false;
        }
    }
}
