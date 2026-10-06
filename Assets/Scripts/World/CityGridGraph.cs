using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.World
{
    /// <summary>
    /// Generación PURA del grafo vial de una ciudad en CUADRÍCULA (testeada en
    /// EditMode): calles de dos carriles con tránsito por la derecha, nodos por
    /// carril (entrada/medio/salida de cada cuadra) y giros conectados en cada
    /// intersección (recto, derecha e izquierda; U solo si no hay otra salida).
    /// Es la base de la zona "ciudad completa": el builder levanta el asfalto
    /// EXACTAMENTE sobre estas líneas, así el grafo nunca miente.
    /// </summary>
    public static class CityGridGraph
    {
        /// <summary>Rumbos cardinales en orden ANTIHORARIO: E, N, O, S.</summary>
        public static readonly Vector3[] Dirs =
        {
            Vector3.right, Vector3.forward, Vector3.left, Vector3.back,
        };

        /// <summary>Centro de la intersección (i,j) con la cuadrícula centrada
        /// en el origen del mundo.</summary>
        public static Vector3 IntersectionCenter(int cols, int rows, float spacing, int i, int j)
            => new Vector3((i - (cols - 1) * 0.5f) * spacing, 0f,
                           (j - (rows - 1) * 0.5f) * spacing);

        /// <summary>
        /// Construye el grafo dirigido de cols×rows intersecciones separadas
        /// spacing metros. laneOffset = centro del carril respecto al eje de la
        /// calle (a la DERECHA del sentido de marcha); inset = distancia de los
        /// nodos de entrada/salida al centro de su intersección. Con
        /// interiorLights, los accesos a las intersecciones interiores llevan
        /// semáforo: grupo 0 para N/S y grupo 1 para E/O (ciclo alternado).
        /// </summary>
        public static RoadGraphData Build(int cols, int rows, float spacing,
            float laneOffset, float inset, bool interiorLights)
        {
            var g = new RoadGraphData();
            var outN = new Dictionary<(int i, int j, int d), RoadNode>();
            var inN = new Dictionary<(int i, int j, int d), RoadNode>();

            // Carriles de cada cuadra: entrada → medio → salida, por sentido.
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                    for (int d = 0; d < 4; d++)
                    {
                        var (ni, nj) = Neighbor(i, j, d);
                        if (ni < 0 || ni >= cols || nj < 0 || nj >= rows) continue;

                        Vector3 t = Dirs[d];
                        Vector3 right = Vector3.Cross(Vector3.up, t);
                        Vector3 pa = IntersectionCenter(cols, rows, spacing, i, j);
                        Vector3 pb = IntersectionCenter(cols, rows, spacing, ni, nj);

                        var start = g.AddNode(pa + t * inset + right * laneOffset);
                        var mid = g.AddNode((pa + pb) * 0.5f + right * laneOffset);
                        var end = g.AddNode(pb - t * inset + right * laneOffset);
                        g.Connect(start.Id, mid.Id);
                        g.Connect(mid.Id, end.Id);

                        outN[(i, j, d)] = start;
                        inN[(ni, nj, d)] = end;
                    }

            // Giros dentro de cada intersección: recto, derecha, izquierda.
            foreach (var kv in inN)
            {
                var (i, j, d) = kv.Key;
                bool linked = false;
                foreach (int u in new[] { d, (d + 3) % 4, (d + 1) % 4 })
                    if (outN.TryGetValue((i, j, u), out var o))
                    {
                        g.Connect(kv.Value.Id, o.Id);
                        linked = true;
                    }
                // Sin salidas (calle de una sola cuadra): U — jamás un callejón.
                if (!linked && outN.TryGetValue((i, j, (d + 2) % 4), out var back))
                    g.Connect(kv.Value.Id, back.Id);

                if (interiorLights && i > 0 && i < cols - 1 && j > 0 && j < rows - 1)
                    kv.Value.TrafficLightGroup = (d == 1 || d == 3) ? 0 : 1;
            }
            return g;
        }

        private static (int, int) Neighbor(int i, int j, int d) => d switch
        {
            0 => (i + 1, j),
            1 => (i, j + 1),
            2 => (i - 1, j),
            _ => (i, j - 1),
        };
    }
}
