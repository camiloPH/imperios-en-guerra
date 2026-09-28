using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Modelo
{
    /// <summary>
    /// Crea los hilos de trabajo (Task): recoleccion, construccion, entrenamiento,
    /// movimiento y recarga. Cada accion valida, cobra, lanza la Task y encola un evento.
    /// Se cancelan con CancellationToken y se esperan en DetenerTodo().
    /// </summary>
    public class GestorConcurrencia
    {
        private readonly Partida _partida;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ConcurrentDictionary<int, Task> _tareas = new ConcurrentDictionary<int, Task>();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _ordenes =
            new ConcurrentDictionary<string, CancellationTokenSource>();

        public GestorConcurrencia(Partida partida)
        {
            _partida = partida ?? throw new ArgumentNullException(nameof(partida));
        }

        /// <summary>Hilos de trabajo activos (se muestra en la interfaz).</summary>
        public int TareasActivas => _tareas.Count;

        /// <summary>Cancela todos los hilos y espera a que terminen.</summary>
        public void DetenerTodo(int esperaMs = 1500)
        {
            if (!_cts.IsCancellationRequested) _cts.Cancel();
            foreach (var orden in _ordenes.Values) orden.Cancel();
            try
            {
                Task.WaitAll(_tareas.Values.ToArray(), esperaMs);
            }
            catch (AggregateException) { /* las tareas canceladas lanzan; es lo esperado */ }
        }

        // --- Infraestructura de hilos ---

        private void Lanzar(string nombre, Func<CancellationToken, Task> trabajo, CancellationToken token)
        {
            var tarea = Task.Run(async () =>
            {
                try
                {
                    await trabajo(token);
                }
                catch (OperationCanceledException) { /* cancelación normal */ }
                catch (Exception ex)
                {
                    _partida.Notificar(TipoEvento.MensajeSistema, null, $"Error en el hilo '{nombre}': {ex.Message}", exito: false);
                }
            });
            _tareas[tarea.Id] = tarea;
            tarea.ContinueWith(t => _tareas.TryRemove(t.Id, out _), TaskScheduler.Default);
        }

        /// <summary>Nueva orden para la unidad: cancela la anterior.</summary>
        private CancellationToken NuevaOrden(Unidad unidad)
        {
            var nueva = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            CancellationTokenSource anterior = null;
            _ordenes.AddOrUpdate(unidad.Id, nueva, (_, vieja) => { anterior = vieja; return nueva; });
            anterior?.Cancel();
            return nueva.Token;
        }

        private void CancelarOrden(Unidad unidad)
        {
            if (_ordenes.TryRemove(unidad.Id, out var orden)) orden.Cancel();
        }

        private ResultadoAccion Rechazar(Jugador jugador, string motivo) => ResultadoAccion.Fallo(motivo);

        private ResultadoAccion? ValidarPartida()
        {
            if (!_partida.EnCurso) return ResultadoAccion.Fallo("La partida no está en curso.");
            return null;
        }

        // --- Construccion ---

        public ResultadoAccion Construir(Jugador jugador, TipoEdificio tipo, Posicion pos)
        {
            var invalida = ValidarPartida();
            if (invalida.HasValue) return invalida.Value;
            if (!jugador.Mapa.EstaDentroDelMapa(pos))
                return Rechazar(jugador, $"Las coordenadas {pos} están fuera del mapa.");
            if (!jugador.Mapa.EstaLibre(pos))
                return Rechazar(jugador, $"La casilla {pos} está ocupada.");

            var costo = ReglasJuego.CostoEdificio(tipo);
            if (!jugador.Recursos.IntentarConsumir(costo))
                return Rechazar(jugador, $"Recursos insuficientes: {ReglasJuego.Nombre(tipo)} cuesta {ReglasJuego.CostoComoTexto(costo)}.");

            var edificio = new Edificio(jugador.NuevoId("ed"), tipo, pos, jugador.Id, civ: jugador.Civilizacion);
            if (!jugador.Mapa.ColocarEdificio(edificio))
            {
                jugador.Recursos.Devolver(costo);  // otro hilo ocupo la casilla justo antes
                return Rechazar(jugador, $"La casilla {pos} acaba de ocuparse. Se devolvió el costo.");
            }
            jugador.AgregarEdificio(edificio);
            _partida.Notificar(TipoEvento.ConstruccionIniciada, jugador,
                $"{ReglasJuego.Nombre(tipo)} en construcción en {pos}", pos, idEntidad: edificio.Id);

            Lanzar("Construcción " + edificio.Id, async token =>
            {
                const int pasos = 20;
                for (int i = 1; i <= pasos; i++)
                {
                    await Task.Delay(edificio.TiempoConstruccionMs / pasos, token);
                    if (!edificio.EstaVivo()) return;  // lo destruyeron mientras se construia
                    edificio.ProgresoConstruccion = i * 100 / pasos;
                }
                edificio.Estado = EstadoConstruccion.Completado;
                jugador.Estadisticas.SumarEdificioConstruido();
                _partida.Notificar(TipoEvento.ConstruccionCompletada, jugador,
                    $"{ReglasJuego.Nombre(tipo)} terminado en {pos}", pos, idEntidad: edificio.Id);
            }, _cts.Token);

            return ResultadoAccion.Ok($"Construyendo {ReglasJuego.Nombre(tipo)} en {pos}.");
        }

        // --- Entrenamiento ---

        public ResultadoAccion Entrenar(Jugador jugador, TipoUnidad tipo)
        {
            var invalida = ValidarPartida();
            if (invalida.HasValue) return invalida.Value;

            var tipoEdificio = ReglasJuego.EdificioEntrenador(tipo);
            string nombreUnidad = ReglasJuego.Nombre(tipo, jugador.Civilizacion);
            var candidatos = jugador.Edificios.Where(e => e.Tipo == tipoEdificio && e.EstaOperativo()).ToList();
            if (candidatos.Count == 0)
                return Rechazar(jugador, $"Necesitas un {ReglasJuego.Nombre(tipoEdificio)} terminado para entrenar {nombreUnidad}.");

            var edificio = candidatos.FirstOrDefault(e => e.IntentarIniciarEntrenamiento(tipo));
            if (edificio == null)
                return Rechazar(jugador, $"Tus {ReglasJuego.Nombre(tipoEdificio)} ya están entrenando. Espera a que terminen.");

            if (!jugador.IntentarReservarPoblacion())
            {
                edificio.TerminarEntrenamiento();
                return Rechazar(jugador, $"Población máxima alcanzada ({jugador.Poblacion}/{jugador.PoblacionMaxima}). Construye una Casa.");
            }

            var costo = ReglasJuego.CostoUnidad(tipo);
            if (!jugador.Recursos.IntentarConsumir(costo))
            {
                edificio.TerminarEntrenamiento();
                jugador.LiberarReservaPoblacion();
                return Rechazar(jugador, $"Recursos insuficientes: {nombreUnidad} cuesta {ReglasJuego.CostoComoTexto(costo)}.");
            }

            _partida.Notificar(TipoEvento.EntrenamientoIniciado, jugador,
                $"Entrenando {nombreUnidad} en {ReglasJuego.Nombre(tipoEdificio)} {edificio.Posicion}", edificio.Posicion);

            int tiempoMs = ReglasJuego.Estadisticas(tipo).TiempoEntrenamientoMs;
            Lanzar("Entrenamiento " + tipo, async token =>
            {
                try
                {
                    const int pasos = 10;
                    for (int i = 1; i <= pasos; i++)
                    {
                        await Task.Delay(tiempoMs / pasos, token);
                        if (!edificio.EstaVivo()) return;
                        edificio.ProgresoEntrenamiento = i * 100 / pasos;
                    }

                    // Buscar donde aparece la unidad; si todo esta lleno, reintenta cada segundo.
                    var unidad = new Unidad(jugador.NuevoId(tipo.ToString().ToLower()), tipo, edificio.Posicion, jugador.Id, jugador.Civilizacion);
                    while (true)
                    {
                        var salida = jugador.Mapa.BuscarCasillaLibreAdyacente(edificio.Posicion, 4);
                        if (salida.HasValue && jugador.Mapa.ColocarUnidad(unidad, salida.Value)) break;
                        await Task.Delay(1000, token);
                        if (!edificio.EstaVivo()) return;
                    }

                    jugador.AgregarUnidad(unidad);
                    jugador.Estadisticas.SumarUnidadEntrenada();
                    _partida.Notificar(TipoEvento.EntrenamientoCompletado, jugador,
                        $"{nombreUnidad} listo en {unidad.Posicion}", unidad.Posicion, idEntidad: unidad.Id);
                }
                finally
                {
                    edificio.TerminarEntrenamiento();
                    jugador.LiberarReservaPoblacion();
                }
            }, _cts.Token);

            return ResultadoAccion.Ok($"Entrenando {nombreUnidad}...");
        }

        // --- Movimiento ---

        public ResultadoAccion Mover(Jugador jugador, Unidad unidad, Posicion destino)
        {
            var invalida = ValidarPartida();
            if (invalida.HasValue) return invalida.Value;
            if (unidad == null || unidad.DueñoId != jugador.Id || !unidad.EstaVivo())
                return Rechazar(jugador, "Esa unidad no está disponible.");
            if (!jugador.Mapa.EstaDentroDelMapa(destino))
                return Rechazar(jugador, $"El destino {destino} está fuera del mapa.");
            if (unidad.Posicion.Equals(destino))
                return Rechazar(jugador, "La unidad ya está en esa casilla.");
            if (!jugador.Mapa.EstaLibre(destino))
                return Rechazar(jugador, $"La casilla {destino} está ocupada.");
            if (jugador.Mapa.BuscarRuta(unidad.Posicion, destino) == null)
                return Rechazar(jugador, $"No hay camino libre hasta {destino}.");

            var token = NuevaOrden(unidad);
            unidad.Estado = EstadoUnidad.Moviendose;
            unidad.DescripcionEstado = $"Moviéndose a {destino}";
            _partida.Notificar(TipoEvento.OrdenMovimiento, jugador,
                $"{unidad.Nombre} se mueve de {unidad.Posicion} a {destino}", destino, idEntidad: unidad.Id);

            Lanzar("Movimiento " + unidad.Id, async t =>
            {
                bool llego = await Caminar(jugador, unidad, () => jugador.Mapa.BuscarRuta(unidad.Posicion, destino), t);
                if (!t.IsCancellationRequested)
                {
                    unidad.Estado = EstadoUnidad.Inactiva;
                    unidad.DescripcionEstado = llego ? "Inactiva" : "Inactiva (camino bloqueado)";
                }
            }, token);

            return ResultadoAccion.Ok($"{unidad.Nombre} en camino a {destino}.");
        }

        /// <summary>Camina la ruta paso a paso; si se bloquea, la recalcula (max. 5 veces).</summary>
        private async Task<bool> Caminar(Jugador jugador, Unidad unidad, Func<List<Posicion>> calcularRuta, CancellationToken token)
        {
            int reintentos = 0;
            var ruta = calcularRuta();
            while (ruta != null)
            {
                bool bloqueado = false;
                foreach (var paso in ruta)
                {
                    await Task.Delay(unidad.MsPorCasilla, token);
                    if (!unidad.EstaVivo() || !_partida.EnCurso) return false;
                    if (!jugador.Mapa.MoverUnidad(unidad, paso))
                    {
                        bloqueado = true;
                        break;
                    }
                }
                if (!bloqueado) return true;
                if (++reintentos > 5) return false;
                await Task.Delay(300, token);
                ruta = calcularRuta();
            }
            return false;
        }

        // --- Recoleccion ---

        public ResultadoAccion Recolectar(Jugador jugador, Unidad aldeano, Posicion posRecurso)
        {
            var invalida = ValidarPartida();
            if (invalida.HasValue) return invalida.Value;
            if (aldeano == null || aldeano.DueñoId != jugador.Id || !aldeano.EstaVivo())
                return Rechazar(jugador, "Esa unidad no está disponible.");
            if (aldeano.Tipo != TipoUnidad.Aldeano)
                return Rechazar(jugador, "Solo los aldeanos pueden recolectar recursos.");

            var tipo = jugador.Mapa.RecursoEn(posRecurso);
            if (!tipo.HasValue)
                return Rechazar(jugador, $"No hay recursos en {posRecurso}.");
            if (jugador.Mapa.BuscarRutaHastaAdyacente(aldeano.Posicion, posRecurso) == null)
                return Rechazar(jugador, "El aldeano no puede llegar a ese recurso (camino bloqueado).");

            var token = NuevaOrden(aldeano);
            aldeano.Estado = EstadoUnidad.Recolectando;
            aldeano.DescripcionEstado = $"Yendo por {ReglasJuego.Nombre(tipo.Value).ToLower()}";
            _partida.Notificar(TipoEvento.OrdenRecoleccion, jugador,
                $"Aldeano enviado a recolectar {ReglasJuego.Nombre(tipo.Value).ToLower()} en {posRecurso}", posRecurso, idEntidad: aldeano.Id);

            Lanzar("Recolección " + aldeano.Id, async t =>
            {
                var objetivo = posRecurso;
                var tipoActual = tipo.Value;
                while (!t.IsCancellationRequested && aldeano.EstaVivo() && _partida.EnCurso)
                {
                    aldeano.DescripcionEstado = $"Yendo por {ReglasJuego.Nombre(tipoActual).ToLower()}";
                    var destino = objetivo;
                    bool llego = await Caminar(jugador, aldeano, () => jugador.Mapa.BuscarRutaHastaAdyacente(aldeano.Posicion, destino), t);
                    if (!llego) break;

                    aldeano.DescripcionEstado = $"Recolectando {ReglasJuego.Nombre(tipoActual).ToLower()}";
                    while (true)
                    {
                        await Task.Delay(ReglasJuego.IntervaloRecoleccionMs, t);
                        if (!aldeano.EstaVivo() || !_partida.EnCurso) return;

                        int extraido = jugador.Mapa.ExtraerRecurso(objetivo, ReglasJuego.CantidadPorCiclo, out var tipoExtraido);
                        if (extraido == 0) break;  // se agoto (quiza otro aldeano saco lo ultimo)

                        jugador.Recursos.Agregar(tipoExtraido, extraido);
                        jugador.Estadisticas.SumarRecolectado(extraido);
                        _partida.Notificar(TipoEvento.RecursoRecolectado, jugador,
                            $"+{extraido} {ReglasJuego.Nombre(tipoExtraido).ToLower()}", objetivo, valor: extraido, idEntidad: aldeano.Id);
                    }

                    // Recurso agotado: igual que en Age of Empires, busca otro del mismo tipo.
                    var siguiente = jugador.Mapa.BuscarRecursoMasCercano(aldeano.Posicion, tipoActual);
                    if (!siguiente.HasValue)
                    {
                        _partida.Notificar(TipoEvento.MensajeSistema, jugador,
                            $"Se agotó la {ReglasJuego.Nombre(tipoActual).ToLower()} y no queda más en el mapa.", objetivo);
                        break;
                    }
                    objetivo = siguiente.Value;
                }
                if (!t.IsCancellationRequested)
                {
                    aldeano.Estado = EstadoUnidad.Inactiva;
                    aldeano.DescripcionEstado = "Inactiva";
                }
            }, token);

            return ResultadoAccion.Ok($"Aldeano enviado por {ReglasJuego.Nombre(tipo.Value).ToLower()}.");
        }

        // --- Ataque ---

        /// <summary>Dispara a una casilla enemiga: el impacto es inmediato y la recarga corre en una Task.</summary>
        public ResultadoAccion Atacar(Jugador atacante, Unidad unidad, Posicion objetivo)
        {
            var invalida = ValidarPartida();
            if (invalida.HasValue) return invalida.Value;
            if (unidad == null || unidad.DueñoId != atacante.Id || !unidad.EstaVivo())
                return Rechazar(atacante, "Esa unidad no está disponible.");
            if (!unidad.EsMilitar())
                return Rechazar(atacante, "Los aldeanos no pueden atacar. Usa Infantes o Arqueros.");

            var defensor = _partida.ObtenerOponente(atacante);
            if (!defensor.Mapa.EstaDentroDelMapa(objetivo))
                return Rechazar(atacante, $"Las coordenadas {objetivo} están fuera del mapa enemigo.");
            if (!unidad.IntentarIniciarRecarga())
                return Rechazar(atacante, $"{unidad.Nombre} está recargando.");

            string resultado = ResolverDisparo(atacante, unidad, defensor, objetivo);

            int radio = ReglasJuego.RadioRevelacion(unidad.Tipo);
            for (int df = -radio; df <= radio; df++)
                for (int dc = -radio; dc <= radio; dc++)
                {
                    var p = new Posicion(objetivo.Fila + df, objetivo.Columna + dc);
                    if (defensor.Mapa.EstaDentroDelMapa(p)) atacante.Revelar(p);
                }

            Lanzar("Recarga " + unidad.Id, async t =>
            {
                try { await Task.Delay((int)(unidad.RecargaMs * atacante.ModificadorRecarga), t); }
                finally { unidad.TerminarRecarga(); }
            }, _cts.Token);

            _partida.VerificarGanador();
            return ResultadoAccion.Ok(resultado);
        }

        private string ResolverDisparo(Jugador atacante, Unidad unidad, Jugador defensor, Posicion objetivo)
        {
            var (victima, edificio) = defensor.Mapa.ObtenerOcupante(objetivo);
            string texto;
            bool impacto = true;
            int daño = 0;

            if (victima != null)
            {
                daño = unidad.Daño;
                string nombre = victima.Nombre;
                if (victima.RecibirDaño(daño))
                {
                    defensor.Mapa.QuitarUnidad(victima);
                    defensor.QuitarUnidad(victima.Id);
                    CancelarOrden(victima);
                    atacante.Estadisticas.SumarUnidadEnemigaDestruida();
                    texto = $"Impacto - {nombre} enemigo destruido";
                    _partida.Notificar(TipoEvento.UnidadDestruida, defensor, $"{nombre} destruido en {objetivo}", objetivo, idEntidad: victima.Id);
                }
                else
                {
                    texto = $"Impacto - {nombre} enemigo herido (-{daño}, vida {victima.VidaActual}/{victima.VidaMaxima})";
                }
            }
            else if (edificio != null)
            {
                daño = unidad.DañoEdificios;
                string nombre = ReglasJuego.Nombre(edificio.Tipo);
                if (edificio.RecibirDaño(daño))
                {
                    defensor.Mapa.QuitarEdificio(edificio);
                    defensor.QuitarEdificio(edificio.Id);
                    atacante.Estadisticas.SumarEdificioEnemigoDestruido();
                    texto = $"Impacto - {nombre} enemigo destruido";
                    _partida.Notificar(TipoEvento.EdificioDestruido, defensor, $"{nombre} destruido en {objetivo}", objetivo, idEntidad: edificio.Id);
                }
                else
                {
                    texto = $"Impacto - {nombre} enemigo dañado (-{daño}, vida {edificio.VidaActual}/{edificio.VidaMaxima})";
                }
            }
            else
            {
                impacto = false;
                texto = $"Fallo - No había nada en {objetivo}";
            }

            defensor.Mapa.MarcarDisparo(objetivo, impacto);
            atacante.Estadisticas.SumarDisparo(impacto);
            _partida.Notificar(TipoEvento.Ataque, atacante,
                $"{unidad.Nombre} dispara a {objetivo}: {texto}", objetivo, impacto, daño, unidad.Id);
            return texto;
        }
    }
}
