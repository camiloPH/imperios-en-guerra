using System;
using System.Collections.Generic;

namespace Modelo
{
    /// <summary>Oro, madera y comida de un jugador. Todo acceso pasa por lock.</summary>
    public class Recursos
    {
        private readonly Dictionary<TipoRecurso, int> _cantidades;
        private readonly object _candado = new object();

        public Recursos(int oroInicial = 200, int maderaInicial = 200, int comidaInicial = 200)
        {
            _cantidades = new Dictionary<TipoRecurso, int>
            {
                { TipoRecurso.Oro, oroInicial },
                { TipoRecurso.Madera, maderaInicial },
                { TipoRecurso.Comida, comidaInicial }
            };
        }

        public int Obtener(TipoRecurso tipo)
        {
            lock (_candado)
            {
                return _cantidades[tipo];
            }
        }

        public void Agregar(TipoRecurso tipo, int cantidad)
        {
            if (cantidad < 0) throw new ArgumentException("La cantidad a agregar no puede ser negativa.");
            lock (_candado)
            {
                _cantidades[tipo] += cantidad;
            }
        }

        /// <summary>Cobra un costo completo o nada (atomico): evita gastar dos veces el mismo recurso.</summary>
        public bool IntentarConsumir(Dictionary<TipoRecurso, int> costo)
        {
            lock (_candado)
            {
                foreach (var par in costo)
                {
                    if (_cantidades[par.Key] < par.Value) return false;
                }
                foreach (var par in costo)
                {
                    _cantidades[par.Key] -= par.Value;
                }
                return true;
            }
        }

        /// <summary>Devuelve un costo ya cobrado.</summary>
        public void Devolver(Dictionary<TipoRecurso, int> costo)
        {
            lock (_candado)
            {
                foreach (var par in costo) _cantidades[par.Key] += par.Value;
            }
        }

        /// <summary>Solo consulta si alcanza, sin descontar nada.</summary>
        public bool AlcanzaPara(Dictionary<TipoRecurso, int> costo)
        {
            lock (_candado)
            {
                foreach (var par in costo)
                    if (_cantidades[par.Key] < par.Value) return false;
                return true;
            }
        }

        public Dictionary<TipoRecurso, int> Snapshot()
        {
            lock (_candado)
            {
                return new Dictionary<TipoRecurso, int>(_cantidades);
            }
        }
    }
}
