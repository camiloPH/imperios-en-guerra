using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Modelo
{
    /// <summary>
    /// configuracion.txt, log_partida.txt y resultado_final.txt.
    /// El log usa productor-consumidor: BlockingCollection + hilo dedicado "Hilo-Log".
    /// </summary>
    public class GestorArchivos : IDisposable
    {
        public const string ArchivoConfiguracion = "configuracion.txt";
        public const string ArchivoLog = "log_partida.txt";
        public const string ArchivoResultado = "resultado_final.txt";

        public string Carpeta { get; }
        public string RutaConfiguracion => Path.Combine(Carpeta, ArchivoConfiguracion);
        public string RutaLog => Path.Combine(Carpeta, ArchivoLog);
        public string RutaResultado => Path.Combine(Carpeta, ArchivoResultado);

        private readonly BlockingCollection<string> _pendientes = new BlockingCollection<string>();
        private readonly Thread _hiloEscritor;
        private readonly object _candadoArchivo = new object();
        private volatile string _ultimoError;

        /// <summary>Ultimo error de E/S (null si todo va bien). La Vista lo puede mostrar.</summary>
        public string UltimoError => _ultimoError;

        public GestorArchivos(string carpeta)
        {
            Carpeta = string.IsNullOrWhiteSpace(carpeta) ? Directory.GetCurrentDirectory() : carpeta;
            try
            {
                Directory.CreateDirectory(Carpeta);
                File.WriteAllText(RutaLog,
                    $"=== IMPERIOS EN GUERRA - LOG DE PARTIDA ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ==={Environment.NewLine}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _ultimoError = $"No se pudo preparar la carpeta de archivos: {ex.Message}";
            }

            _hiloEscritor = new Thread(EscribirPendientes) { Name = "Hilo-Log", IsBackground = true };
            _hiloEscritor.Start();
        }

        // ------------------------------------------------------------ log (productor)

        /// <summary>Puede llamarse desde cualquier hilo. No toca el disco: solo encola.</summary>
        public void RegistrarEvento(EventoJuego evento)
        {
            if (evento == null || _pendientes.IsAddingCompleted) return;
            var sb = new StringBuilder();
            sb.AppendLine($"Turno: {evento.NombreJugador}");
            sb.AppendLine($"Acción: {evento.AccionLegible}");
            sb.AppendLine($"Resultado: {evento.Descripcion}");
            sb.AppendLine($"Hora: {evento.Momento:HH:mm:ss}");
            sb.AppendLine();
            try { _pendientes.Add(sb.ToString()); }
            catch (InvalidOperationException) { /* ya se cerró el log */ }
        }

        // ------------------------------------------------------------ log (consumidor)

        private void EscribirPendientes()
        {
            foreach (var bloque in _pendientes.GetConsumingEnumerable())
            {
                try
                {
                    lock (_candadoArchivo) File.AppendAllText(RutaLog, bloque, Encoding.UTF8);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    _ultimoError = $"Error escribiendo el log: {ex.Message}";
                }
            }
        }

        // ------------------------------------------------------------ configuracion y resultado

        public bool GuardarConfiguracionInicial(Partida partida)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== IMPERIOS EN GUERRA - CONFIGURACIÓN INICIAL ===");
            sb.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Modo: Jugador vs IA (dificultad {ReglasJuego.Nombre(partida.Dificultad)})");
            sb.AppendLine($"Ubicación del Centro Urbano del jugador: {(partida.UbicacionManual ? "manual (clic)" : "aleatoria")}");
            sb.AppendLine($"Tamaño de cada mapa: {partida.JugadorHumano.Mapa.Filas}x{partida.JugadorHumano.Mapa.Columnas}");
            sb.AppendLine($"Leyenda: {Mapa.LeyendaTexto}");
            sb.AppendLine();
            foreach (var jugador in new[] { partida.JugadorHumano, partida.JugadorIA })
                DescribirJugador(sb, jugador);
            return Escribir(RutaConfiguracion, sb.ToString());
        }

        public bool GuardarResultadoFinal(Partida partida)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== IMPERIOS EN GUERRA - RESULTADO FINAL ===");
            sb.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Ganador: {partida.Ganador?.Nombre ?? "(sin ganador: partida interrumpida)"}");
            sb.AppendLine($"Duración: {partida.Duracion:mm\\:ss}");
            sb.AppendLine($"Dificultad de la IA: {ReglasJuego.Nombre(partida.Dificultad)}");
            sb.AppendLine($"Leyenda: {Mapa.LeyendaTexto}");
            sb.AppendLine();
            foreach (var jugador in new[] { partida.JugadorHumano, partida.JugadorIA })
            {
                DescribirJugador(sb, jugador);
                var e = jugador.Estadisticas;
                sb.AppendLine("Estadísticas:");
                sb.AppendLine($"  Recursos recolectados: {e.RecursosRecolectados}");
                sb.AppendLine($"  Edificios construidos: {e.EdificiosConstruidos}");
                sb.AppendLine($"  Unidades entrenadas: {e.UnidadesEntrenadas}");
                sb.AppendLine($"  Disparos acertados/fallados: {e.DisparosAcertados}/{e.DisparosFallados}");
                sb.AppendLine($"  Unidades enemigas destruidas: {e.UnidadesEnemigasDestruidas}");
                sb.AppendLine($"  Edificios enemigos destruidos: {e.EdificiosEnemigosDestruidos}");
                sb.AppendLine();
            }
            return Escribir(RutaResultado, sb.ToString());
        }

        private static void DescribirJugador(StringBuilder sb, Jugador jugador)
        {
            sb.AppendLine($"--- {jugador.Nombre} ({(jugador.EsIA ? "IA" : "Humano")}) ---");
            sb.AppendLine($"Civilización: {ReglasJuego.Nombre(jugador.Civilizacion)} ({ReglasJuego.Bonificacion(jugador.Civilizacion)})");
            sb.AppendLine("Recursos: " + string.Join("  ",
                jugador.Recursos.Snapshot().Select(p => $"{ReglasJuego.Nombre(p.Key)}={p.Value}")));
            sb.AppendLine($"Población: {jugador.Poblacion}/{jugador.PoblacionMaxima}");
            sb.AppendLine("Edificios:");
            foreach (var e in jugador.Edificios.OrderBy(e => e.Tipo))
                sb.AppendLine($"  {ReglasJuego.Nombre(e.Tipo)} en {e.Posicion} - vida {e.VidaActual}/{e.VidaMaxima} - {e.Estado}");
            sb.AppendLine("Unidades:");
            foreach (var u in jugador.Unidades.OrderBy(u => u.Tipo))
                sb.AppendLine($"  {u.Nombre} en {u.Posicion} - vida {u.VidaActual}/{u.VidaMaxima}");
            sb.AppendLine("Mapa:");
            sb.Append(jugador.Mapa.ComoTexto());
            sb.AppendLine();
        }

        private bool Escribir(string ruta, string contenido)
        {
            try
            {
                lock (_candadoArchivo) File.WriteAllText(ruta, contenido, Encoding.UTF8);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _ultimoError = $"No se pudo escribir {Path.GetFileName(ruta)}: {ex.Message}";
                return false;
            }
        }

        /// <summary>Cierra el log: termina de escribir lo pendiente y finaliza el Hilo-Log.</summary>
        public void Dispose()
        {
            if (!_pendientes.IsAddingCompleted) _pendientes.CompleteAdding();
            if (_hiloEscritor.IsAlive) _hiloEscritor.Join(2000);
        }
    }
}
