using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Modelo;

namespace Controlador
{
    /// <summary>
    /// Controlador del patrón MVC:
    ///   - Recibe la entrada del usuario desde la Vista (IEntradaJugador),
    ///     valida el contexto (selección, modo) y ordena al Modelo.
    ///   - Cada frame vacía la ColaEventos del Modelo (hilo principal de
    ///     Unity), registra los eventos en el log y avisa a la Vista.
    ///   - No dibuja nada ni contiene reglas del juego (esas viven en Modelo).
    ///
    /// Es el ÚNICO punto donde el trabajo de los hilos secundarios vuelve al
    /// hilo principal: los hilos encolan EventoJuego y aquí se consumen.
    /// </summary>
    public class JuegoControlador : MonoBehaviour, IEntradaJugador
    {
        [Tooltip("Cada cuántos segundos se vuelve a leer el Modelo para refrescar la Vista.")]
        [SerializeField] private float intervaloRefrescoSeg = 0.12f;

        [Tooltip("Semilla para generar el mapa. 0 = aleatoria en cada partida.")]
        [SerializeField] private int semillaMapa = 0;

        [SerializeField] private string nombreJugador = "Jugador 1";

        private readonly List<IVistaJuego> _vistas = new List<IVistaJuego>();
        private readonly Seleccion _seleccion = new Seleccion();

        private Partida _partida;
        private GestorConcurrencia _gestor;
        private LogicaIA _logicaIA;
        private GestorArchivos _archivos;
        private float _proximoRefresco;
        private bool _finProcesado;
        private bool _iniciado;

        private Jugador Humano => _partida?.JugadorHumano;

        // ================================================================
        // Ciclo de vida de Unity
        // ================================================================

        private void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
        }

        private void Start()
        {
            // Las Vistas pueden registrarse antes o después de este Start (según el orden de Unity);
            // por eso el menú se muestra aquí para todas las ya registradas.
            foreach (var vista in GetComponents<MonoBehaviour>().OfType<IVistaJuego>())
                RegistrarVista(vista);
            _iniciado = true;
            Avisar(v => v.MostrarMenuInicio());
        }

        /// <summary>Permite que una Vista creada en tiempo de ejecución se suscriba.</summary>
        public void RegistrarVista(IVistaJuego vista)
        {
            if (vista == null || _vistas.Contains(vista)) return;
            _vistas.Add(vista);
            vista.Conectar(this);
            if (_iniciado && _partida == null) vista.MostrarMenuInicio();
        }

        private void Update()
        {
            if (_partida == null) return;

            bool huboCambios = false;
            foreach (var evento in _partida.Eventos.DesencolarTodos())
            {
                ProcesarEvento(evento);
                huboCambios = true;
            }

            if (_partida.Estado == EstadoPartida.Finalizada && !_finProcesado)
                TerminarPartida();

            if (huboCambios || Time.unscaledTime >= _proximoRefresco)
            {
                _proximoRefresco = Time.unscaledTime + intervaloRefrescoSeg;
                _seleccion.Depurar(Humano);
                int hilos = (_gestor?.TareasActivas ?? 0) + 2; // + Hilo-IA + Hilo-Log
                foreach (var v in _vistas) v.Refrescar(_partida, _seleccion, hilos);
            }
        }

        private void OnApplicationQuit() => LiberarPartida(guardarSiInterrumpida: true);
        private void OnDestroy() => LiberarPartida(guardarSiInterrumpida: false);

        // ================================================================
        // Eventos del Modelo -> Vista (+ log)
        // ================================================================

        private void ProcesarEvento(EventoJuego e)
        {
            if (e.EsRelevanteParaLog) _archivos?.RegistrarEvento(e);

            bool delHumano = e.IdJugador == Humano.Id;
            var pos = e.Posicion ?? new Posicion(0, 0);

            switch (e.Tipo)
            {
                case TipoEvento.Ataque:
                    // Si atacó el humano, el disparo cae en el mapa de la IA, y viceversa.
                    Avisar(v => v.MostrarAtaque(!delHumano, pos, e.Exito, e.Valor));
                    Mensaje(delHumano ? $"Tu ataque: {TextoResultado(e)}"
                            : $"¡{ReglasJuego.Nombre(_partida.JugadorIA.Civilizacion)} atacó {pos}! {TextoResultado(e)}",
                        delHumano ? TipoMensaje.Combate : TipoMensaje.Enemigo);
                    break;

                case TipoEvento.UnidadDestruida:
                case TipoEvento.EdificioDestruido:
                    // IdJugador es el dueño de lo destruido.
                    Avisar(v => v.MostrarDestruccion(delHumano, pos));
                    Mensaje(delHumano ? $"Perdiste: {e.Descripcion}" : $"¡Enemigo abatido! {e.Descripcion}",
                        delHumano ? TipoMensaje.Enemigo : TipoMensaje.Exito);
                    break;

                case TipoEvento.RecursoRecolectado:
                    if (delHumano) Avisar(v => v.MostrarRecoleccion(pos, e.Descripcion));
                    break;

                case TipoEvento.ConstruccionCompletada:
                case TipoEvento.EntrenamientoCompletado:
                    if (delHumano) Mensaje(e.Descripcion, TipoMensaje.Exito);
                    break;

                case TipoEvento.ConstruccionIniciada:
                case TipoEvento.EntrenamientoIniciado:
                case TipoEvento.PartidaIniciada:
                    if (delHumano) Mensaje(e.Descripcion, TipoMensaje.Info);
                    break;

                case TipoEvento.MensajeSistema:
                    if (delHumano || e.IdJugador == null)
                        Mensaje(e.Descripcion, e.Exito ? TipoMensaje.Info : TipoMensaje.Error);
                    break;

                // Órdenes de movimiento/recolección y fin de partida: solo log (el fin se procesa aparte).
            }
        }

        private static string TextoResultado(EventoJuego e)
        {
            int i = e.Descripcion.IndexOf(": ", StringComparison.Ordinal);
            return i >= 0 ? e.Descripcion.Substring(i + 2) : e.Descripcion;
        }

        private void TerminarPartida()
        {
            _finProcesado = true;
            _seleccion.Limpiar();
            _logicaIA?.Detener();
            _gestor?.DetenerTodo();

            bool guardado = _archivos != null && _archivos.GuardarResultadoFinal(_partida);
            _archivos?.Dispose();

            var ganador = _partida.Ganador;
            bool ganoHumano = ganador == Humano;
            var yo = Humano.Estadisticas;
            string resumen =
                $"Duración: {_partida.Duracion:mm\\:ss}\n" +
                $"Recursos recolectados: {yo.RecursosRecolectados}\n" +
                $"Unidades entrenadas: {yo.UnidadesEntrenadas}   ·   Edificios construidos: {yo.EdificiosConstruidos}\n" +
                $"Disparos acertados: {yo.DisparosAcertados} de {yo.DisparosAcertados + yo.DisparosFallados}\n" +
                $"Enemigos destruidos: {yo.UnidadesEnemigasDestruidas} unidades, {yo.EdificiosEnemigosDestruidos} edificios";
            string carpeta = guardado ? _archivos.Carpeta : "(no se pudieron guardar los archivos)";

            Avisar(v => v.MostrarFinDePartida(ganoHumano, ganador?.Nombre ?? "-", resumen, carpeta));
        }

        // ================================================================
        // Entrada del usuario (IEntradaJugador)
        // ================================================================

        public void IniciarPartida(Dificultad dificultad, bool ubicacionManual, Civilizacion civilizacion)
        {
            try
            {
                LiberarPartida(guardarSiInterrumpida: false);
                _seleccion.Limpiar();
                _finProcesado = false;

                _partida = InicializadorPartida.CrearPartida(nombreJugador, dificultad, ubicacionManual, civilizacion,
                    semillaMapa == 0 ? (int?)null : semillaMapa);
                _gestor = new GestorConcurrencia(_partida);
                _archivos = new GestorArchivos(CarpetaArchivos());
                if (_archivos.UltimoError != null) Mensaje(_archivos.UltimoError, TipoMensaje.Error);

                Avisar(v => v.MostrarPartida(_partida));

                if (ubicacionManual)
                {
                    Avisar(v => v.MostrarColocacionInicial(true));
                    Mensaje("Haz clic en una casilla libre de TU mapa para ubicar tu Centro Urbano.", TipoMensaje.Advertencia);
                }
                else
                {
                    ComenzarPartida();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Mensaje($"No se pudo iniciar la partida: {ex.Message}", TipoMensaje.Error);
            }
        }

        private void ComenzarPartida()
        {
            var r = _partida.Comenzar();
            if (!r.Exito)
            {
                Mensaje(r.Mensaje, TipoMensaje.Error);
                return;
            }
            Avisar(v => v.MostrarColocacionInicial(false));

            if (!_archivos.GuardarConfiguracionInicial(_partida))
                Mensaje(_archivos.UltimoError, TipoMensaje.Error);

            _logicaIA = new LogicaIA(_partida, _gestor);
            _logicaIA.Iniciar();
            var h = _partida.JugadorHumano;
            Mensaje($"Juegas con {ReglasJuego.Nombre(h.Civilizacion)} ({ReglasJuego.Bonificacion(h.Civilizacion)}). " +
                    "Recolecta, construye un Cuartel y entrena tropas: ¡destruye el Centro Urbano y el ejército enemigo! (Ayuda: F1)",
                TipoMensaje.Info);
        }

        public void ClicMapaPropio(Posicion pos, bool clicDerecho)
        {
            if (_partida == null) return;
            try
            {
                if (_partida.Estado == EstadoPartida.Preparando)
                {
                    var r = InicializadorPartida.UbicarCentroUrbanoManual(_partida, pos);
                    Mensaje(r.Mensaje, r.Exito ? TipoMensaje.Exito : TipoMensaje.Advertencia);
                    if (r.Exito) ComenzarPartida();
                    return;
                }
                if (!_partida.EnCurso) return;
                if (!Humano.Mapa.EstaDentroDelMapa(pos)) return;

                if (_seleccion.Modo == ModoAccion.Construir)
                {
                    if (clicDerecho)
                    {
                        _seleccion.SalirDeConstruccion();
                        Mensaje("Construcción cancelada.", TipoMensaje.Info);
                        return;
                    }
                    var r = _gestor.Construir(Humano, _seleccion.EdificioAConstruir, pos);
                    if (r.Exito) _seleccion.SalirDeConstruccion();
                    else Mensaje(r.Mensaje, TipoMensaje.Advertencia);
                    return;
                }

                var info = Humano.Mapa.ObtenerInfo(pos);

                // Clic izquierdo sobre algo propio = seleccionar.
                if (!clicDerecho && info.TieneUnidad)
                {
                    _seleccion.SeleccionarUnidades(new[] { info.IdUnidad });
                    return;
                }
                if (!clicDerecho && info.TieneEdificio)
                {
                    _seleccion.SeleccionarEdificio(info.IdEdificio);
                    return;
                }

                // Con unidades seleccionadas: clic en recurso = recolectar, clic en casilla libre = mover.
                if (_seleccion.TieneUnidades)
                {
                    OrdenarUnidades(pos, info);
                    return;
                }

                if (!clicDerecho) _seleccion.Limpiar();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Mensaje($"Acción no válida: {ex.Message}", TipoMensaje.Error);
            }
        }

        private void OrdenarUnidades(Posicion pos, InfoCasilla info)
        {
            var unidades = _seleccion.IdsUnidades.Select(Humano.BuscarUnidad).Where(u => u != null && u.EstaVivo()).ToList();
            if (unidades.Count == 0) return;

            if (info.Tipo == TipoCasilla.RecursoNatural)
            {
                var aldeanos = unidades.Where(u => u.Tipo == TipoUnidad.Aldeano).ToList();
                if (aldeanos.Count == 0)
                {
                    Mensaje("Solo los aldeanos pueden recolectar recursos.", TipoMensaje.Advertencia);
                    return;
                }
                ResultadoAccion ultimo = default;
                foreach (var a in aldeanos) ultimo = _gestor.Recolectar(Humano, a, pos);
                Mensaje(ultimo.Mensaje, ultimo.Exito ? TipoMensaje.Info : TipoMensaje.Advertencia);
                return;
            }

            if (info.Tipo != TipoCasilla.Libre)
            {
                Mensaje($"La casilla {pos} está ocupada.", TipoMensaje.Advertencia);
                return;
            }

            // Varias unidades: cada una va a una casilla libre distinta alrededor del destino.
            var destinos = Humano.Mapa.CasillasLibresCercanas(pos, unidades.Count, 3);
            int movidas = 0;
            string error = null;
            for (int i = 0; i < unidades.Count && i < destinos.Count; i++)
            {
                var r = _gestor.Mover(Humano, unidades[i], destinos[i]);
                if (r.Exito) movidas++;
                else error = r.Mensaje;
            }
            if (movidas == 0 && error != null) Mensaje(error, TipoMensaje.Advertencia);
        }

        public void ClicMapaEnemigo(Posicion pos, bool clicDerecho)
        {
            if (_partida == null || !_partida.EnCurso) return;
            try
            {
                var militares = _seleccion.IdsUnidades.Select(Humano.BuscarUnidad)
                    .Where(u => u != null && u.EstaVivo() && u.EsMilitar()).ToList();
                if (militares.Count == 0)
                {
                    Mensaje("Para atacar, selecciona tus Infantes/Arqueros (botón \"Seleccionar ejército\") y luego haz clic en el mapa enemigo.",
                        TipoMensaje.Advertencia);
                    return;
                }

                int disparos = 0;
                string motivo = null;
                foreach (var u in militares)
                {
                    var r = _gestor.Atacar(Humano, u, pos);
                    if (r.Exito) disparos++;
                    else motivo = r.Mensaje;
                    if (!_partida.EnCurso) break;
                }
                if (disparos == 0) Mensaje(militares.Count > 1 ? "Todas tus tropas seleccionadas están recargando." : motivo, TipoMensaje.Advertencia);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Mensaje($"Ataque no válido: {ex.Message}", TipoMensaje.Error);
            }
        }

        public void ActivarModoConstruccion(TipoEdificio tipo)
        {
            if (_partida == null || !_partida.EnCurso) return;
            var costo = ReglasJuego.CostoEdificio(tipo);
            if (!Humano.Recursos.AlcanzaPara(costo))
            {
                Mensaje($"No te alcanza: {ReglasJuego.Nombre(tipo)} cuesta {ReglasJuego.CostoComoTexto(costo)}.", TipoMensaje.Advertencia);
                return;
            }
            _seleccion.ActivarConstruccion(tipo);
            Mensaje($"Elige una casilla libre de tu mapa para el {ReglasJuego.Nombre(tipo)} (clic derecho o Esc para cancelar).", TipoMensaje.Info);
        }

        public void EntrenarUnidad(TipoUnidad tipo)
        {
            if (_partida == null || !_partida.EnCurso) return;
            var r = _gestor.Entrenar(Humano, tipo);
            if (!r.Exito) Mensaje(r.Mensaje, TipoMensaje.Advertencia);
        }

        public void SeleccionarMilitares()
        {
            if (_partida == null || !_partida.EnCurso) return;
            var ids = Humano.Unidades.Where(u => u.EsMilitar() && u.EstaVivo()).Select(u => u.Id).ToList();
            if (ids.Count == 0)
            {
                Mensaje("No tienes unidades militares. Construye un Cuartel y entrena Infantes o Arqueros.", TipoMensaje.Advertencia);
                return;
            }
            _seleccion.SeleccionarUnidades(ids);
            Mensaje($"{ids.Count} unidades militares seleccionadas. Haz clic en el mapa enemigo para atacar.", TipoMensaje.Info);
        }

        public void SeleccionarAldeanosInactivos()
        {
            if (_partida == null || !_partida.EnCurso) return;
            var ids = Humano.Unidades.Where(u => u.Tipo == TipoUnidad.Aldeano && u.EstaVivo() && u.Estado == EstadoUnidad.Inactiva)
                .Select(u => u.Id).ToList();
            if (ids.Count == 0)
            {
                Mensaje("No hay aldeanos inactivos.", TipoMensaje.Info);
                return;
            }
            _seleccion.SeleccionarUnidades(ids);
            Mensaje($"{ids.Count} aldeanos inactivos seleccionados. Haz clic en un recurso para ponerlos a trabajar.", TipoMensaje.Info);
        }

        public void CancelarSeleccion() => _seleccion.Limpiar();

        public void Rendirse()
        {
            if (_partida == null || !_partida.EnCurso) return;
            _partida.Rendirse(Humano);
        }

        public void VolverAlMenu()
        {
            LiberarPartida(guardarSiInterrumpida: true);
            _partida = null;
            _seleccion.Limpiar();
            Avisar(v => v.MostrarMenuInicio());
        }

        public void Salir()
        {
            LiberarPartida(guardarSiInterrumpida: true);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ================================================================
        // Utilidades
        // ================================================================

        /// <summary>Detiene los hilos de la partida actual y cierra los archivos.</summary>
        private void LiberarPartida(bool guardarSiInterrumpida)
        {
            if (_partida == null) return;
            _logicaIA?.Detener();
            _gestor?.DetenerTodo();
            if (guardarSiInterrumpida && _partida.EnCurso && !_finProcesado)
                _archivos?.GuardarResultadoFinal(_partida);
            _archivos?.Dispose();
            _logicaIA = null;
            _gestor = null;
            _archivos = null;
        }

        /// <summary>
        /// En el editor: la carpeta del proyecto. En el build: la carpeta del
        /// ejecutable (Application.dataPath apunta a "Juego_Data").
        /// </summary>
        private static string CarpetaArchivos()
        {
            var raiz = Directory.GetParent(Application.dataPath)?.FullName ?? Application.persistentDataPath;
            return Path.Combine(raiz, "ArchivosPartida");
        }

        private void Mensaje(string texto, TipoMensaje tipo)
        {
            if (string.IsNullOrEmpty(texto)) return;
            Avisar(v => v.MostrarMensaje(texto, tipo));
        }

        private void Avisar(Action<IVistaJuego> accion)
        {
            foreach (var v in _vistas)
            {
                try { accion(v); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }
    }
}
