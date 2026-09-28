using System;
using System.Collections.Generic;

namespace Modelo
{
    /// <summary>
    /// Coordenada (fila, columna) dentro de la matriz del mapa. Es un struct
    /// inmutable: no depende de UnityEngine.Vector2, para que el Modelo se
    /// pueda compilar y probar sin el motor.
    /// </summary>
    public readonly struct Posicion : IEquatable<Posicion>
    {
        public int Fila { get; }
        public int Columna { get; }

        public Posicion(int fila, int columna)
        {
            Fila = fila;
            Columna = columna;
        }

        public bool Equals(Posicion otra) => Fila == otra.Fila && Columna == otra.Columna;
        public override bool Equals(object obj) => obj is Posicion p && Equals(p);
        public override int GetHashCode() => (Fila * 397) ^ Columna;
        public override string ToString() => $"({Fila},{Columna})";

        public static bool operator ==(Posicion a, Posicion b) => a.Equals(b);
        public static bool operator !=(Posicion a, Posicion b) => !a.Equals(b);

        public double DistanciaA(Posicion otra)
        {
            int df = Fila - otra.Fila;
            int dc = Columna - otra.Columna;
            return Math.Sqrt(df * df + dc * dc);
        }

        /// <summary>Distancia en "pasos de rey" (permite diagonales).</summary>
        public int DistanciaEnPasos(Posicion otra) =>
            Math.Max(Math.Abs(Fila - otra.Fila), Math.Abs(Columna - otra.Columna));

        public bool EsAdyacenteA(Posicion otra) => !Equals(otra) && DistanciaEnPasos(otra) == 1;

        /// <summary>Las 8 casillas vecinas (pueden quedar fuera del mapa: el Mapa lo valida).</summary>
        public IEnumerable<Posicion> Vecinas()
        {
            for (int df = -1; df <= 1; df++)
                for (int dc = -1; dc <= 1; dc++)
                    if (df != 0 || dc != 0)
                        yield return new Posicion(Fila + df, Columna + dc);
        }
    }
}
