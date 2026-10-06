using System.Collections.Generic;
using UnityEngine;
using HablaCamaron.World;
using HablaCamaron.Vehicle;
using HablaCamaron.AI;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// Barre la ruta A* de una misión con el volumen del auto y reporta
    /// todo collider que invada la calzada (playtest nivel 4: "hay cosas
    /// en el camino que no dejan avanzar"). Invariante de builder: la
    /// ruta de cada ancla nace despejada.
    /// </summary>
    public static class RouteClearanceScan
    {
        /// <summary>Colliders intrusos sobre la ruta (excluye las piezas de
        /// vía y el piso: solo cuenta lo que BLOQUEA).</summary>
        public static List<Collider> Scan(RoadGraphData grafo,
            IReadOnlyList<int> ruta, Vector3 mediasCaja)
        {
            Physics.SyncTransforms();
            var intrusos = new List<Collider>();
            for (int i = 1; i < ruta.Count; i++)
            {
                var na = grafo.GetNode(ruta[i - 1]);
                var nb = grafo.GetNode(ruta[i]);
                if (na == null || nb == null) continue;
                var a = na.Position;
                var b = nb.Position;
                var dir = (b - a).normalized;
                if (dir.sqrMagnitude < 1e-6f) continue;
                var rot = Quaternion.LookRotation(dir, Vector3.up);
                float largo = Vector3.Distance(a, b);
                // Paso FINO (media caja): con el paso completo, un poste
                // estrecho podía caer justo entre dos muestras y el barrido no
                // lo veía — quedaban farolas en plena calzada (playtest).
                float paso = Mathf.Max(mediasCaja.z * 0.5f, 0.75f);
                for (float t = 0f; t <= largo; t += paso)
                {
                    var centro = a + dir * t + Vector3.up * (mediasCaja.y + 0.4f);
                    foreach (var c in Physics.OverlapBox(centro, mediasCaja, rot))
                    {
                        // El propio auto del jugador y los NPC no cuentan como "obstáculo fijo".
                        bool esAuto = c.GetComponentInParent<VehicleController>() != null
                                   || c.GetComponentInParent<NpcDriver>() != null;
                        if (esAuto || intrusos.Contains(c)) continue;
                        if (!EsSuelo(c)) intrusos.Add(c);
                    }
                }
            }
            return intrusos;
        }

        /// <summary>
        /// ¿Este collider es suelo (calzada, parche, acera o terreno)?
        ///
        /// OJO: hay que mirar la CADENA de padres buscando el nombre de una
        /// pieza de vía, no `transform.root.name`. En la ciudad Toon todos los
        /// props cuelgan de un contenedor llamado **"Roads"**, y como ese
        /// nombre contiene "Road", el criterio viejo daba por calzada las
        /// farolas, las señales y hasta los edificios: el invariante reportaba
        /// CERO obstáculos mientras el jugador chocaba contra un poste en mitad
        /// de la calle (playtest, con foto).
        /// </summary>
        private static bool EsSuelo(Collider c)
        {
            for (var t = c.transform; t != null; t = t.parent)
            {
                string n = t.name;
                if (n.StartsWith("Road_") || n.StartsWith("Highway_") ||
                    n.StartsWith("Roundabout") || n.StartsWith("Pavement") ||
                    n == "Parche_Costura" || n == "Calzada" || n == "Piso" ||
                    n == "Terrain" || n.StartsWith("Meseta") || n.StartsWith("Cuna") ||
                    n.StartsWith("Relleno") || n.StartsWith("RampaSuave"))
                    return true;
            }
            return false;
        }
    }
}
