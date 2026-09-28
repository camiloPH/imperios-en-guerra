using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Controlador;
using Modelo;

namespace Vista
{
    /// <summary>
    /// Vista grafica principal (UGUI), construida por codigo. Dibuja lo que pide el
    /// Controlador y lo que lee del Modelo; reenvia los clics. No modifica el Modelo.
    /// </summary>
    public class VistaJuegoUI : MonoBehaviour, IVistaJuego
    {
        private const float TamCelda = 46f;
        private const int MaxMensajes = 7;

        private IEntradaJugador _entrada;
        private Partida _partida;
        private bool _construida;

        private RectTransform _raiz;

        // Barra superior
        private Text _txtOro, _txtMadera, _txtComida, _txtPoblacion, _txtTiempo, _txtHilos, _txtSubtitulo;

        // Mapas
        private RectTransform _contPropio, _contEnemigo;
        private VistaMapa _mapaPropio, _mapaEnemigo;
        private Text _tituloPropio, _tituloEnemigo;
        private GameObject _bannerColocacion;
        private InfoCasilla[,] _ultimoPropio, _ultimoEnemigo;
        private Posicion? _hoverPropio, _hoverEnemigo;

        // Panel lateral
        private Image _retrato;
        private Text _txtSelNombre, _txtSelDetalle, _txtProduccion, _txtInfo;
        private readonly Dictionary<string, BotonUI> _botones = new Dictionary<string, BotonUI>();

        // Mensajes
        private Text _txtMensajes;
        private readonly List<string> _mensajes = new List<string>();

        // Menu y fin
        private GameObject _menu, _fin;
        private Image _imgFin;
        private Text _txtFinTitulo, _txtFinGanador, _txtFinResumen, _txtFinArchivos;
        private BotonUI _btnFacil, _btnNormal, _btnDificil, _btnAleatoria, _btnManual;
        private Dificultad _dificultad = Dificultad.Facil;
        private bool _manual;
        private Civilizacion _civilizacion = Civilizacion.Grecia;
        private BotonUI _btnGrecia, _btnTroya;
        private GameObject _ayuda;
        private Image _marcoPropio, _marcoEnemigo, _escudoBarra;

        // --- Ciclo de vida ---

        private void Start()
        {
            var controlador = GetComponent<JuegoControlador>();
            if (controlador == null) controlador = FindAnyObjectByType<JuegoControlador>();
            if (controlador != null) controlador.RegistrarVista(this);
            else Debug.LogError("[Vista] No hay un JuegoControlador en la escena.");
        }

        private void Update()
        {
            var teclado = Keyboard.current;
            if (teclado == null) return;
            if (teclado.f1Key.wasPressedThisFrame && _ayuda != null)
                MostrarAyuda(!_ayuda.activeSelf);
            if (teclado.escapeKey.wasPressedThisFrame)
            {
                if (_ayuda != null && _ayuda.activeSelf) MostrarAyuda(false);
                else if (_partida != null) _entrada?.CancelarSeleccion();
            }
        }

        // --- IVistaJuego ---

        public void Conectar(IEntradaJugador entrada)
        {
            _entrada = entrada;
            ConstruirInterfaz();
        }

        public void MostrarMenuInicio()
        {
            ConstruirInterfaz();
            _partida = null;
            _menu.SetActive(true);
            _fin.SetActive(false);
            ActualizarOpcionesMenu();
        }

        public void MostrarPartida(Partida partida)
        {
            ConstruirInterfaz();
            _partida = partida;
            _menu.SetActive(false);
            _fin.SetActive(false);
            _mensajes.Clear();
            _txtMensajes.text = "";
            _hoverPropio = _hoverEnemigo = null;

            var civH = partida.JugadorHumano.Civilizacion;
            var civIA = partida.JugadorIA.Civilizacion;
            _mapaPropio = CrearMapa(_contPropio, partida.JugadorHumano.Mapa, true, civH);
            _mapaEnemigo = CrearMapa(_contEnemigo, partida.JugadorIA.Mapa, false, civIA);
            AplicarColoresDeBando(civH, civIA);
            _mapaPropio.AlHacerClic = (p, derecho) => _entrada.ClicMapaPropio(p, derecho);
            _mapaEnemigo.AlHacerClic = (p, derecho) => _entrada.ClicMapaEnemigo(p, derecho);
            _mapaPropio.AlCambiarHover = p => _hoverPropio = p;
            _mapaEnemigo.AlCambiarHover = p => _hoverEnemigo = p;

            _tituloPropio.text = $"TU IMPERIO  —  {partida.JugadorHumano.Nombre}";
            _tituloEnemigo.text = $"IMPERIO ENEMIGO  —  {partida.JugadorIA.Nombre}";
            _txtSubtitulo.text = $"Jugador vs IA  ·  Dificultad {ReglasJuego.Nombre(partida.Dificultad)}";
        }

        public void MostrarColocacionInicial(bool activa)
        {
            if (_bannerColocacion != null) _bannerColocacion.SetActive(activa);
        }

        public void Refrescar(Partida partida, Seleccion seleccion, int hilosActivos)
        {
            if (!_construida || partida == null || _mapaPropio == null) return;
            _partida = partida;
            var humano = partida.JugadorHumano;
            var ia = partida.JugadorIA;

            // Barra superior
            var r = humano.Recursos.Snapshot();
            _txtOro.text = r[TipoRecurso.Oro].ToString();
            _txtMadera.text = r[TipoRecurso.Madera].ToString();
            _txtComida.text = r[TipoRecurso.Comida].ToString();
            _txtPoblacion.text = $"{humano.Poblacion}/{humano.PoblacionMaxima}";
            _txtTiempo.text = partida.Duracion.ToString(@"mm\:ss");
            _txtHilos.text = $"Hilos activos: {hilosActivos}\n<size=12>(tareas + IA + Log)</size>";

            // Mapas (instantaneas tomadas bajo el lock de cada Mapa)
            _ultimoPropio = humano.Mapa.Instantanea();
            _ultimoEnemigo = ia.Mapa.Instantanea();
            var seleccionadas = new HashSet<string>(seleccion.IdsUnidades);
            _mapaPropio.Refrescar(_ultimoPropio, humano.Unidades, _ => true, seleccionadas, seleccion.IdEdificio);
            _mapaEnemigo.Refrescar(_ultimoEnemigo, ia.Unidades, humano.TieneRevelada, null, null);

            // Color del cursor al elegir donde construir
            if (seleccion.Modo == ModoAccion.Construir && _hoverPropio.HasValue)
            {
                bool libre = _ultimoPropio[_hoverPropio.Value.Fila, _hoverPropio.Value.Columna].Tipo == TipoCasilla.Libre;
                _mapaPropio.ColorHover(libre ? new Color(0.3f, 1f, 0.3f, 0.35f) : new Color(1f, 0.25f, 0.2f, 0.4f));
            }
            else
            {
                _mapaPropio.ColorHover(new Color(1, 1, 1, 0.18f));
            }
            _mapaEnemigo.ColorHover(seleccion.TieneUnidades ? new Color(1f, 0.3f, 0.2f, 0.35f) : new Color(1, 1, 1, 0.18f));

            ActualizarSeleccion(humano, seleccion);
            ActualizarBotones(humano, seleccion);
            ActualizarProduccion(humano);
            ActualizarInfo(humano, seleccion);
        }

        public void MostrarMensaje(string texto, TipoMensaje tipo)
        {
            if (!_construida) return;
            string color;
            switch (tipo)
            {
                case TipoMensaje.Exito: color = "#7CE07C"; break;
                case TipoMensaje.Advertencia: color = "#FFB050"; break;
                case TipoMensaje.Error: color = "#FF6A5A"; break;
                case TipoMensaje.Combate: color = "#8FC0FF"; break;
                case TipoMensaje.Enemigo: color = "#FF7F6E"; break;
                default: color = "#F3E6C8"; break;
            }
            string hora = _partida != null ? _partida.Duracion.ToString(@"mm\:ss") : "--:--";
            _mensajes.Insert(0, $"<color=#B8A888>[{hora}]</color>  <color={color}>{Escapar(texto)}</color>");
            if (_mensajes.Count > MaxMensajes) _mensajes.RemoveAt(_mensajes.Count - 1);
            _txtMensajes.text = string.Join("\n", _mensajes);
        }

        public void MostrarAtaque(bool sobreMapaHumano, Posicion posicion, bool impacto, int daño)
        {
            var mapa = sobreMapaHumano ? _mapaPropio : _mapaEnemigo;
            if (mapa == null) return;
            if (impacto)
            {
                mapa.Efecto("explosion", posicion, 0.55f, 0.3f, 1.2f);
                mapa.TextoFlotante(posicion, $"-{daño}", FabricaUI.Rojo, 22);
                if (sobreMapaHumano) mapa.Sacudir();
            }
            else
            {
                mapa.Efecto("polvo", posicion, 0.7f, 0.4f, 1.1f);
                mapa.TextoFlotante(posicion, "¡Fallo!", Color.white, 17);
            }
        }

        public void MostrarDestruccion(bool sobreMapaHumano, Posicion posicion)
        {
            var mapa = sobreMapaHumano ? _mapaPropio : _mapaEnemigo;
            if (mapa == null) return;
            mapa.Efecto("explosion", posicion, 0.9f, 0.5f, 2.0f, 1.6f);
            mapa.TextoFlotante(posicion, "¡Destruido!", FabricaUI.Naranja, 20);
            if (sobreMapaHumano) mapa.Sacudir(10f, 0.4f);
        }

        public void MostrarRecoleccion(Posicion posicion, string texto)
        {
            _mapaPropio?.TextoFlotante(posicion, texto, FabricaUI.Dorado, 16);
        }

        public void MostrarFinDePartida(bool ganoHumano, string nombreGanador, string resumen, string carpetaArchivos)
        {
            _fin.SetActive(true);
            _fin.transform.SetAsLastSibling();
            _imgFin.sprite = CatalogoSprites.Obtener(ganoHumano ? "icono_corona" : "icono_calavera");
            _txtFinTitulo.text = ganoHumano ? "¡VICTORIA!" : "DERROTA";
            _txtFinTitulo.color = ganoHumano ? FabricaUI.Dorado : FabricaUI.Rojo;
            _txtFinGanador.text = $"Ganador: {nombreGanador}";
            _txtFinResumen.text = resumen;
            _txtFinArchivos.text = $"configuracion.txt, log_partida.txt y resultado_final.txt guardados en:\n{carpetaArchivos}";
        }

        // --- Construccion de la interfaz ---

        private void ConstruirInterfaz()
        {
            if (_construida) return;
            _construida = true;

            AsegurarEventSystem();

            var canvasGO = new GameObject("InterfazJuego", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var escalador = canvasGO.GetComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920, 1080);
            escalador.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var fondo = FabricaUI.CrearImagen("Fondo", canvasGO.transform, CatalogoSprites.Obtener("blanco"), FabricaUI.Fondo);
            FabricaUI.Estirar(fondo.rectTransform);

            _raiz = FabricaUI.CrearRect("Raiz", canvasGO.transform);
            Centrar(_raiz, 1920, 1080);

            ConstruirBarraSuperior();
            _contPropio = ConstruirPanelMapa(16, true, out _tituloPropio);
            _contEnemigo = ConstruirPanelMapa(758, false, out _tituloEnemigo);
            ConstruirPanelLateral();
            ConstruirPanelMensajes();
            _menu = ConstruirMenu(canvasGO.transform);
            _fin = ConstruirFin(canvasGO.transform);
            _fin.SetActive(false);
            _ayuda = ConstruirAyuda(canvasGO.transform);
            _ayuda.SetActive(false);
        }

        private static void AsegurarEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var modulo = go.AddComponent<InputSystemUIInputModule>();
            modulo.AssignDefaultActions();
        }

        private static void Centrar(RectTransform rt, float ancho, float alto)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(ancho, alto);
        }

        private void ConstruirBarraSuperior()
        {
            var barra = FabricaUI.CrearPanel("BarraSuperior", _raiz, 16, 10, 1888, 78).transform;

            _escudoBarra = FabricaUI.CrearImagen("Escudo", barra, CatalogoSprites.Emblema(Civilizacion.Grecia), Color.white);
            _escudoBarra.preserveAspect = true;
            FabricaUI.Ubicar(_escudoBarra.rectTransform, 16, 9, 60, 60);
            FabricaUI.CrearTexto("Titulo", barra, "IMPERIOS EN GUERRA", 30, FabricaUI.Dorado, 86, 6, 440, 40, TextAnchor.MiddleLeft, FontStyle.Bold);
            _txtSubtitulo = FabricaUI.CrearTexto("Subtitulo", barra, "Jugador vs IA", 16, FabricaUI.PergaminoTenue, 88, 44, 440, 26);

            _txtOro = ChipRecurso(barra, 560, "icono_oro", "Oro");
            _txtMadera = ChipRecurso(barra, 760, "icono_madera", "Madera");
            _txtComida = ChipRecurso(barra, 960, "icono_comida", "Comida");
            _txtPoblacion = ChipRecurso(barra, 1160, "icono_poblacion", "Población");

            var reloj = FabricaUI.CrearImagen("Reloj", barra, CatalogoSprites.Obtener("icono_reloj"), Color.white);
            reloj.preserveAspect = true;
            FabricaUI.Ubicar(reloj.rectTransform, 1380, 17, 44, 44);
            _txtTiempo = FabricaUI.CrearTexto("Tiempo", barra, "00:00", 26, FabricaUI.Pergamino, 1432, 14, 120, 50, TextAnchor.MiddleLeft, FontStyle.Bold);

            FabricaUI.CrearBoton("BtnAyuda", barra, 1560, 14, 130, 50, "Ayuda (F1)", null, () => MostrarAyuda(true), 16);
            _txtHilos = FabricaUI.CrearTexto("Hilos", barra, "", 14, FabricaUI.PergaminoTenue, 1700, 10, 176, 58, TextAnchor.MiddleRight);
        }

        private static Text ChipRecurso(Transform barra, float x, string icono, string nombre)
        {
            var img = FabricaUI.CrearImagen("Icono" + nombre, barra, CatalogoSprites.Obtener(icono), Color.white);
            img.preserveAspect = true;
            FabricaUI.Ubicar(img.rectTransform, x, 15, 46, 46);
            FabricaUI.CrearTexto("Nombre" + nombre, barra, nombre.ToUpper(), 12, FabricaUI.PergaminoTenue, x + 54, 10, 140, 18);
            return FabricaUI.CrearTexto("Valor" + nombre, barra, "0", 26, FabricaUI.Pergamino, x + 54, 26, 140, 40, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private RectTransform ConstruirPanelMapa(float x, bool esHumano, out Text titulo)
        {
            var panel = FabricaUI.CrearPanel(esHumano ? "PanelMapaPropio" : "PanelMapaEnemigo", _raiz, x, 100, 730, 764).transform;
            var color = esHumano ? FabricaUI.Azul : FabricaUI.Rojo;

            titulo = FabricaUI.CrearTexto("Titulo", panel, esHumano ? "TU IMPERIO" : "IMPERIO ENEMIGO", 22, color, 18, 8, 470, 34, TextAnchor.MiddleLeft, FontStyle.Bold);
            FabricaUI.CrearTexto("Ayuda", panel,
                esHumano ? "Todo visible  ·  selecciona y da órdenes" : "Solo ves edificios y lo que revelan tus disparos",
                13, FabricaUI.PergaminoTenue, 380, 12, 334, 28, TextAnchor.MiddleRight);

            for (int c = 0; c < ReglasJuego.ColumnasMapa; c++)
                FabricaUI.CrearTexto("Col" + c, panel, c.ToString(), 13, FabricaUI.PergaminoTenue, 22 + c * TamCelda, 42, TamCelda, 18, TextAnchor.MiddleCenter);
            for (int f = 0; f < ReglasJuego.FilasMapa; f++)
                FabricaUI.CrearTexto("Fila" + f, panel, f.ToString(), 13, FabricaUI.PergaminoTenue, 0, 62 + f * TamCelda, 22, TamCelda, TextAnchor.MiddleCenter);

            var marco = FabricaUI.CrearImagen("Marco", panel, CatalogoSprites.Obtener("blanco"), new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 1f));
            FabricaUI.Ubicar(marco.rectTransform, 19, 59, 696, 696);
            if (esHumano) _marcoPropio = marco;
            else _marcoEnemigo = marco;

            var contenedor = FabricaUI.CrearRect("ContenedorMapa", panel);
            FabricaUI.Ubicar(contenedor, 22, 62, ReglasJuego.ColumnasMapa * TamCelda, ReglasJuego.FilasMapa * TamCelda);

            if (esHumano)
            {
                var banner = FabricaUI.CrearPanel("BannerColocacion", panel, 60, 300, 610, 90, 0.95f);
                banner.raycastTarget = false;
                banner.color = new Color(1f, 0.85f, 0.55f, 0.95f);
                FabricaUI.CrearTexto("Texto", banner.transform,
                    "Haz clic en una casilla libre de tu mapa\npara ubicar tu CENTRO URBANO", 20, FabricaUI.Pergamino,
                    0, 0, 610, 90, TextAnchor.MiddleCenter, FontStyle.Bold);
                _bannerColocacion = banner.gameObject;
                _bannerColocacion.SetActive(false);
            }
            return contenedor;
        }

        /// <summary>Colores, emblemas e iconos de botones segun el bando elegido.</summary>
        private void AplicarColoresDeBando(Civilizacion civH, Civilizacion civIA)
        {
            Color Oscuro(Color c) => new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 1f);
            _tituloPropio.color = FabricaUI.ColorDe(civH);
            _tituloEnemigo.color = FabricaUI.ColorDe(civIA);
            _marcoPropio.color = Oscuro(FabricaUI.ColorDe(civH));
            _marcoEnemigo.color = Oscuro(FabricaUI.ColorDe(civIA));
            _escudoBarra.sprite = CatalogoSprites.Emblema(civH);

            _botones["casa"].Icono.sprite = CatalogoSprites.DeEdificio(TipoEdificio.Casa, civH);
            _botones["cuartel"].Icono.sprite = CatalogoSprites.DeEdificio(TipoEdificio.Cuartel, civH);
            _botones["centro"].Icono.sprite = CatalogoSprites.DeEdificio(TipoEdificio.CentroUrbano, civH);
            _botones["aldeano"].Icono.sprite = CatalogoSprites.DeUnidad(TipoUnidad.Aldeano, civH);
            _botones["infante"].Icono.sprite = CatalogoSprites.DeUnidad(TipoUnidad.Infante, civH);
            _botones["arquero"].Icono.sprite = CatalogoSprites.DeUnidad(TipoUnidad.Arquero, civH);
        }

        private VistaMapa CrearMapa(RectTransform contenedor, Mapa mapa, bool esHumano, Civilizacion civ)
        {
            for (int i = contenedor.childCount - 1; i >= 0; i--)
                Destroy(contenedor.GetChild(i).gameObject);

            var rt = FabricaUI.CrearRect("Mapa", contenedor);
            FabricaUI.Estirar(rt);
            var vista = rt.gameObject.AddComponent<VistaMapa>();
            vista.Construir(mapa.Filas, mapa.Columnas, TamCelda, esHumano, civ, esHumano ? 11 : 29);
            if (esHumano && _bannerColocacion != null) _bannerColocacion.transform.SetAsLastSibling();
            return vista;
        }

        private void ConstruirPanelLateral()
        {
            var panel = FabricaUI.CrearPanel("PanelLateral", _raiz, 1500, 100, 404, 968).transform;

            Seccion(panel, "SELECCIÓN", 10);
            var marcoRetrato = FabricaUI.CrearImagen("MarcoRetrato", panel, CatalogoSprites.Obtener("panel"), new Color(0.6f, 0.6f, 0.6f, 1f));
            marcoRetrato.type = Image.Type.Sliced;
            FabricaUI.Ubicar(marcoRetrato.rectTransform, 16, 40, 100, 100);
            _retrato = FabricaUI.CrearImagen("Retrato", panel, CatalogoSprites.Obtener("icono_escudo"), new Color(1, 1, 1, 0.3f));
            _retrato.preserveAspect = true;
            FabricaUI.Ubicar(_retrato.rectTransform, 22, 46, 88, 88);
            _txtSelNombre = FabricaUI.CrearTexto("SelNombre", panel, "Nada seleccionado", 20, FabricaUI.Pergamino, 128, 38, 262, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
            _txtSelDetalle = FabricaUI.CrearTexto("SelDetalle", panel, "", 15, FabricaUI.PergaminoTenue, 128, 70, 262, 96, TextAnchor.UpperLeft);

            Seccion(panel, "CONSTRUIR", 170);
            _botones["casa"] = FabricaUI.CrearBoton("BtnCasa", panel, 16, 198, 372, 50, "Casa",
                CatalogoSprites.DeEdificio(TipoEdificio.Casa, Civilizacion.Grecia), () => _entrada.ActivarModoConstruccion(TipoEdificio.Casa));
            _botones["cuartel"] = FabricaUI.CrearBoton("BtnCuartel", panel, 16, 252, 372, 50, "Cuartel",
                CatalogoSprites.DeEdificio(TipoEdificio.Cuartel, Civilizacion.Grecia), () => _entrada.ActivarModoConstruccion(TipoEdificio.Cuartel));
            _botones["centro"] = FabricaUI.CrearBoton("BtnCentro", panel, 16, 306, 372, 50, "Centro Urbano",
                CatalogoSprites.DeEdificio(TipoEdificio.CentroUrbano, Civilizacion.Grecia), () => _entrada.ActivarModoConstruccion(TipoEdificio.CentroUrbano));

            Seccion(panel, "ENTRENAR", 362);
            _botones["aldeano"] = FabricaUI.CrearBoton("BtnAldeano", panel, 16, 390, 372, 50, "Aldeano",
                CatalogoSprites.DeUnidad(TipoUnidad.Aldeano, Civilizacion.Grecia), () => _entrada.EntrenarUnidad(TipoUnidad.Aldeano));
            _botones["infante"] = FabricaUI.CrearBoton("BtnInfante", panel, 16, 444, 372, 50, "Infante",
                CatalogoSprites.DeUnidad(TipoUnidad.Infante, Civilizacion.Grecia), () => _entrada.EntrenarUnidad(TipoUnidad.Infante));
            _botones["arquero"] = FabricaUI.CrearBoton("BtnArquero", panel, 16, 498, 372, 50, "Arquero",
                CatalogoSprites.DeUnidad(TipoUnidad.Arquero, Civilizacion.Grecia), () => _entrada.EntrenarUnidad(TipoUnidad.Arquero));

            Seccion(panel, "ÓRDENES", 554);
            _botones["ejercito"] = FabricaUI.CrearBoton("BtnEjercito", panel, 16, 582, 182, 50, "Seleccionar\nejército",
                CatalogoSprites.Obtener("icono_espada"), () => _entrada.SeleccionarMilitares(), 15);
            _botones["inactivos"] = FabricaUI.CrearBoton("BtnInactivos", panel, 206, 582, 182, 50, "Aldeanos\ninactivos",
                CatalogoSprites.Obtener("icono_martillo"), () => _entrada.SeleccionarAldeanosInactivos(), 15);
            _botones["cancelar"] = FabricaUI.CrearBoton("BtnCancelar", panel, 16, 636, 182, 44, "Cancelar (Esc)",
                null, () => _entrada.CancelarSeleccion(), 15);
            _botones["rendirse"] = FabricaUI.CrearBoton("BtnRendirse", panel, 206, 636, 182, 44, "Rendirse",
                CatalogoSprites.Obtener("icono_calavera"), () => _entrada.Rendirse(), 15);

            Seccion(panel, "PRODUCCIÓN", 692);
            _txtProduccion = FabricaUI.CrearTexto("Produccion", panel, "", 14, FabricaUI.PergaminoTenue, 16, 718, 372, 96, TextAnchor.UpperLeft);

            Seccion(panel, "INFORMACIÓN", 820);
            _txtInfo = FabricaUI.CrearTexto("Info", panel, "", 14, FabricaUI.PergaminoTenue, 16, 846, 372, 112, TextAnchor.UpperLeft);
        }

        private static void Seccion(Transform padre, string titulo, float y)
        {
            FabricaUI.CrearTexto("Seccion" + titulo, padre, titulo, 15, FabricaUI.Dorado, 16, y, 372, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            var linea = FabricaUI.CrearImagen("Linea", padre, CatalogoSprites.Obtener("blanco"), new Color(0.9f, 0.75f, 0.4f, 0.35f));
            FabricaUI.Ubicar(linea.rectTransform, 140, y + 12, 248, 2);
        }

        private void ConstruirPanelMensajes()
        {
            var panel = FabricaUI.CrearPanel("PanelMensajes", _raiz, 16, 876, 1472, 192).transform;
            FabricaUI.CrearTexto("Titulo", panel, "MENSAJES DEL JUEGO", 15, FabricaUI.Dorado, 18, 8, 300, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            FabricaUI.CrearTexto("Ayuda", panel,
                "Clic izq.: seleccionar   ·   Clic der. (o izq. con selección): mover / recolectar   ·   Tropas seleccionadas + clic en mapa enemigo: atacar   ·   Esc: cancelar",
                13, FabricaUI.PergaminoTenue, 330, 8, 1126, 24, TextAnchor.MiddleRight);
            _txtMensajes = FabricaUI.CrearTexto("Mensajes", panel, "", 16, FabricaUI.Pergamino, 18, 36, 1436, 150, TextAnchor.UpperLeft);
            _txtMensajes.lineSpacing = 1.05f;
        }

        private GameObject ConstruirMenu(Transform canvas)
        {
            var capa = FabricaUI.CrearImagen("Menu", canvas, CatalogoSprites.Obtener("blanco"), new Color(0, 0, 0, 0.8f), true);
            FabricaUI.Estirar(capa.rectTransform);
            var panelImg = FabricaUI.CrearImagen("PanelMenu", capa.transform, CatalogoSprites.Obtener("panel"), Color.white, true);
            panelImg.type = Image.Type.Sliced;
            Centrar(panelImg.rectTransform, 880, 760);
            var p = panelImg.transform;

            FabricaUI.CrearTexto("Titulo", p, "IMPERIOS EN GUERRA", 50, FabricaUI.Dorado, 0, 18, 880, 62, TextAnchor.MiddleCenter, FontStyle.Bold);
            FabricaUI.CrearTexto("Subtitulo", p, "La Guerra de Troya  ·  Estrategia en tiempo real contra la computadora", 18, FabricaUI.Pergamino, 0, 76, 880, 30, TextAnchor.MiddleCenter);

            FabricaUI.CrearTexto("LblBando", p, "Elige tu bando", 18, FabricaUI.Dorado, 60, 114, 760, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
            _btnGrecia = FabricaUI.CrearBoton("BtnGrecia", p, 60, 146, 375, 118,
                $"GRECIA\n<size=14>Aqueos de Agamenón y Aquiles\n{ReglasJuego.Bonificacion(Civilizacion.Grecia)}</size>",
                CatalogoSprites.Emblema(Civilizacion.Grecia), () => ElegirCivilizacion(Civilizacion.Grecia), 24);
            _btnTroya = FabricaUI.CrearBoton("BtnTroya", p, 445, 146, 375, 118,
                $"TROYA\n<size=14>Troyanos de Príamo y Héctor\n{ReglasJuego.Bonificacion(Civilizacion.Troya)}</size>",
                CatalogoSprites.Emblema(Civilizacion.Troya), () => ElegirCivilizacion(Civilizacion.Troya), 24);

            FabricaUI.CrearTexto("LblDificultad", p, "Dificultad de la IA", 18, FabricaUI.Dorado, 60, 278, 760, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
            _btnFacil = FabricaUI.CrearBoton("BtnFacil", p, 60, 310, 245, 52, "Fácil", null, () => ElegirDificultad(Dificultad.Facil), 20);
            _btnNormal = FabricaUI.CrearBoton("BtnNormal", p, 318, 310, 245, 52, "Normal", null, () => ElegirDificultad(Dificultad.Normal), 20);
            _btnDificil = FabricaUI.CrearBoton("BtnDificil", p, 575, 310, 245, 52, "Difícil", null, () => ElegirDificultad(Dificultad.Dificil), 20);

            FabricaUI.CrearTexto("LblUbicacion", p, "Ubicación de tu Centro Urbano", 18, FabricaUI.Dorado, 60, 376, 760, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
            _btnAleatoria = FabricaUI.CrearBoton("BtnAleatoria", p, 60, 408, 375, 52, "Aleatoria", null, () => ElegirUbicacion(false), 20);
            _btnManual = FabricaUI.CrearBoton("BtnManual", p, 445, 408, 375, 52, "Manual (clic en el mapa)", null, () => ElegirUbicacion(true), 20);

            FabricaUI.CrearTexto("Reglas", p,
                "•  Tus aldeanos recolectan oro, madera y comida (clic en un recurso).\n" +
                "•  Construye Casas (+población) y un Cuartel para entrenar tropas.\n" +
                "•  Ataca el mapa enemigo: solo ves sus edificios y lo que revelan tus disparos.\n" +
                "•  Ganas si el enemigo se queda sin Centro Urbano Y sin unidades militares.",
                16, FabricaUI.PergaminoTenue, 60, 476, 760, 120, TextAnchor.UpperLeft);

            FabricaUI.CrearBoton("BtnJugar", p, 60, 648, 370, 76, "¡A LA GUERRA!", CatalogoSprites.Obtener("icono_espada"),
                () => _entrada.IniciarPartida(_dificultad, _manual, _civilizacion), 26);
            FabricaUI.CrearBoton("BtnComoJugar", p, 442, 648, 220, 76, "Cómo jugar", null, () => MostrarAyuda(true), 21);
            FabricaUI.CrearBoton("BtnSalir", p, 674, 648, 146, 76, "Salir", null, () => _entrada.Salir(), 21);
            return capa.gameObject;
        }

        private GameObject ConstruirAyuda(Transform canvas)
        {
            var capa = FabricaUI.CrearImagen("Ayuda", canvas, CatalogoSprites.Obtener("blanco"), new Color(0, 0, 0, 0.82f), true);
            FabricaUI.Estirar(capa.rectTransform);
            var panelImg = FabricaUI.CrearImagen("PanelAyuda", capa.transform, CatalogoSprites.Obtener("panel"), Color.white, true);
            panelImg.type = Image.Type.Sliced;
            Centrar(panelImg.rectTransform, 1100, 860);
            var p = panelImg.transform;

            FabricaUI.CrearTexto("Titulo", p, "CÓMO JUGAR Y GANAR", 36, FabricaUI.Dorado, 0, 16, 1100, 50, TextAnchor.MiddleCenter, FontStyle.Bold);
            FabricaUI.CrearTexto("Texto", p,
                "<b><color=#E8BE60>OBJETIVO</color></b>  Destruye el Centro Urbano enemigo <b>y</b> todas sus unidades militares. Si solo tumbas el Centro Urbano, la guerra sigue.\n\n" +
                "<b><color=#E8BE60>1. ECONOMÍA (primeros 30 s)</color></b>  Pulsa <i>Aldeanos inactivos</i> y haz clic en un arbusto de bayas. Entrena 2 aldeanos más " +
                "(Centro Urbano). Reparte: 2 en madera, 2 en comida, 1 en oro.\n\n" +
                "<b><color=#E8BE60>2. CUARTEL DE INMEDIATO</color></b>  Empiezas con 200 de madera y el Cuartel cuesta 150: constrúyelo en el primer minuto, " +
                "lejos del borde. Sin Cuartel no hay ejército.\n\n" +
                "<b><color=#E8BE60>3. POBLACIÓN</color></b>  El Centro Urbano da 5 de población. Cuando veas 4/5 o 5/5, construye una Casa (50 de madera, +5).\n\n" +
                "<b><color=#E8BE60>4. EJÉRCITO SIN PARAR</color></b>  Hoplita/Lancero: 20 de daño a edificios. Arquero: menos daño pero revela 3x3 casillas. " +
                "Ten siempre algo entrenándose; combina 2 infantes por cada arquero.\n\n" +
                "<b><color=#E8BE60>5. ATAQUE CONCENTRADO</color></b>  <i>Seleccionar ejército</i> + clic en el mapa enemigo: todos disparan a la misma casilla. Orden:\n" +
                "     a) Su <b>Cuartel</b> (deja de producir tropas).\n" +
                "     b) Dispara con arqueros a las casillas <b>junto</b> a su Cuartel y su Centro Urbano: ahí aparecen sus soldados.\n" +
                "     c) Cuando una casilla revelada muestre soldados, ¡fuego sobre ellos!\n" +
                "     d) Remata el Centro Urbano.\n\n" +
                "<b><color=#E8BE60>6. DEFENSA</color></b>  La IA adivina dónde están tus unidades, sobre todo junto a los recursos. Repón las bajas. " +
                "Si pierdes tu Centro Urbano pero te quedan militares, sigues vivo y puedes reconstruirlo (275 madera + 100 oro).\n\n" +
                "<b><color=#E8BE60>CONTROLES</color></b>  Clic izq.: seleccionar  ·  Clic der. o izq. con selección: mover/recolectar  ·  Esc: cancelar  ·  F1: esta ayuda",
                17, FabricaUI.Pergamino, 50, 76, 1000, 690, TextAnchor.UpperLeft);
            FabricaUI.CrearBoton("BtnCerrar", p, 400, 780, 300, 60, "Entendido", null, () => MostrarAyuda(false), 22);
            return capa.gameObject;
        }

        private void MostrarAyuda(bool visible)
        {
            _ayuda.SetActive(visible);
            if (visible) _ayuda.transform.SetAsLastSibling();
        }

        private void ElegirCivilizacion(Civilizacion civ)
        {
            _civilizacion = civ;
            ActualizarOpcionesMenu();
        }

        private GameObject ConstruirFin(Transform canvas)
        {
            var capa = FabricaUI.CrearImagen("Fin", canvas, CatalogoSprites.Obtener("blanco"), new Color(0, 0, 0, 0.72f), true);
            FabricaUI.Estirar(capa.rectTransform);
            var panelImg = FabricaUI.CrearImagen("PanelFin", capa.transform, CatalogoSprites.Obtener("panel"), Color.white, true);
            panelImg.type = Image.Type.Sliced;
            Centrar(panelImg.rectTransform, 780, 610);
            var p = panelImg.transform;

            _imgFin = FabricaUI.CrearImagen("Icono", p, CatalogoSprites.Obtener("icono_corona"), Color.white);
            _imgFin.preserveAspect = true;
            FabricaUI.Ubicar(_imgFin.rectTransform, 320, 28, 140, 140);
            _txtFinTitulo = FabricaUI.CrearTexto("Titulo", p, "", 56, FabricaUI.Dorado, 0, 172, 780, 70, TextAnchor.MiddleCenter, FontStyle.Bold);
            _txtFinGanador = FabricaUI.CrearTexto("Ganador", p, "", 22, FabricaUI.Pergamino, 0, 240, 780, 34, TextAnchor.MiddleCenter, FontStyle.Bold);
            _txtFinResumen = FabricaUI.CrearTexto("Resumen", p, "", 17, FabricaUI.Pergamino, 70, 284, 640, 150, TextAnchor.MiddleCenter);
            _txtFinArchivos = FabricaUI.CrearTexto("Archivos", p, "", 13, FabricaUI.PergaminoTenue, 30, 440, 720, 60, TextAnchor.MiddleCenter);

            FabricaUI.CrearBoton("BtnOtra", p, 80, 516, 360, 64, "Volver al menú", CatalogoSprites.Obtener("icono_escudo"),
                () => _entrada.VolverAlMenu(), 22);
            FabricaUI.CrearBoton("BtnSalirFin", p, 460, 516, 240, 64, "Salir", null, () => _entrada.Salir(), 22);
            return capa.gameObject;
        }

        private void ElegirDificultad(Dificultad d)
        {
            _dificultad = d;
            ActualizarOpcionesMenu();
        }

        private void ElegirUbicacion(bool manual)
        {
            _manual = manual;
            ActualizarOpcionesMenu();
        }

        private void ActualizarOpcionesMenu()
        {
            _btnFacil.Resaltar(_dificultad == Dificultad.Facil);
            _btnNormal.Resaltar(_dificultad == Dificultad.Normal);
            _btnDificil.Resaltar(_dificultad == Dificultad.Dificil);
            _btnGrecia.Resaltar(_civilizacion == Civilizacion.Grecia);
            _btnTroya.Resaltar(_civilizacion == Civilizacion.Troya);
            _btnAleatoria.Resaltar(!_manual);
            _btnManual.Resaltar(_manual);
        }

        // --- Actualizacion de paneles ---

        private void ActualizarSeleccion(Jugador humano, Seleccion sel)
        {
            if (sel.IdsUnidades.Count == 1 && humano.BuscarUnidad(sel.IdsUnidades[0]) is Unidad u)
            {
                Retrato(CatalogoSprites.DeUnidad(u.Tipo, u.Civilizacion));
                _txtSelNombre.text = u.Nombre;
                string extra = u.EsMilitar()
                    ? $"{(u.EstaRecargando ? "<color=#FFB050>Recargando...</color>" : "<color=#7CE07C>Listo para disparar</color>")}  ·  Daño {u.Daño}/{u.DañoEdificios}"
                    : "Clic en un recurso para recolectar";
                _txtSelDetalle.text = $"Vida: {u.VidaActual}/{u.VidaMaxima}\nEstado: {u.DescripcionEstado}\n{extra}\nPosición: {u.Posicion}";
            }
            else if (sel.IdsUnidades.Count > 1)
            {
                var unidades = sel.IdsUnidades.Select(humano.BuscarUnidad).Where(x => x != null).ToList();
                var primera = unidades.FirstOrDefault();
                if (primera != null) Retrato(CatalogoSprites.DeUnidad(primera.Tipo, primera.Civilizacion));
                _txtSelNombre.text = $"{unidades.Count} unidades";
                var conteo = unidades.GroupBy(x => x.Tipo).Select(g => $"{g.Count()} {ReglasJuego.Nombre(g.Key, humano.Civilizacion)}");
                int militares = unidades.Count(x => x.EsMilitar());
                int listos = unidades.Count(x => x.EsMilitar() && !x.EstaRecargando);
                _txtSelDetalle.text = string.Join(", ", conteo) +
                    (militares > 0 ? $"\nListos para disparar: {listos}/{militares}\nClic en el mapa enemigo para atacar" : "\nClic en un recurso o casilla libre");
            }
            else if (sel.IdEdificio != null && humano.BuscarEdificio(sel.IdEdificio) is Edificio e)
            {
                Retrato(CatalogoSprites.DeEdificio(e.Tipo, e.Civilizacion));
                _txtSelNombre.text = ReglasJuego.Nombre(e.Tipo);
                string estado = e.Estado == EstadoConstruccion.EnConstruccion ? $"En construcción ({e.ProgresoConstruccion}%)" : "Terminado";
                string produce = e.Tipo == TipoEdificio.CentroUrbano ? "Entrena: Aldeanos  ·  +5 población"
                    : e.Tipo == TipoEdificio.Cuartel ? "Entrena: Infantes y Arqueros" : "+5 población";
                string entrenando = e.EstaEntrenando ? $"\nEntrenando {e.UnidadEnEntrenamiento}: {e.ProgresoEntrenamiento}%" : "";
                _txtSelDetalle.text = $"Vida: {e.VidaActual}/{e.VidaMaxima}\nEstado: {estado}\n{produce}{entrenando}";
            }
            else
            {
                Retrato(null);
                _txtSelNombre.text = sel.Modo == ModoAccion.Construir ? $"Construir {ReglasJuego.Nombre(sel.EdificioAConstruir)}" : "Nada seleccionado";
                _txtSelDetalle.text = sel.Modo == ModoAccion.Construir
                    ? "Haz clic en una casilla libre (verde) de tu mapa.\nClic derecho o Esc para cancelar."
                    : "Haz clic en una unidad o edificio de tu mapa.";
            }
        }

        private void Retrato(Sprite sprite)
        {
            _retrato.sprite = sprite != null ? sprite : CatalogoSprites.Obtener("icono_escudo");
            _retrato.color = sprite != null ? Color.white : new Color(1, 1, 1, 0.3f);
        }

        private void ActualizarBotones(Jugador humano, Seleccion sel)
        {
            BotonEdificio("casa", TipoEdificio.Casa, "+5 población", humano, sel);
            BotonEdificio("cuartel", TipoEdificio.Cuartel, "entrena tropas", humano, sel);
            BotonEdificio("centro", TipoEdificio.CentroUrbano, "+5 pobl., aldeanos", humano, sel);
            BotonUnidad("aldeano", TipoUnidad.Aldeano, humano);
            BotonUnidad("infante", TipoUnidad.Infante, humano);
            BotonUnidad("arquero", TipoUnidad.Arquero, humano);

            int inactivos = humano.Unidades.Count(u => u.Tipo == TipoUnidad.Aldeano && u.Estado == EstadoUnidad.Inactiva);
            _botones["inactivos"].Texto.text = $"Aldeanos\ninactivos ({inactivos})";
            _botones["inactivos"].Resaltar(inactivos > 0);
            _botones["ejercito"].Texto.text = $"Seleccionar\nejército ({humano.ContarMilitares()})";
        }

        private void BotonEdificio(string clave, TipoEdificio tipo, string nota, Jugador humano, Seleccion sel)
        {
            var costo = ReglasJuego.CostoEdificio(tipo);
            _botones[clave].Texto.text = $"{ReglasJuego.Nombre(tipo)}   <size=13>{Costo(costo, humano)}  ·  {nota}</size>";
            _botones[clave].Resaltar(sel.Modo == ModoAccion.Construir && sel.EdificioAConstruir == tipo);
        }

        private void BotonUnidad(string clave, TipoUnidad tipo, Jugador humano)
        {
            var costo = ReglasJuego.CostoUnidad(tipo);
            var requiere = ReglasJuego.EdificioEntrenador(tipo);
            bool tieneEdificio = humano.Edificios.Any(e => e.Tipo == requiere && e.EstaOperativo());
            string nota = tieneEdificio ? $"{ReglasJuego.Estadisticas(tipo).TiempoEntrenamientoMs / 1000}s" : $"<color=#FF8A7A>requiere {ReglasJuego.Nombre(requiere)}</color>";
            _botones[clave].Texto.text = $"{ReglasJuego.Nombre(tipo, humano.Civilizacion)}   <size=13>{Costo(costo, humano)}  ·  {nota}</size>";
        }

        private static string Costo(Dictionary<TipoRecurso, int> costo, Jugador humano)
        {
            string color = humano.Recursos.AlcanzaPara(costo) ? "#B8E6A0" : "#FF8A7A";
            return $"<color={color}>{ReglasJuego.CostoComoTexto(costo)}</color>";
        }

        private void ActualizarProduccion(Jugador humano)
        {
            var sb = new StringBuilder();
            int lineas = 0;
            foreach (var e in humano.Edificios.OrderBy(x => x.Tipo))
            {
                if (lineas >= 5) break;
                if (e.Estado == EstadoConstruccion.EnConstruccion)
                {
                    sb.AppendLine($"•  Construyendo {ReglasJuego.Nombre(e.Tipo)} {e.Posicion}: {e.ProgresoConstruccion}%");
                    lineas++;
                }
                else if (e.EstaEntrenando)
                {
                    sb.AppendLine($"•  {ReglasJuego.Nombre(e.Tipo)} entrena {e.UnidadEnEntrenamiento}: {e.ProgresoEntrenamiento}%");
                    lineas++;
                }
            }
            _txtProduccion.text = lineas == 0 ? "Sin producción en curso." : sb.ToString();
        }

        private void ActualizarInfo(Jugador humano, Seleccion sel)
        {
            if (_partida != null && _partida.Estado == EstadoPartida.Preparando)
            {
                _txtInfo.text = "Elige dónde ubicar tu Centro Urbano: debe ser una casilla libre con espacio alrededor para tus 3 aldeanos.";
                return;
            }

            if (_hoverPropio.HasValue && _ultimoPropio != null)
            {
                var i = _ultimoPropio[_hoverPropio.Value.Fila, _hoverPropio.Value.Columna];
                string texto = Describir(i, humano, _partida.JugadorHumano, true);
                if (sel.Modo == ModoAccion.Construir)
                    texto += i.Tipo == TipoCasilla.Libre ? "\n<color=#7CE07C>Puedes construir aquí.</color>" : "\n<color=#FF8A7A>Casilla ocupada.</color>";
                _txtInfo.text = $"<b>Tu mapa {i.Posicion}</b>\n{texto}";
            }
            else if (_hoverEnemigo.HasValue && _ultimoEnemigo != null)
            {
                var i = _ultimoEnemigo[_hoverEnemigo.Value.Fila, _hoverEnemigo.Value.Columna];
                bool visible = humano.TieneRevelada(i.Posicion);
                string texto;
                if (visible || i.TieneEdificio) texto = Describir(i, humano, _partida.JugadorIA, visible);
                else if (i.Tipo == TipoCasilla.RecursoNatural) texto = $"{ReglasJuego.Nombre(i.Recurso.Value)} (puede haber aldeanos cerca)";
                else texto = "Casilla sin explorar. Dispárale para revelarla.";
                if (sel.TieneUnidades) texto += "\n<color=#FF8A7A>Clic para disparar aquí.</color>";
                _txtInfo.text = $"<b>Mapa enemigo {i.Posicion}</b>\n{texto}";
            }
            else
            {
                _txtInfo.text = "Pasa el cursor sobre una casilla para ver qué hay.\nLas casillas oscuras del mapa enemigo están ocultas.";
            }
        }

        private static string Describir(InfoCasilla i, Jugador humano, Jugador dueño, bool verUnidades)
        {
            switch (i.Tipo)
            {
                case TipoCasilla.RecursoNatural:
                    return $"{ReglasJuego.Nombre(i.Recurso.Value)}: quedan {i.CantidadRecurso}.";
                case TipoCasilla.Edificio:
                    string estado = i.EstadoEdificio == EstadoConstruccion.EnConstruccion ? $"en construcción {i.ProgresoConstruccion}%" : "terminado";
                    return $"{ReglasJuego.Nombre(i.TipoEdificio)} ({estado})\nVida: {i.VidaEdificio}/{i.VidaMaximaEdificio}";
                case TipoCasilla.Unidad when verUnidades:
                    var u = dueño.BuscarUnidad(i.IdUnidad);
                    return u == null ? ReglasJuego.Nombre(i.TipoUnidad, dueño.Civilizacion)
                        : $"{u.Nombre}  ·  Vida {u.VidaActual}/{u.VidaMaxima}\n{u.DescripcionEstado}";
                default:
                    return "Casilla libre.";
            }
        }

        /// <summary>Evita que un "&lt;" en un mensaje rompa el texto enriquecido.</summary>
        private static string Escapar(string texto) => texto.Replace("<", "‹").Replace(">", "›");
    }
}
