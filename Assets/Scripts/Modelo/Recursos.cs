using System;
using System.Collections.Generic;

namespace Modelo
{
    /// <summary>
    /// Recursos (oro, madera, comida) de un jugador. Varios hilos leen y
    /// escriben aquí a la vez: varios aldeanos recolectando, una construcción
    /// y un entrenamiento consumiendo costo al mismo tiempo. Por eso todo
    /// acceso pasa por lock(_candado).
    /// </summary>
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

        /// <summary>
        /// Intenta descontar un costo compuesto (por ejemplo madera+oro para un
        /// edificio) de forma atómica: o se descuenta todo, o no se descuenta
        /// nada. Evita que dos hilos "gasten" el mismo oro al mismo tiempo.
        /// </summary>
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

        /// <summary>Devuelve un costo ya descontado (por ejemplo, si la casilla se ocupó antes de construir).</summary>
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
