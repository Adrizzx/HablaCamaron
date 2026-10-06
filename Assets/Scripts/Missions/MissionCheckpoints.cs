using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// Checkpoints invisibles que ARMAN la meta (playtest: la meta del nivel 3
    /// se podía tocar desde el arranque). Lógica PURA: dónde sembrarlos sobre
    /// la ruta A*, cómo avanzan (solo en orden) y cuándo queda armada la meta.
    /// </summary>
    public static class MissionCheckpoints
    {
        public const float SeparacionMetros = 80f;
        public const int MinCheckpoints = 3;
        public const int MaxCheckpoints = 6;
        /// <summary>Radio del trigger: generoso, no dependemos de la puntería.</summary>
        public const float RadioMetros = 12f;

        /// <summary>
        /// Posiciones de los checkpoints repartidas a lo largo de la ruta, sin
        /// incluir el arranque ni la meta (esa ya es la baliza).
        /// </summary>
        public static List<Vector3> PickPositions(IReadOnlyList<Vector3> ruta)
        {
            var salida = new List<Vector3>();
            if (ruta == null || ruta.Count < 2) return salida;

            // Largo total y acumulado por punto.
            var acumulado = new float[ruta.Count];
            for (int i = 1; i < ruta.Count; i++)
                acumulado[i] = acumulado[i - 1] + Vector3.Distance(ruta[i - 1], ruta[i]);
            float largo = acumulado[ruta.Count - 1];
            if (largo < 1f) return salida;

            int cuantos = Mathf.Clamp(Mathf.RoundToInt(largo / SeparacionMetros),
                MinCheckpoints, MaxCheckpoints);

            for (int k = 1; k <= cuantos; k++)
            {
                float meta = largo * k / (cuantos + 1f); // repartidos, sin pisar la meta
                salida.Add(PuntoA(ruta, acumulado, meta));
            }
            return salida;
        }

        private static Vector3 PuntoA(IReadOnlyList<Vector3> ruta, float[] acumulado, float dist)
        {
            for (int i = 1; i < ruta.Count; i++)
            {
                if (acumulado[i] < dist) continue;
                float tramo = acumulado[i] - acumulado[i - 1];
                float t = tramo < 1e-4f ? 0f : (dist - acumulado[i - 1]) / tramo;
                return Vector3.Lerp(ruta[i - 1], ruta[i], t);
            }
            return ruta[ruta.Count - 1];
        }

        /// <summary>Tocar un checkpoint solo cuenta si es EL que sigue.</summary>
        public static int Advance(int pasados, int indiceTocado) =>
            indiceTocado == pasados ? pasados + 1 : pasados;

        public static bool GoalArmed(int pasados, int total) => pasados >= total;
    }
}
