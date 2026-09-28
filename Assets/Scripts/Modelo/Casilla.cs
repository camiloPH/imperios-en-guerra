using System;

namespace Modelo
{
    public enum TipoCasilla
    {
        Libre,
        RecursoNatural,
        Edificio,
        Unidad
    }

    /// <summary>Marca que deja un disparo sobre el mapa atacado.</summary>
    public enum MarcaCasilla { Ninguna, Fallo, Impacto }

    /// <summary>Celda del mapa. Solo Mapa la modifica, dentro de su lock.</summary>
    public class Casilla
    {
        public TipoCasilla Tipo { get; internal set; } = TipoCasilla.Libre;
        public TipoRecurso? RecursoNatural { get; internal set; }
        public int CantidadRecursoNatural { get; internal set; }
        public Edificio Edificio { get; internal set; }
        public Unidad Unidad { get; internal set; }
        public MarcaCasilla Marca { get; internal set; }
        public DateTime MomentoMarca { get; internal set; }

        public bool EstaLibre() => Tipo == TipoCasilla.Libre;

        internal void Vaciar()
        {
            Tipo = TipoCasilla.Libre;
            RecursoNatural = null;
            CantidadRecursoNatural = 0;
            Edificio = null;
            Unidad = null;
        }
    }

    /// <summary>Copia inmutable de una casilla para que la Vista dibuje sin leer datos a medio cambiar.</summary>
    public readonly struct InfoCasilla
    {
        public readonly Posicion Posicion;
        public readonly TipoCasilla Tipo;
        public readonly TipoRecurso? Recurso;
        public readonly int CantidadRecurso;

        public readonly string IdEdificio;
        public readonly TipoEdificio TipoEdificio;
        public readonly EstadoConstruccion EstadoEdificio;
        public readonly int VidaEdificio;
        public readonly int VidaMaximaEdificio;
        public readonly int ProgresoConstruccion;

        public readonly string IdUnidad;
        public readonly TipoUnidad TipoUnidad;

        public readonly MarcaCasilla Marca;
        public readonly double SegundosDesdeMarca;

        internal InfoCasilla(Posicion posicion, Casilla c, DateTime ahora)
        {
            Posicion = posicion;
            Tipo = c.Tipo;
            Recurso = c.RecursoNatural;
            CantidadRecurso = c.CantidadRecursoNatural;

            var e = c.Edificio;
            IdEdificio = e?.Id;
            TipoEdificio = e?.Tipo ?? TipoEdificio.Casa;
            EstadoEdificio = e?.Estado ?? EstadoConstruccion.Destruido;
            VidaEdificio = e?.VidaActual ?? 0;
            VidaMaximaEdificio = e?.VidaMaxima ?? 0;
            ProgresoConstruccion = e?.ProgresoConstruccion ?? 0;

            IdUnidad = c.Unidad?.Id;
            TipoUnidad = c.Unidad?.Tipo ?? TipoUnidad.Aldeano;

            Marca = c.Marca;
            SegundosDesdeMarca = c.Marca == MarcaCasilla.Ninguna ? double.MaxValue : (ahora - c.MomentoMarca).TotalSeconds;
        }

        public bool TieneEdificio => IdEdificio != null;
        public bool TieneUnidad => IdUnidad != null;
    }
}
