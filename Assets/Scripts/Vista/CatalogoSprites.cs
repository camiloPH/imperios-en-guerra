using System.Collections.Generic;
using UnityEngine;
using Modelo;

namespace Vista
{
    /// <summary>Carga los sprites de Resources/Sprites una vez; si falta uno, usa un color de respaldo.</summary>
    public static class CatalogoSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Obtener(string nombre)
        {
            if (Cache.TryGetValue(nombre, out var s) && s != null) return s;

            var textura = Resources.Load<Texture2D>("Sprites/" + nombre);
            if (textura != null)
            {
                textura.filterMode = FilterMode.Bilinear;
                textura.wrapMode = TextureWrapMode.Clamp;
                var borde = EsNueveCortes(nombre) ? new Vector4(20, 20, 20, 20) * (textura.width / 64f) : Vector4.zero;
                s = Sprite.Create(textura, new Rect(0, 0, textura.width, textura.height), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, borde);
            }
            else
            {
                s = Resources.Load<Sprite>("Sprites/" + nombre) ?? CrearRespaldo();
                if (s.texture == null || s.name == "respaldo")
                    Debug.LogWarning($"[Vista] Falta el sprite 'Resources/Sprites/{nombre}'. Se usa un color de respaldo.");
            }
            s.name = nombre;
            Cache[nombre] = s;
            return s;
        }

        private static bool EsNueveCortes(string nombre) => nombre == "panel" || nombre == "boton";

        private static Sprite CrearRespaldo()
        {
            var tex = new Texture2D(4, 4);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.8f, 0.2f, 0.8f, 1f);
            tex.SetPixels(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            s.name = "respaldo";
            return s;
        }

        // ------------------------------------------------------------ atajos
        private static string Sufijo(Civilizacion civ) => civ == Civilizacion.Grecia ? "grecia" : "troya";

        public static Sprite DeUnidad(TipoUnidad tipo, Civilizacion civ)
        {
            string baseNombre = tipo == TipoUnidad.Aldeano ? "aldeano" : tipo == TipoUnidad.Infante ? "infante" : "arquero";
            return Obtener($"{baseNombre}_{Sufijo(civ)}");
        }

        public static Sprite DeEdificio(TipoEdificio tipo, Civilizacion civ)
        {
            string baseNombre = tipo == TipoEdificio.CentroUrbano ? "centro_urbano" : tipo == TipoEdificio.Cuartel ? "cuartel" : "casa";
            return Obtener($"{baseNombre}_{Sufijo(civ)}");
        }

        /// <summary>Casco corintio (Grecia) o caballo de Troya (Troya).</summary>
        public static Sprite Emblema(Civilizacion civ) => Obtener("emblema_" + Sufijo(civ));

        public static Sprite DeRecurso(TipoRecurso tipo)
        {
            switch (tipo)
            {
                case TipoRecurso.Oro: return Obtener("mina_oro");
                case TipoRecurso.Madera: return Obtener("arbol");
                default: return Obtener("bayas");
            }
        }

        public static Sprite IconoRecurso(TipoRecurso tipo)
        {
            switch (tipo)
            {
                case TipoRecurso.Oro: return Obtener("icono_oro");
                case TipoRecurso.Madera: return Obtener("icono_madera");
                default: return Obtener("icono_comida");
            }
        }

        public static Sprite Pasto(int variante) => Obtener("pasto_" + (1 + Mathf.Abs(variante) % 3));
    }
}
