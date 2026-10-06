using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.Missions
{
    /// <summary>Dónde está el auto respecto a las calles del grafo.</summary>
    public enum LaneVerdict { OnRoad, WrongWay, OffRoad }

    /// <summary>
    /// El juez de carril, PURO (testeado en EditMode). El grafo vial es
    /// dirigido y con nodos POR CARRIL, así que las aristas son la verdad de
    /// por dónde y en qué sentido se circula: lejos de toda arista = fuera de
    /// la vía; pegado a una arista pero avanzando CONTRA su sentido (y sin una
    /// arista propia igual de cerca) = contravía. Cruzar perpendicular (una
    /// intersección, un giro) es legal: solo se castiga la oposición franca.
    /// RoadDiscipline lo consulta cada frame con el rumbo real del auto.
    /// </summary>
    public static class LaneJudge
    {
        /// <summary>Más lejos que esto de cualquier carril = fuera de la vía (m).</summary>
        public const float OffRoadDistance = 8f;

        /// <summary>Aristas a menos de esto del carril más cercano compiten
        /// como "mi carril" (una intersección junta varias) (m).</summary>
        public const float LaneCluster = 2.5f;

        /// <summary>Avanzar con este alineamiento o menos es ir a contramano.</summary>
        public const float WrongWayDot = -0.35f;

        /// <summary>Con este alineamiento o más, se va con el sentido de la vía.</summary>
        public const float RightWayDot = 0.25f;

        /// <summary>
        /// Veredicto para una posición y un rumbo de avance. Con grafo vacío o
        /// rumbo nulo no juzga (OnRoad: en la duda, a favor del alumno).
        /// </summary>
        public static LaneVerdict Judge(RoadGraphData graph, Vector3 position, Vector3 moveDir)
        {
            if (graph == null || graph.Edges.Count == 0) return LaneVerdict.OnRoad;

            Vector3 f = moveDir; f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) return LaneVerdict.OnRoad;
            f.Normalize();

            // Distancia XZ a cada arista; el veredicto lo dan las del racimo
            // más cercano (mi calle), no una avenida paralela a media cuadra.
            // Dos pasadas sin asignar memoria: esto corre cada frame.
            float bestDist = float.MaxValue;
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                var e = graph.Edges[i];
                var a = graph.GetNode(e.FromId);
                var b = graph.GetNode(e.ToId);
                if (a == null || b == null) continue;

                float d = DistanceToSegmentXZ(position, a.Position, b.Position);
                if (d < bestDist) bestDist = d;
            }

            if (bestDist > OffRoadDistance) return LaneVerdict.OffRoad;

            float bestDot = -1f;
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                var e = graph.Edges[i];
                var a = graph.GetNode(e.FromId);
                var b = graph.GetNode(e.ToId);
                if (a == null || b == null) continue;
                if (DistanceToSegmentXZ(position, a.Position, b.Position) > bestDist + LaneCluster)
                    continue;

                Vector3 dir = b.Position - a.Position; dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) continue;
                float dot = Vector3.Dot(dir.normalized, f);
                if (dot > bestDot) bestDot = dot;
            }

            if (bestDot >= RightWayDot) return LaneVerdict.OnRoad;
            return bestDot <= WrongWayDot ? LaneVerdict.WrongWay
                                          : LaneVerdict.OnRoad; // cruce/giro: legal
        }

        /// <summary>
        /// El punto de vía más cercano y el sentido de su carril (para devolver
        /// al auto a la calle tras salirse). false solo sin aristas válidas.
        /// </summary>
        public static bool NearestLanePoint(RoadGraphData graph, Vector3 position,
                                            out Vector3 point, out Vector3 direction)
        {
            point = position; direction = Vector3.forward;
            if (graph == null) return false;

            bool found = false;
            float bestDist = float.MaxValue;
            foreach (var e in graph.Edges)
            {
                var a = graph.GetNode(e.FromId);
                var b = graph.GetNode(e.ToId);
                if (a == null || b == null) continue;

                Vector3 p = ClosestOnSegmentXZ(position, a.Position, b.Position);
                float d = DistXZ(position, p);
                if (d >= bestDist) continue;

                Vector3 dir = b.Position - a.Position; dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) continue;

                bestDist = d;
                point = p;
                direction = dir.normalized;
                found = true;
            }
            return found;
        }

        // ---------------- Geometría plana ----------------

        private static float DistanceToSegmentXZ(Vector3 p, Vector3 a, Vector3 b) =>
            DistXZ(p, ClosestOnSegmentXZ(p, a, b));

        private static Vector3 ClosestOnSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; ab.y = 0f;
            Vector3 ap = p - a; ap.y = 0f;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(ap, ab) / len2);
            return a + (b - a) * t;
        }

        private static float DistXZ(Vector3 p, Vector3 q)
        {
            float dx = p.x - q.x, dz = p.z - q.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
