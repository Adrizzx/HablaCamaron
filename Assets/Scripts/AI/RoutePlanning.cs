using System.Collections.Generic;
using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.AI
{
    /// <summary>
    /// Elección PURA del nodo donde un NPC empieza una ruta (testeada en
    /// EditMode). NearestNode a secas era el bug de los buses que "cambiaban
    /// de dirección" en el redondel: al replanificar, el nodo más cercano
    /// podía quedar ATRÁS o en el CARRIL CONTRARIO, y el bus daba media
    /// vuelta en pleno anillo. Aquí se pondera cercanía + que el nodo quede
    /// adelante + que sus salidas apunten hacia donde el auto ya va.
    /// </summary>
    public static class RoutePlanning
    {
        /// <summary>Radio de búsqueda; sin candidatos dentro, manda el más cercano.</summary>
        public const float SearchRadius = 30f;

        /// <summary>Peso del castigo por nodo detrás del rumbo.</summary>
        public const float BehindWeight = 10f;

        /// <summary>Peso del castigo por salidas a contramano (carril contrario).</summary>
        public const float WrongLaneWeight = 14f;

        /// <summary>
        /// Nodo para EMPEZAR una ruta: cercano, adelante del rumbo y con
        /// aristas salientes alineadas. Con rumbo nulo (recién nacido) o sin
        /// candidatos en el radio, cae al nodo más cercano.
        /// </summary>
        public static RoadNode BestStartNode(RoadGraphData graph, Vector3 position, Vector3 forward)
        {
            if (graph == null || graph.Nodes.Count == 0) return null;

            Vector3 f = forward; f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) return graph.NearestNode(position);
            f.Normalize();

            RoadNode best = null;
            float bestScore = float.MaxValue;
            foreach (var n in graph.Nodes)
            {
                Vector3 to = n.Position - position; to.y = 0f;
                float dist = to.magnitude;
                if (dist > SearchRadius) continue;

                // ¿Queda adelante? (encima de él cuenta como adelante).
                float dirDot = dist > 0.5f ? Vector3.Dot(to / dist, f) : 1f;

                // ¿Sus salidas siguen mi rumbo o son el carril contrario?
                float edgeDot = -1f;
                foreach (int nb in graph.Neighbors(n.Id))
                {
                    var m = graph.GetNode(nb);
                    if (m == null) continue;
                    Vector3 e = m.Position - n.Position; e.y = 0f;
                    if (e.sqrMagnitude < 0.01f) continue;
                    edgeDot = Mathf.Max(edgeDot, Vector3.Dot(e.normalized, f));
                }

                float score = dist + (1f - dirDot) * BehindWeight
                                   + (1f - edgeDot) * WrongLaneWeight;
                if (score < bestScore) { bestScore = score; best = n; }
            }
            return best ?? graph.NearestNode(position);
        }

        /// <summary>Más de esto respecto al rumbo actual ya es media vuelta.</summary>
        public const float MaxFirstHopDeg = 130f;

        /// <summary>
        /// Solo se saltan waypoints que quedan atrás Y cerca; uno lejano se
        /// respeta aunque quede atrás — saltarlo sería cortar camino en línea
        /// recta ignorando la curvatura real de la calle (el tipo de atajo
        /// que el proyecto ya cazó como bug antes).
        /// </summary>
        public const float MaxSkipMeters = 25f;

        /// <summary>
        /// Índice del primer waypoint que NO obliga a un giro en U. Con rumbo
        /// nulo (auto recién nacido) manda el primero. Se saltan solo los
        /// waypoints atrás del rumbo (más de MaxFirstHopDeg) Y cerca (menos
        /// de MaxSkipMeters); en cuanto uno esté adelante O esté lejos, se
        /// devuelve. Si se agotan todos, devuelve path.Count: el NPC sigue
        /// por su calle y dará la vuelta a la manzana en el próximo cruce.
        /// </summary>
        public static int FirstUsefulWaypoint(IReadOnlyList<RoadNode> path,
            Vector3 position, Vector3 forward)
        {
            if (path == null || path.Count == 0) return 0;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return 0;
            forward.Normalize();

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 hacia = path[i].Position - position;
                hacia.y = 0f;
                float dist = hacia.magnitude;
                if (dist < 1e-6f) continue;
                bool atras = Vector3.Angle(forward, hacia / dist) > MaxFirstHopDeg;
                bool cerca = dist < MaxSkipMeters;
                if (!(atras && cerca)) return i;
            }
            return path.Count;
        }
    }
}
