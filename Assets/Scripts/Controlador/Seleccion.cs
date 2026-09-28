using System.Collections.Generic;
using Modelo;

namespace Controlador
{
    public enum ModoAccion { Normal, Construir }

    /// <summary>Que tiene seleccionado el usuario y en que modo esta. La Vista solo lo lee.</summary>
    public class Seleccion
    {
        private readonly List<string> _unidades = new List<string>();

        public IReadOnlyList<string> IdsUnidades => _unidades;
        public string IdEdificio { get; private set; }
        public ModoAccion Modo { get; private set; } = ModoAccion.Normal;
        public TipoEdificio EdificioAConstruir { get; private set; }

        public bool TieneUnidades => _unidades.Count > 0;
        public bool EstaVacia => _unidades.Count == 0 && IdEdificio == null;

        public void SeleccionarUnidades(IEnumerable<string> ids)
        {
            _unidades.Clear();
            _unidades.AddRange(ids);
            IdEdificio = null;
        }

        public void SeleccionarEdificio(string id)
        {
            _unidades.Clear();
            IdEdificio = id;
        }

        public void ActivarConstruccion(TipoEdificio tipo)
        {
            Modo = ModoAccion.Construir;
            EdificioAConstruir = tipo;
        }

        public void SalirDeConstruccion() => Modo = ModoAccion.Normal;

        public void Limpiar()
        {
            _unidades.Clear();
            IdEdificio = null;
            Modo = ModoAccion.Normal;
        }

        /// <summary>Quita de la seleccion lo que ya no existe.</summary>
        public void Depurar(Jugador jugador)
        {
            _unidades.RemoveAll(id => jugador.BuscarUnidad(id) == null);
            if (IdEdificio != null && jugador.BuscarEdificio(IdEdificio) == null) IdEdificio = null;
        }
    }
}
