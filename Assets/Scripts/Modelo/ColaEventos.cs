using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Modelo
{
    /// <summary>
    /// Buzon thread-safe: los hilos del Modelo encolan eventos y el Controlador
    /// los desencola en Update() (hilo principal). ConcurrentQueue no necesita lock.
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

        /// <summary>Saca todos los eventos pendientes, en orden de llegada (una vez por frame).</summary>
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
