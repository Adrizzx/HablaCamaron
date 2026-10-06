using System;
using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.World
{
    /// <summary>Señal de tránsito que aplica a un nodo (la leen NPCs y el puntaje).</summary>
    public enum SignType { None, Stop, Yield, SpeedLimit30, SpeedLimit50, SpeedLimit90 }

    [Serializable]
    public class RoadNode
    {
        public int Id;
        public Vector3 Position;
        public int TrafficLightGroup = -1; // -1 = sin semáforo
        public SignType Sign = SignType.None;
    }

    [Serializable]
    public class RoadEdge
    {
        public int FromId;
        public int ToId;
        public float Cost; // distancia en metros (la usa A* en Fase 2)
    }

    /// <summary>
    /// El grafo de calles: nodos por CARRIL (dirigido — el sentido de la vía es
    /// la dirección de las aristas). Clase pura y serializable: toda la lógica
    /// se prueba en EditMode sin escenas. Es la base de la IA de la Fase 2.
    /// </summary>
    [Serializable]
    public class RoadGraphData
    {
        public List<RoadNode> Nodes = new List<RoadNode>();
        public List<RoadEdge> Edges = new List<RoadEdge>();

        [NonSerialized] private Dictionary<int, List<int>> _adj;
        [NonSerialized] private Dictionary<int, List<(int to, float cost)>> _adjCost;
        [NonSerialized] private Dictionary<int, RoadNode> _byId;

        public RoadNode AddNode(Vector3 position)
        {
            int id = 0;
            foreach (var n in Nodes) if (n.Id >= id) id = n.Id + 1;
            var node = new RoadNode { Id = id, Position = position };
            Nodes.Add(node);
            InvalidateCache();
            return node;
        }

        /// <summary>Arista dirigida from→to con costo = distancia. Rechaza inválidas.</summary>
        /// <summary>
        /// Une dos nodos. `penalizacion` multiplica el costo SIN cambiar la
        /// geometría: sirve para aristas transitables pero indeseables, que A*
        /// solo debe tomar si no hay otro camino. Lo usan las costuras de
        /// RESCATE de la ciudad escaneada: son asfalto tendido sobre lo que era
        /// vereda y, valiendo lo mismo que una calle, A* las metía en la ruta —
        /// la guía mandaba al jugador por encima de la acera (playtest nivel 4:
        /// "esa parte antes era vereda y le hicieron calle, se ve feo, y la
        /// dirección me dice que vaya por ahí").
        /// </summary>
        public bool Connect(int fromId, int toId, float penalizacion = 1f)
        {
            if (fromId == toId) return false;
            var from = GetNode(fromId);
            var to = GetNode(toId);
            if (from == null || to == null) return false;
            foreach (var e in Edges)
                if (e.FromId == fromId && e.ToId == toId) return false; // duplicada

            Edges.Add(new RoadEdge
            {
                FromId = fromId,
                ToId = toId,
                Cost = Vector3.Distance(from.Position, to.Position) * Mathf.Max(penalizacion, 1f)
            });
            InvalidateCache();
            return true;
        }

        public RoadNode GetNode(int id)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<int, RoadNode>();
                foreach (var n in Nodes) _byId[n.Id] = n;
            }
            return _byId.TryGetValue(id, out var node) ? node : null;
        }

        /// <summary>Ids de los nodos alcanzables directamente desde id (salidas).</summary>
        public IReadOnlyList<int> Neighbors(int id)
        {
            if (_adj == null)
            {
                _adj = new Dictionary<int, List<int>>();
                foreach (var n in Nodes) _adj[n.Id] = new List<int>();
                foreach (var e in Edges)
                    if (_adj.TryGetValue(e.FromId, out var list)) list.Add(e.ToId);
            }
            return _adj.TryGetValue(id, out var l) ? l : (IReadOnlyList<int>)Array.Empty<int>();
        }

        /// <summary>
        /// Vecinos de un nodo CON el costo de la arista ya resuelto. Reemplaza
        /// el escaneo lineal de Edges que hacía el A* por vecino (O(E)) — aquí
        /// el costo sale en O(1) de una adyacencia cacheada. Base del A* rápido.
        /// </summary>
        public IReadOnlyList<(int to, float cost)> NeighborEdges(int id)
        {
            if (_adjCost == null)
            {
                _adjCost = new Dictionary<int, List<(int, float)>>(Nodes.Count);
                foreach (var n in Nodes) _adjCost[n.Id] = new List<(int, float)>();
                foreach (var e in Edges)
                    if (_adjCost.TryGetValue(e.FromId, out var list)) list.Add((e.ToId, e.Cost));
            }
            return _adjCost.TryGetValue(id, out var l)
                ? l : (IReadOnlyList<(int, float)>)Array.Empty<(int, float)>();
        }

        /// <summary>
        /// Distancia HORIZONTAL (XZ) al segmento de vía más cercano — la Y no
        /// cuenta (en la cuesta el auto va más alto que los nodos y sigue en
        /// la calle). float.MaxValue sin aristas. La usa la reprobación dura
        /// de MissionRunner ("fuera de la vía").
        /// </summary>
        public float DistanceToNearestEdge(Vector3 position)
        {
            float best = float.MaxValue;
            foreach (var e in Edges)
            {
                var a = GetNode(e.FromId);
                var b = GetNode(e.ToId);
                if (a == null || b == null) continue;
                float d = DistancePointSegmentXZ(position, a.Position, b.Position);
                if (d < best) best = d;
            }
            return best;
        }

        private static float DistancePointSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 p2 = new Vector2(p.x, p.z);
            Vector2 a2 = new Vector2(a.x, a.z);
            Vector2 ab = new Vector2(b.x, b.z) - a2;
            float len = ab.sqrMagnitude;
            float t = len < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / len);
            return Vector2.Distance(p2, a2 + ab * t);
        }

        /// <summary>Nodo más cercano a una posición del mundo (spawn de NPCs, re-ruta).</summary>
        public RoadNode NearestNode(Vector3 position)
        {
            RoadNode best = null;
            float bestSq = float.MaxValue;
            foreach (var n in Nodes)
            {
                float sq = (n.Position - position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = n; }
            }
            return best;
        }

        /// <summary>¿Existe camino dirigido from→to? (BFS; valida mapas generados).</summary>
        public bool IsReachable(int fromId, int toId)
        {
            if (GetNode(fromId) == null || GetNode(toId) == null) return false;
            if (fromId == toId) return true;
            var visited = new HashSet<int> { fromId };
            var queue = new Queue<int>();
            queue.Enqueue(fromId);
            while (queue.Count > 0)
            {
                foreach (int next in Neighbors(queue.Dequeue()))
                {
                    if (next == toId) return true;
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }
            return false;
        }

        /// <summary>Problemas del grafo, en texto (lo usa el armador de zonas y los tests).</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var ids = new HashSet<int>();
            foreach (var n in Nodes)
                if (!ids.Add(n.Id)) problems.Add($"Id de nodo duplicado: {n.Id}");

            foreach (var e in Edges)
            {
                if (!ids.Contains(e.FromId)) problems.Add($"Arista desde nodo inexistente: {e.FromId}");
                if (!ids.Contains(e.ToId)) problems.Add($"Arista hacia nodo inexistente: {e.ToId}");
            }

            // Un nodo sin salidas es una trampa para los NPCs.
            foreach (var n in Nodes)
                if (Neighbors(n.Id).Count == 0)
                    problems.Add($"Nodo {n.Id} sin salidas (callejón para la IA) en {n.Position}");

            return problems;
        }

        private void InvalidateCache() { _adj = null; _byId = null; _adjCost = null; }
    }

    /// <summary>
    /// Portador del grafo en la escena + dibujo con gizmos (nodos, flechas de
    /// sentido, semáforos y señales) para editarlo y depurarlo visualmente.
    /// </summary>
    public class RoadGraph : MonoBehaviour
    {
        public static RoadGraph Instance { get; private set; }

        public RoadGraphData Data = new RoadGraphData();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDrawGizmos()
        {
            if (Data == null) return;

            foreach (var n in Data.Nodes)
            {
                Gizmos.color = n.TrafficLightGroup >= 0 ? Color.red
                    : n.Sign != SignType.None ? Color.yellow
                    : Color.cyan;
                Gizmos.DrawSphere(n.Position + Vector3.up * 0.3f, 0.35f);
            }

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
            foreach (var e in Data.Edges)
            {
                var from = Data.GetNode(e.FromId);
                var to = Data.GetNode(e.ToId);
                if (from == null || to == null) continue;
                Vector3 a = from.Position + Vector3.up * 0.3f;
                Vector3 b = to.Position + Vector3.up * 0.3f;
                Gizmos.DrawLine(a, b);

                // Punta de flecha para ver el sentido de la vía.
                Vector3 dir = (b - a).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir);
                Vector3 tip = Vector3.Lerp(a, b, 0.7f);
                Gizmos.DrawLine(tip, tip - dir * 0.8f + right * 0.4f);
                Gizmos.DrawLine(tip, tip - dir * 0.8f - right * 0.4f);
            }
        }
    }
}
