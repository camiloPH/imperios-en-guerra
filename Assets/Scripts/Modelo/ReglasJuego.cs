using System.Collections.Generic;
using System.Linq;

namespace Modelo
{
    public enum Dificultad { Facil, Normal, Dificil }

    /// <summary>Los dos bandos de la Guerra de Troya. El humano elige uno y la IA toma el otro.</summary>
    public enum Civilizacion { Grecia, Troya }

    /// <summary>Estadísticas base de un tipo de unidad.</summary>
    public readonly struct EstadisticasUnidad
    {
        public readonly int Vida;
        public readonly int Daño;
        public readonly int DañoEdificios;
        public readonly double VelocidadCasillasPorSeg;
        public readonly int RecargaMs;
        public readonly int TiempoEntrenamientoMs;

        public EstadisticasUnidad(int vida, int daño, int dañoEdificios, double velocidad, int recargaMs, int tiempoEntrenamientoMs)
        {
            Vida = vida;
            Daño = daño;
            DañoEdificios = dañoEdificios;
            VelocidadCasillasPorSeg = velocidad;
            RecargaMs = recargaMs;
            TiempoEntrenamientoMs = tiempoEntrenamientoMs;
        }
    }

    /// <summary>
    /// Todas las constantes de balance del juego en un único lugar: costos,
    /// vidas, tiempos y población. Cambiar el balance no obliga a tocar la
    /// lógica ni la interfaz.
    /// </summary>
    public static class ReglasJuego
    {
        public const int FilasMapa = 15;
        public const int ColumnasMapa = 15;

        public const int RecursoInicial = 200;
        public const int AldeanosIniciales = 3;
        public const int PoblacionTope = 25;

        public const int CantidadPorCiclo = 8;
        public const int IntervaloRecoleccionMs = 2000;

        /// <summary>Tiempo que una casilla enemiga queda visible tras recibir un disparo.</summary>
        public const int DuracionRevelacionMs = 10000;

        // ------------------------------------------------------------ edificios
        public static Dictionary<TipoRecurso, int> CostoEdificio(TipoEdificio tipo)
        {
            switch (tipo)
            {
                case TipoEdificio.CentroUrbano:
                    return new Dictionary<TipoRecurso, int> { { TipoRecurso.Madera, 275 }, { TipoRecurso.Oro, 100 } };
                case TipoEdificio.Cuartel:
                    return new Dictionary<TipoRecurso, int> { { TipoRecurso.Madera, 150 } };
                case TipoEdificio.Casa:
                    return new Dictionary<TipoRecurso, int> { { TipoRecurso.Madera, 50 } };
                default:
                    return new Dictionary<TipoRecurso, int>();
            }
        }

        /// <summary>Bonificación de Troya: sus murallas dan +20 % de vida a todos sus edificios.</summary>
        public static int VidaEdificio(TipoEdificio tipo, Civilizacion civ) =>
            civ == Civilizacion.Troya ? VidaEdificio(tipo) * 6 / 5 : VidaEdificio(tipo);

        public static int VidaEdificio(TipoEdificio tipo)
        {
            switch (tipo)
            {
                case TipoEdificio.CentroUrbano: return 600;
                case TipoEdificio.Cuartel: return 350;
                default: return 200;
            }
        }

        public static int TiempoConstruccionMs(TipoEdificio tipo)
        {
            switch (tipo)
            {
                case TipoEdificio.CentroUrbano: return 20000;
                case TipoEdificio.Cuartel: return 12000;
                default: return 7000;
            }
        }

        public static int PoblacionQueAporta(TipoEdificio tipo)
        {
            switch (tipo)
            {
                case TipoEdificio.CentroUrbano: return 5;
                case TipoEdificio.Casa: return 5;
                default: return 0;
            }
        }

        // ------------------------------------------------------------ unidades
        public static Dictionary<TipoRecurso, int> CostoUnidad(TipoUnidad tipo)
        {
            switch (tipo)
            {
                case TipoUnidad.Aldeano:
                    return new Dictionary<TipoRecurso, int> { { TipoRecurso.Comida, 50 } };
                case TipoUnidad.Infante:
                    return new Dictionary<TipoRecurso, int> { { TipoRecurso.Comida, 60 }, { TipoRecurso.Oro, 20 } };
                case TipoUnidad.Arquero:
                    return new Dictionary<TipoRecurso, int> { { TipoRecurso.Madera, 40 }, { TipoRecurso.Oro, 40 } };
                default:
                    return new Dictionary<TipoRecurso, int>();
            }
        }

        /// <summary>Bonificación de Grecia: sus Hoplitas (infantería) tienen +15 de vida.</summary>
        public static EstadisticasUnidad Estadisticas(TipoUnidad tipo, Civilizacion civ)
        {
            var e = Estadisticas(tipo);
            if (civ == Civilizacion.Grecia && tipo == TipoUnidad.Infante)
                return new EstadisticasUnidad(e.Vida + 15, e.Daño, e.DañoEdificios, e.VelocidadCasillasPorSeg, e.RecargaMs, e.TiempoEntrenamientoMs);
            return e;
        }

        /// <summary>
        /// Multiplicador del tiempo de recarga de las tropas de la IA según la
        /// dificultad (en Fácil disparan más despacio).
        /// </summary>
        public static double ModificadorRecargaIA(Dificultad dificultad)
        {
            switch (dificultad)
            {
                case Dificultad.Facil: return 1.5;
                case Dificultad.Normal: return 1.15;
                default: return 1.0;
            }
        }

        public static EstadisticasUnidad Estadisticas(TipoUnidad tipo)
        {
            switch (tipo)
            {
                case TipoUnidad.Aldeano: return new EstadisticasUnidad(30, 0, 0, 1.6, 0, 5000);
                case TipoUnidad.Infante: return new EstadisticasUnidad(70, 12, 20, 1.2, 2500, 7000);
                case TipoUnidad.Arquero: return new EstadisticasUnidad(45, 10, 6, 1.4, 1800, 8000);
                default: return new EstadisticasUnidad(10, 1, 1, 1.0, 1000, 1000);
            }
        }

        /// <summary>Qué edificio (terminado) se necesita para entrenar cada unidad.</summary>
        public static TipoEdificio EdificioEntrenador(TipoUnidad tipo) =>
            tipo == TipoUnidad.Aldeano ? TipoEdificio.CentroUrbano : TipoEdificio.Cuartel;

        /// <summary>El arquero revela un área de 3x3 alrededor del disparo; el infante solo la casilla.</summary>
        public static int RadioRevelacion(TipoUnidad tipo) => tipo == TipoUnidad.Arquero ? 1 : 0;

        // ------------------------------------------------------------ textos
        public static string Nombre(TipoEdificio tipo)
        {
            switch (tipo)
            {
                case TipoEdificio.CentroUrbano: return "Centro Urbano";
                case TipoEdificio.Cuartel: return "Cuartel";
                default: return "Casa";
            }
        }

        public static string Nombre(TipoUnidad tipo)
        {
            switch (tipo)
            {
                case TipoUnidad.Aldeano: return "Aldeano";
                case TipoUnidad.Infante: return "Infante";
                default: return "Arquero";
            }
        }

        /// <summary>Nombre de la unidad según el bando (Hoplita griego, Lancero troyano...).</summary>
        public static string Nombre(TipoUnidad tipo, Civilizacion civ)
        {
            switch (tipo)
            {
                case TipoUnidad.Aldeano: return "Aldeano";
                case TipoUnidad.Infante: return civ == Civilizacion.Grecia ? "Hoplita" : "Lancero troyano";
                default: return civ == Civilizacion.Grecia ? "Arquero cretense" : "Arquero troyano";
            }
        }

        public static string Nombre(Civilizacion civ) => civ == Civilizacion.Grecia ? "Grecia" : "Troya";

        public static string Bonificacion(Civilizacion civ) =>
            civ == Civilizacion.Grecia ? "Hoplitas con +15 de vida" : "Murallas: edificios con +20 % de vida";

        public static Civilizacion Opuesta(Civilizacion civ) =>
            civ == Civilizacion.Grecia ? Civilizacion.Troya : Civilizacion.Grecia;

        public static string Nombre(TipoRecurso tipo)
        {
            switch (tipo)
            {
                case TipoRecurso.Oro: return "Oro";
                case TipoRecurso.Madera: return "Madera";
                default: return "Comida";
            }
        }

        public static string Nombre(Dificultad dificultad)
        {
            switch (dificultad)
            {
                case Dificultad.Facil: return "Fácil";
                case Dificultad.Dificil: return "Difícil";
                default: return "Normal";
            }
        }

        public static string CostoComoTexto(Dictionary<TipoRecurso, int> costo) =>
            string.Join(", ", costo.Select(par => $"{par.Value} {Nombre(par.Key).ToLower()}"));
    }
}
