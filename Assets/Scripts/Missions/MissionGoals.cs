using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// Selección de la META de una misión sobre el grafo de calles, como
    /// funciones PURAS (grafo + posición → nodo) para probarlas en EditMode.
    /// MissionRunner las usa según el GoalMode de la misión.
    /// </summary>
    public static class MissionGoals
    {
        /// <summary>El nodo más lejano al punto de partida (la cima de la cuesta,
        /// el final de la avenida...). Null si el grafo está vacío.</summary>
        public static RoadNode Farthest(RoadGraphData graph, Vector3 from)
        {
            if (graph == null || graph.Nodes.Count == 0) return null;
            RoadNode best = graph.Nodes[0];
            float bestSq = -1f;
            foreach (var n in graph.Nodes)
            {
                float sq = (n.Position - from).sqrMagnitude;
                if (sq > bestSq) { bestSq = sq; best = n; }
            }
            return best;
        }

        /// <summary>El nodo con semáforo más cercano al punto de partida (la
        /// entrada al redondel del barrio). Null si no hay semáforos.</summary>
        public static RoadNode NearestTrafficLight(RoadGraphData graph, Vector3 from)
        {
            if (graph == null) return null;
            RoadNode best = null;
            float bestSq = float.MaxValue;
            foreach (var n in graph.Nodes)
            {
                if (n.TrafficLightGroup < 0) continue;
                float sq = (n.Position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = n; }
            }
            return best;
        }

        /// <summary>
        /// Lógica de la meta "volver al inicio": la baliza se ARMA recién cuando
        /// el jugador se alejó más de armDistance (si no, la misión terminaría
        /// en el segundo cero). Una vez armada, queda armada.
        /// </summary>
        public static bool UpdateReturnArmed(bool armed, float distanceFromStart, float armDistance) =>
            armed || distanceFromStart > armDistance;

        /// <summary>Etiqueta del HUD para misiones de varias vueltas
        /// ("VUELTA 1/2"). PURA (testeada).</summary>
        public static string LapLabel(int lapsDone, int totalLaps) =>
            $"VUELTA {Mathf.Min(lapsDone + 1, totalLaps)}/{totalLaps}";

        /// <summary>
        /// La meta "volver" pero A UN LADO del inicio (playtest: la baliza
        /// encima del spawn se sentía mal): el nodo MÁS CERCANO al spawn cuya
        /// distancia cae en [minDist, maxDist]. Si la banda quedó vacía, el
        /// más cercano pasado minDist (nunca el nodo bajo las ruedas); null
        /// solo si ningún nodo supera el mínimo.
        /// </summary>
        public static RoadNode AsideFromStart(RoadGraphData graph, Vector3 spawn,
            float minDist, float maxDist)
        {
            if (graph == null) return null;
            RoadNode best = null;
            float bestSq = float.MaxValue;
            float minSq = minDist * minDist, maxSq = maxDist * maxDist;

            foreach (var n in graph.Nodes)
            {
                float sq = (n.Position - spawn).sqrMagnitude;
                if (sq < minSq) continue; // demasiado cerca: sería "el inicio"
                bool bandaBest = best != null && (best.Position - spawn).sqrMagnitude <= maxSq;
                bool bandaN = sq <= maxSq;
                // La banda manda; dentro del mismo grupo, el más cercano.
                if (best == null || (bandaN && !bandaBest) ||
                    (bandaN == bandaBest && sq < bestSq))
                {
                    best = n;
                    bestSq = sq;
                }
            }
            return best;
        }
    }
}
