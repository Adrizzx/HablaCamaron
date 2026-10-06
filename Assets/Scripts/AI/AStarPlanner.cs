using System.Collections.Generic;
using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.AI
{
    /// <summary>
    /// A* sobre el RoadGraph: la NAVEGACIÓN de los NPCs (qué ruta tomar).
    /// Implementación clásica de libro — costo real g + heurística euclidiana h
    /// (admisible: la línea recta nunca sobreestima la distancia por calles).
    /// Estático y puro: se prueba en EditMode con grafos conocidos.
    /// </summary>
    public static class AStarPlanner
    {
        /// <summary>
        /// Ruta óptima de startId a goalId como lista de nodos (incluye ambos
        /// extremos). Devuelve null si no existe camino respetando los sentidos.
        /// </summary>
        public static List<RoadNode> FindPath(RoadGraphData graph, int startId, int goalId)
        {
            var start = graph.GetNode(startId);
            var goal = graph.GetNode(goalId);
            if (start == null || goal == null) return null;
            if (startId == goalId) return new List<RoadNode> { start };

            var open = new MinHeap();                          // frontera O(log n)
            open.Push(startId, H(start, goal));
            var cameFrom = new Dictionary<int, int>();
            var gScore = new Dictionary<int, float> { [startId] = 0f };
            var closed = new HashSet<int>();

            while (open.Count > 0)
            {
                int current = open.Pop();
                if (current == goalId) return Reconstruct(graph, cameFrom, current);
                if (!closed.Add(current)) continue;            // entrada obsoleta del heap

                float gCur = gScore[current];
                foreach (var (next, cost) in graph.NeighborEdges(current))
                {
                    if (closed.Contains(next)) continue;

                    float tentative = gCur + cost;             // costo O(1)
                    if (gScore.TryGetValue(next, out float known) && tentative >= known)
                        continue;

                    cameFrom[next] = current;
                    gScore[next] = tentative;
                    // "decrease-key" perezoso: se empuja de nuevo y la entrada
                    // vieja se descarta al hacer Pop (por el closed.Add de arriba).
                    open.Push(next, tentative + H(graph.GetNode(next), goal));
                }
            }
            return null; // sin camino (respetando el sentido de las vías)
        }

        private static float H(RoadNode a, RoadNode b) =>
            Vector3.Distance(a.Position, b.Position);

        private static List<RoadNode> Reconstruct(RoadGraphData g,
            Dictionary<int, int> cameFrom, int current)
        {
            var path = new List<RoadNode> { g.GetNode(current) };
            while (cameFrom.TryGetValue(current, out int prev))
            {
                current = prev;
                path.Insert(0, g.GetNode(current));
            }
            return path;
        }
    }
}
