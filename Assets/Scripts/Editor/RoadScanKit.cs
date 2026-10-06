using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using HablaCamaron.World;
using static HablaCamaron.EditorTools.ToonCityKit;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// ESCÁNER de vías para ciudades YA CONSTRUIDAS (Demo_Scene_1 de Toon
    /// City): encuentra las piezas de vía instanciadas, DETECTA sus puertos
    /// midiendo con raycasts (el asfalto llega al borde sin escalón de vereda
    /// = puerto; el gradiente constante distingue cuesta de bordillo) y le
    /// pide a RoadPortGraph (puro, testeado) el RoadGraph de carriles.
    /// Nada de tablas a mano: medir, nunca a ojo.
    /// </summary>
    public static class RoadScanKit
    {
        /// <summary>Las costuras de RESCATE del último escaneo (las que no
        /// vienen de puertos enfrentados, sino de unir islas). El builder las
        /// necesita para PAVIMENTARLAS: unen piezas que no se tocan, así que
        /// entre ellas puede no haber asfalto — medido en el nivel 4, el 9.7 %
        /// de la ruta pasaba por terreno o por la vereda.</summary>
        public static List<(int a, int b)> LastRescuePairs { get; private set; } =
            new List<(int a, int b)>();

        /// <summary>¿La pieza forma parte de la red conducible?</summary>
        public static bool EsVia(string srcName) =>
            srcName.StartsWith("Road_") || srcName.StartsWith("Highway_1") ||
            srcName.StartsWith("Roundabout_");

        /// <summary>Las instancias de vía de la escena (raíz de prefab + nombre origen).</summary>
        public static List<(GameObject go, string src)> FindRoadPieces(Scene scene)
        {
            var found = new List<(GameObject, string)>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if (src != null && EsVia(src.name)) found.Add((t.gameObject, src.name));
                }
            return found;
        }

        /// <summary>
        /// Escanea la escena abierta y devuelve el grafo de carriles + los
        /// puertos y costuras (para semáforos y cebras: el nodo de ENTRADA
        /// del puerto i es el id 2i+1 y el de salida el 2i — orden de Build).
        /// Requiere Physics.SyncTransforms() previo (raycastea colliders).
        /// </summary>
        public static RoadGraphData Scan(Scene scene, out List<RoadPort> ports,
            out List<(int a, int b)> pairs, out string report)
        {
            var pieces = FindRoadPieces(scene);
            ports = new List<RoadPort>();
            for (int i = 0; i < pieces.Count; i++)
                DetectPorts(pieces[i].go, i, ports);

            // Tolerancia MEDIDA con el diagnóstico [CasiCostura]: las uniones
            // de la demo con las arterias dejan un separador fijo de ~3.0-3.5 m
            // entre bounds vecinos (36 uniones clave fallaban con tolerancia 3).
            // 4.5 las cose todas y sigue por debajo del ancho de una
            // intersección (~11 m): imposible coser saltándose un cruce.
            pairs = RoadPortGraph.Match(ports, tolerance: 4.5f);

            // RESCATE DE ISLAS (2026-07-25). Medido antes de esto: Match dejaba
            // las 314 piezas en 35 componentes y solo el 14.6 % de los destinos
            // tenía ruta A*. Los NPCs se pasaban la vida replanificando (parados
            // y girando en pañuelos) y la meta "el nodo más alto ALCANZABLE" del
            // nivel 3 caía dentro de la isla del spawn — la cuesta salía con
            // desnivel CERO. El rescate une las islas por el par de puertos más
            // cercano (Kruskal, lógica pura testeada), respetando la altura para
            // no coser la autopista elevada con la calle de abajo.
            int normales = pairs.Count;
            var extra = RoadPortGraph.StitchComponents(ports, pairs,
                maxDistance: 14f, maxHeightDelta: 1.2f);
            pairs.AddRange(extra);
            LastRescuePairs = extra;

            var graph = new RoadGraphData();
            // `extra` va aparte para que A* PENALICE esas costuras: son asfalto
            // tendido sobre lo que era vereda, así que se usan solo si no hay
            // calle de verdad por donde ir.
            RoadPortGraph.Build(graph, ports, pairs, extra);

            report = $"{pieces.Count} piezas de vía, {ports.Count} puertos, " +
                     $"{normales} costuras + {extra.Count} de rescate, " +
                     $"{graph.Nodes.Count} nodos, {graph.Edges.Count} aristas";
            return graph;
        }

        /// <summary>
        /// Detecta los puertos de UNA pieza: en el punto medio de cada borde
        /// (XZ) del bounds se muestrea la superficie DE LA PIEZA a 0.6, 1.6 y
        /// 2.6 m hacia adentro. Si el gradiente es CONSTANTE (plano o cuesta)
        /// el asfalto llega al borde = puerto; un escalón (vereda/bordillo)
        /// rompe el gradiente y descarta el borde.
        /// </summary>
        public static void DetectPorts(GameObject piece, int pieceId, List<RoadPort> into)
        {
            var b = WorldBounds(piece);
            float laneOff = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.22f, 1.2f, 2.8f);

            // Cada borde con su semiancho LATERAL (lo que mide el borde a lo
            // largo): el barrido de abajo lo necesita para cubrirlo entero.
            foreach (var (dir, half, semiancho) in new (Vector3, float, float)[]
            {
                (Vector3.forward, b.extents.z, b.extents.x),
                (Vector3.back, b.extents.z, b.extents.x),
                (Vector3.right, b.extents.x, b.extents.z),
                (Vector3.left, b.extents.x, b.extents.z),
            })
            {
                Vector3 edgeMid = new Vector3(b.center.x, 0f, b.center.z) + dir * half;
                Vector3 lateral = Vector3.Cross(Vector3.up, dir).normalized;

                // El muestreo prueba el CENTRO del borde, las líneas de los
                // carriles (±laneOffset) y —desde 2026-07-25— un BARRIDO a lo
                // largo del borde entero.
                // Por qué el barrido: con solo tres muestras (0, ±2.8 m como
                // techo) un borde de 20 m quedaba casi sin explorar, así que
                // las piezas irregulares (las rampas Highway_1D/1E/1F) salían
                // con 0 ó 1 puerto y su trozo de ciudad quedaba incomunicado.
                // Medido: 6 piezas sin ningún puerto y 116 pares de piezas que
                // se TOCAN (separación 0.00 m) sin costura entre ellas.
                // El puerto se sigue anotando en el CENTRO del borde: esto solo
                // decide SI hay asfalto, no dónde va el puerto.
                var laterales = new List<float> { 0f, laneOff, -laneOff };
                for (int k = 1; k <= 4; k++)
                {
                    float f = semiancho * 0.9f * k / 4f;
                    laterales.Add(f);
                    laterales.Add(-f);
                }

                foreach (float lat in laterales)
                {
                    Vector3 baseP = edgeMid + lateral * lat;
                    if (!SurfaceYDe(piece, baseP - dir * 0.6f, b, out float y0)) continue;
                    if (!SurfaceYDe(piece, baseP - dir * 1.6f, b, out float y1)) continue;
                    if (!SurfaceYDe(piece, baseP - dir * 2.6f, b, out float y2)) continue;

                    float d1 = y0 - y1, d2 = y1 - y2;
                    if (Mathf.Abs(d1 - d2) > 0.12f) continue; // escalón: vereda/parterre

                    into.Add(new RoadPort
                    {
                        // La posición del puerto queda en el CENTRO del borde
                        // (la costura con la vecina se busca ahí, sin importar
                        // en qué línea pasó la prueba de asfalto).
                        Position = new Vector3(edgeMid.x, y0, edgeMid.z),
                        Outward = dir,
                        PieceId = pieceId,
                        LaneOffset = laneOff,
                    });
                    break;
                }
            }
        }

        /// <summary>Altura de la superficie DE ESTA PIEZA bajo un punto XZ
        /// (RaycastAll filtrado: los puentes de la autopista pasan por encima
        /// de calles ajenas y un raycast simple mediría el puente).</summary>
        private static bool SurfaceYDe(GameObject piece, Vector3 at, Bounds b, out float y)
        {
            y = 0f;
            Vector3 origin = new Vector3(at.x, b.max.y + 5f, at.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, b.size.y + 15f);
            float best = float.NegativeInfinity;
            foreach (var h in hits)
                if (h.collider != null && h.collider.transform.IsChildOf(piece.transform) &&
                    h.point.y > best)
                    best = h.point.y;
            if (float.IsNegativeInfinity(best)) return false;
            y = best;
            return true;
        }
    }
}
