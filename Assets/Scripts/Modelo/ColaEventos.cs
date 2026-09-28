using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Modelo
{
    /// <summary>
    /// Puente thread-safe entre el Modelo (hilos secundarios: recolección,
    /// construcción, entrenamiento, movimiento, IA) y el Controlador, que vive
    /// en el ÚNICO hilo principal de Unity.
    ///
    /// Los hilos del Modelo llaman a Encolar(...) desde cualquier hilo.
    /// El Controlador, dentro de Update() (hilo principal de Unity), llama a
    /// DesencolarTodos() en cada frame y traduce cada EventoJuego en una
    /// llamada a la Vista (por ejemplo, refrescar un contador de recursos o
    /// mover un sprite). Así nunca se toca la API de UnityEngine desde un
    /// hilo secundario.
    ///
    /// ConcurrentQueue ya es thread-safe internamente, así que no hace falta
    /// lock adicional aquí.
    /// </summary>
    public class ColaEventos
    {
        private readonly ConcurrentQueue<EventoJuego> _eventos = new ConcurrentQueue<EventoJuego>();

        public void Encolar(EventoJuego evento)
        {
            _eventos.Enqueue(evento);
        }

        public bool TryDesencolar(out EventoJuego evento)
        {
            return _eventos.TryDequeue(out evento);
        }

        /// <summary>
        /// Pensado para llamarse una vez por frame desde el Controlador
        /// (por ejemplo dentro de Update()). Devuelve todo lo acumulado
        /// desde la última llamada, en orden de llegada.
        /// </summary>
        public List<EventoJuego> DesencolarTodos()
        {
            var lista = new List<EventoJuego>();
            while (_eventos.TryDequeue(out var evento))
            {
                lista.Add(evento);
            }
            return lista;
        }
    }
}
