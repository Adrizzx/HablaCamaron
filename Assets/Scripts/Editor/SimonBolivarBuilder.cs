using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using HablaCamaron.UI;
using HablaCamaron.World;
using static HablaCamaron.EditorTools.ToonCityKit;
using static HablaCamaron.EditorTools.RoadMeshKit;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// FASE 1 — AV. SIMÓN BOLÍVAR (nocturna). Avenida rápida, larga y solitaria
    /// al borde de Quito: la misión de luces del GDD. Las baldosas prefabricadas
    /// de Toon City se agrietan al girar, así que aquí la vía se MODELA con malla
    /// continua (RoadMeshKit): una calzada dividida con parterre central, dos
    /// sentidos, líneas de borde, bordillos de hormigón y arcén — todo siguiendo
    /// una polilínea 3D con curvas largas y colinas suaves, como la avenida real.
    /// El grafo de carriles se crea SIGUIENDO la curva (nunca miente) y se cierra
    /// en circuito para que la IA no tenga callejones.
    ///
    /// Ambiente nocturno: casi sin luz. Pocas farolas encendidas, árboles a los
    /// lados. Esa soledad ES el diseño: las cortas alumbran las curvas lentas;
    /// las largas, las rectas y bajadas.
    /// Menú: Habla Camarón > 4 · Crear Av. Simón Bolívar (Fase 1)
    /// </summary>
    public static class SimonBolivarBuilder
    {
        private const string SceneP = "Assets/Scenes/N1_SimonBolivar.unity";

        // --- Geometría de la avenida (metros) ---
        private const float SegLen = 8f;         // largo de cada tramo de la polilínea
        private const float LaneWidth = 4.0f;    // un carril por sentido (ancho, tipo autopista)
        private const float MedianWidth = 1.4f;  // parterre central
        private const float MedianHeight = 0.55f;
        private const float Shoulder = 1.8f;     // arcén a cada lado

        private static float DriveHalf => MedianWidth * 0.5f + LaneWidth;   // borde de calzada
        private static float TotalHalf => DriveHalf + Shoulder;             // borde del asfalto
        private static float LaneOffset => MedianWidth * 0.5f + LaneWidth * 0.5f; // eje del carril

        // Trazado: (tramos, giro por tramo °, pendiente ° +sube). Los giros hacen
        // las curvas; la pendiente hace las colinas suaves.
        private static readonly (int tiles, float turn, float grade)[] Route =
        {
            (4,  0f,    0f),    // recta de arranque
            (5, +5f,  +3.5f),   // curva a la derecha subiendo
            (4,  0f,  +3.5f),   // recta en subida
            (5, -6f,  +1f),     // curva izquierda cerca de la cima
            (5,  0f,  -3f),     // recta bajando
            (5, -5f,  -3f),     // curva izquierda en bajada
            (4,  0f,    0f),    // llano en el valle
            (6, +6f,  +2f),     // curva derecha larga subiendo
            (4,  0f,    0f),    // meseta intermedia
            // ---- Extensión (niveles finales más largos, 2026-07-15):
            //      la avenida sigue — segundo valle CON PEAJE y subida final.
            (5, -4f,  -2.5f),   // baja al segundo valle
            (4,  0f,    0f),    // el valle del PEAJE (ver BuildTollBooth)
            (6, +5f,  +2.5f),   // subida final larga
            (3,  0f,    0f),    // meseta de la meta
        };

        /// <summary>Índice del punto (pts) al centro del valle del peaje:
        /// 42 tiles del trazado original + 5 de la bajada + 2 dentro del
        /// valle. Si Route cambia, recalcular (BuildTollBooth lo usa).</summary>
        private const int TollTileIndex = 49;

        // Árboles de Toon City que quedan bien como bosque de borde de avenida.
        private static readonly string[] TreeNames =
        {
            "Tree_3A", "Tree_3B", "Tree_4A", "Tree_4B",
            "Tree_5A", "Tree_5B", "Tree_5C", "Tree_5D", "Tree_2A",
        };

        [MenuItem("Habla Camarón/4 · Crear Av. Simón Bolívar (Fase 1)")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupNightAmbience();

            var world = new GameObject("SimonBolivar_ToonCity").transform;

            // ---- 1) Polilínea 3D del eje de la vía (curvas + colinas) ----
            var pts = new List<Vector3>();
            var headings = new List<float>();
            Vector3 p = Vector3.zero;
            float heading = 0f;
            pts.Add(p);
            foreach (var seg in Route)
                for (int i = 0; i < seg.tiles; i++)
                {
                    heading += seg.turn;
                    Vector3 dir = Quaternion.Euler(-seg.grade, heading, 0f) * Vector3.forward;
                    p += dir * SegLen;
                    pts.Add(p);
                    headings.Add(heading);
                }
            var rights = ComputeRights(pts);

            // ---- 2) MALLA de la avenida: asfalto + marcas + parterre + bordillos ----
            var via = new GameObject("Via").transform;
            via.SetParent(world, false);

            var asphalt = SolidMat(new Color(0.13f, 0.13f, 0.15f));
            var white = SolidMat(new Color(0.88f, 0.88f, 0.82f), 0.25f);
            var concrete = SolidMat(new Color(0.58f, 0.57f, 0.55f), 0.15f);

            // Calzada completa (con arcén) — es la superficie transitable.
            BuildRibbon("Asfalto", via, pts, rights, -TotalHalf, +TotalHalf, 0f, asphalt, collider: true);
            Physics.SyncTransforms(); // para los raycasts de altura de nodos y props

            // Líneas de borde de calzada (blancas continuas) a cada lado.
            BuildRibbon("Linea_Borde_Der", via, pts, rights, DriveHalf - 0.20f, DriveHalf - 0.02f, 0.03f, white, false);
            BuildRibbon("Linea_Borde_Izq", via, pts, rights, -(DriveHalf - 0.02f), -(DriveHalf - 0.20f), 0.03f, white, false);
            // Línea de carril discontinua junto al parterre (referencia visual).
            BuildDashes("Trazos_Der", via, pts, rights, MedianWidth * 0.5f + 0.15f, 0.09f, 0.04f, 3f, 3f, white);
            BuildDashes("Trazos_Izq", via, pts, rights, -(MedianWidth * 0.5f + 0.15f), 0.09f, 0.04f, 3f, 3f, white);

            // Parterre central (separa los dos sentidos) y bordillos exteriores.
            BuildCurb("Parterre", via, pts, rights, 0f, MedianWidth, MedianHeight, concrete, collider: true);
            BuildCurb("Bordillo_Der", via, pts, rights, +TotalHalf, 0.45f, 0.35f, concrete, collider: true);
            BuildCurb("Bordillo_Izq", via, pts, rights, -TotalHalf, 0.45f, 0.35f, concrete, collider: true);

            float roadWidth = TotalHalf * 2f;

            // ---- 3) Grafo de carriles SIGUIENDO la curva ----
            var graphGO = new GameObject("[RoadGraph]");
            var graph = graphGO.AddComponent<RoadGraph>();
            var g = graph.Data;

            var outb = new List<RoadNode>();
            var inb = new List<RoadNode>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 mid = (pts[i] + pts[i + 1]) * 0.5f;
                Vector3 rmid = (rights[i] + rights[i + 1]).normalized;
                outb.Add(g.AddNode(SurfacePoint(mid + rmid * LaneOffset)));  // ida (derecha)
                inb.Add(g.AddNode(SurfacePoint(mid - rmid * LaneOffset)));   // vuelta
            }
            for (int i = 0; i < outb.Count - 1; i++) g.Connect(outb[i].Id, outb[i + 1].Id);
            for (int i = inb.Count - 1; i > 0; i--) g.Connect(inb[i].Id, inb[i - 1].Id);
            g.Connect(outb[outb.Count - 1].Id, inb[inb.Count - 1].Id); // retorno lejano
            g.Connect(inb[0].Id, outb[0].Id);                           // retorno cercano

            outb[0].Sign = SignType.SpeedLimit90;
            inb[inb.Count - 1].Sign = SignType.SpeedLimit90;

            // ---- 4) Decoración: farolas escasas, árboles, señal ----
            var deco = new GameObject("Decoracion").transform;
            deco.SetParent(world, false);
            BuildStreetlights(deco, pts, rights);
            BuildTrees(deco, pts, rights);
            PlaceEcuadorSigns(deco, pts, rights, headings, outb[0].Id);
            BuildGuardrails(deco, pts, rights, headings);  // la Simón real los tiene
            BuildBillboards(deco, pts, rights, headings);  // vallas iluminadas en la soledad
            // PEAJE RETIRADO (playtest 2026-07-28): la caseta se plantaba "al
            // centro de cada calzada" — es decir, ENCIMA del carril por el que
            // se maneja, con sus colliders — y tapaba la avenida por completo.
            // Un peaje que no se puede cruzar no es un obstáculo de diseño, es
            // un muro: fuera, igual que los de la ciudad. El método queda por
            // si algún día se coloca A UN LADO de la vía y con barrera que suba.

            // ---- 5) Piso nocturno y perímetro ----
            Bounds span = new Bounds(pts[0], Vector3.zero);
            foreach (var q in pts) span.Encapsulate(q);
            float groundScale = (Mathf.Max(span.size.x, span.size.z) + 200f) / 10f;
            BuildGround(world, new Vector3(span.center.x, -0.05f, span.center.z),
                groundScale, new Color(0.07f, 0.08f, 0.10f));

            // ---- 6) Aveo (los faros —tecla L— son la estrella de este nivel) ----
            Vector3 spawn = outb[0].Position + Vector3.up * 0.5f;
            BuildPlayerCar(GetOrCreateAveoSpec(), spawn, yawDeg: headings[0]);

            var ui = new GameObject("[GameplayUI]");
            ui.AddComponent<HUDController>();
            ui.AddComponent<PauseController>();
            ui.AddComponent<DonPanchoDialogue>();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            // ---- 7) Validación y guardado ----
            var problems = g.Validate();
            bool reachable = g.IsReachable(outb[0].Id, outb[outb.Count - 1].Id);

            BuildWorldLimits(g); // muros invisibles: la Simón tiene borde

            // OJO: aquí NO corre CityDecorKit.ReGround — las colinas de la
            // Simón son VISUALES (FillUnder sin collider) y el raycast del
            // re-asentado atraviesa el cerro y hunde árboles y guardavías
            // hasta el plano real (medido: movía ~208 objetos sanos). La
            // decoración de esta zona ya nace apoyada por raycast al armarse.
            TrafficInjectionTool.InjectIntoOpenScene(SceneP); // NPCs con modelo, no cubos
            EditorSceneManager.SaveScene(scene, SceneP);
            AddToBuildSettings(SceneP);
            AssetDatabase.SaveAssets();

            float km = (pts.Count - 1) * SegLen;
            EditorUtility.DisplayDialog("Habla Camarón — Fase 1",
                $"Av. Simón Bolívar creada: avenida modelada de {km:0} m con curvas y colinas.\n\n" +
                $"Calzada dividida ({roadWidth:0.0} m) con parterre, líneas y bordillos.\n" +
                $"Grafo: {g.Nodes.Count} nodos, {g.Edges.Count} aristas.\n" +
                $"Validación: {(problems.Count == 0 ? "ninguno ✓" : string.Join("\n", problems))}\n" +
                $"Inicio → final: {(reachable ? "alcanzable ✓" : "¡NO ALCANZABLE!")}\n\n" +
                "Misión de luces: arranca a oscuras, enciende cortas (L) para las\n" +
                "curvas lentas y LARGAS (L otra vez) en las rectas y bajadas.", "¡A la Simón!");

            if (problems.Count > 0 || !reachable)
                Debug.LogError("[Habla Camarón] Grafo de Simón Bolívar con problemas.");
        }

        // ================== Colocación de props ==================

        private static Vector3 SurfacePoint(Vector3 at)
        {
            if (Physics.Raycast(at + Vector3.up * 15f, Vector3.down, out var hit, 40f))
                return hit.point + Vector3.up * 0.05f;
            return at;
        }

        private static Transform PlaceProp(GameObject prefab, Transform parent, Vector3 groundPos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var b = WorldBounds(go);
            go.transform.position += new Vector3(
                groundPos.x - b.center.x, groundPos.y - b.min.y, groundPos.z - b.center.z);
            return go.transform;
        }

        /// <summary>Farolas escasas alternando lado; solo la mitad alumbra (soledad nocturna).</summary>
        private static void BuildStreetlights(Transform parent, List<Vector3> pts, List<Vector3> rights)
        {
            var pole = Load($"{TC}/Roads/Highway_Streetlight_1A.prefab") ??
                       Load($"{TC}/Roads/Streetlight_1A.prefab");
            if (pole == null) return;
            float edge = TotalHalf + 1.0f;
            int lamp = 0;

            for (int i = 3; i < pts.Count - 1; i += 4)
            {
                float side = (lamp % 2 == 0) ? 1f : -1f;
                Vector3 basePos = SurfacePoint(pts[i] + rights[i] * side * edge);
                float yaw = Mathf.Atan2(rights[i].x, rights[i].z) * Mathf.Rad2Deg;
                PlaceProp(pole, parent, basePos, yaw + (side > 0 ? 180f : 0f));

                if (lamp % 2 == 0) // solo una de cada dos enciende
                {
                    var lampGO = new GameObject("Luz_Poste");
                    lampGO.transform.SetParent(parent, false);
                    lampGO.transform.position = basePos - rights[i] * side * 1.4f + Vector3.up * 5.2f;
                    var light = lampGO.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.78f, 0.45f); // sodio anaranjado
                    light.range = 20f;
                    light.intensity = 2.4f;
                    light.shadows = LightShadows.None;
                }
                lamp++;
            }
        }

        /// <summary>Bosque de borde: árboles variados a ambos lados, más allá del bordillo.</summary>
        private static void BuildTrees(Transform parent, List<Vector3> pts, List<Vector3> rights)
        {
            var trees = new List<GameObject>();
            foreach (var n in TreeNames)
            {
                var t = Load($"{TC}/Vegetation/{n}.prefab");
                if (t != null) trees.Add(t);
            }
            if (trees.Count == 0) return;

            var rng = new System.Random(20260709);
            float edge = TotalHalf + 3.0f;

            for (int i = 1; i < pts.Count - 1; i++)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    if (rng.NextDouble() < 0.35) continue; // huecos: no un muro de árboles
                    float dist = edge + (float)rng.NextDouble() * 8f;
                    Vector3 at = SurfacePoint(pts[i] + rights[i] * s * dist);
                    var prefab = trees[rng.Next(trees.Count)];
                    var t = PlaceProp(prefab, parent, at, (float)rng.NextDouble() * 360f);
                    float sc = 0.85f + (float)rng.NextDouble() * 0.5f;
                    t.localScale *= sc;
                }
            }
        }

        /// <summary>
        /// El PEAJE del segundo valle (Toll_Booth_1A de Toon City, ¡para eso
        /// está el paquete!): una caseta por sentido sobre su carril + conos
        /// que canalizan — la avenida se ANGOSTA y obliga a bajar la velocidad
        /// (la parada natural de toda autopista). En el llano, medido de la
        /// polilínea real.
        /// </summary>
        private static void BuildTollBooth(Transform parent, List<Vector3> pts, List<float> headings)
        {
            var booth = Load($"{TC}/Roads/Toll_Booth_1A.prefab");
            if (booth == null || pts.Count <= TollTileIndex + 1) return;

            Vector3 at = pts[TollTileIndex];
            float yaw = headings[Mathf.Min(TollTileIndex, headings.Count - 1)];
            var rot = Quaternion.Euler(0f, yaw, 0f);

            foreach (float lado in new[] { -1f, 1f })
            {
                // Caseta al centro de cada calzada, mirando a su tránsito.
                Vector3 pos = at + rot * new Vector3(lado * LaneOffset, 0f, 0f);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(booth, parent);
                go.transform.rotation = Quaternion.Euler(0f, yaw + (lado < 0 ? 180f : 0f), 0f);
                var b = WorldBounds(go);
                go.transform.position += new Vector3(pos.x - b.center.x, at.y - b.min.y, pos.z - b.center.z);

                // Conos canalizando la entrada a la caseta (30 m antes).
                var cono = Load($"{TC}/Roads/Traffic_Cone_1A.prefab");
                if (cono == null) continue;
                for (int i = 1; i <= 5; i++)
                {
                    Vector3 p = at + rot * new Vector3(
                        lado * (LaneOffset + 2.2f - i * 0.3f), 0f, -lado * (6f + i * 5f));
                    var c = (GameObject)PrefabUtility.InstantiatePrefab(cono, parent);
                    var cb = WorldBounds(c);
                    c.transform.position += new Vector3(p.x - cb.center.x, at.y - cb.min.y, p.z - cb.center.z);
                }
            }
        }

        /// <summary>Guardavías continuos a AMBOS lados siguiendo la curva:
        /// una pieza por tramo de 8 m — el borde de la avenida se lee de noche.</summary>
        private static void BuildGuardrails(Transform parent, List<Vector3> pts,
            List<Vector3> rights, List<float> headings)
        {
            var fence = Load($"{TC}/Roads/Highway_Fence_1A.prefab");
            if (fence == null) return;
            float edge = TotalHalf + 0.5f;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                float yaw = headings[Mathf.Min(i, headings.Count - 1)];
                foreach (float s in new[] { -1f, 1f })
                    PlaceProp(fence, parent, SurfacePoint(pts[i] + rights[i] * s * edge), yaw);
            }
        }

        /// <summary>Tres vallas publicitarias ILUMINADAS de cara al viajero:
        /// puntos de referencia cálidos en la noche cerrada (y muy carretera
        /// ecuatoriana). El reflector lo agrega CityDecorKit.</summary>
        private static void BuildBillboards(Transform parent, List<Vector3> pts,
            List<Vector3> rights, List<float> headings)
        {
            (int i, float side, string name)[] vallas =
                { (9, 1f, "Billboard_4A"), (21, -1f, "Billboard_5A"), (33, 1f, "Billboard_2D") };
            foreach (var (i, side, name) in vallas)
            {
                int idx = Mathf.Clamp(i, 0, pts.Count - 1);
                Vector3 at = SurfacePoint(pts[idx] + rights[idx] * side * (TotalHalf + 7f));
                float yaw = headings[Mathf.Min(idx, headings.Count - 1)] + 180f; // de cara al que viene
                CityDecorKit.Billboard(parent, at, yaw, name, lit: true);
            }
        }

        // ---- Señales ecuatorianas de la avenida (kit de la Fase 4). El frente
        //      de una señal es su -Z local: yaw = rumbo de la vía la deja de
        //      cara al que avanza por el carril de ida. Antes el límite 90 era
        //      un poste de Toon City SIN el "90" visible; ahora es la placa real. ----
        private static void PlaceEcuadorSigns(Transform parent, List<Vector3> pts,
            List<Vector3> rights, List<float> headings, int limitNodeId)
        {
            PlaceSign(parent, pts, rights, headings, 1, "lim90", new[] { limitNodeId });
            PlaceSign(parent, pts, rights, headings, 3, "curva", null);    // antes de la 1ª curva
            PlaceSign(parent, pts, rights, headings, 17, "no_rebasar", null); // hacia la bajada
            PlaceSign(parent, pts, rights, headings, 27, "doble_via", null);  // el llano del valle
        }

        private static void PlaceSign(Transform parent, List<Vector3> pts,
            List<Vector3> rights, List<float> headings, int i, string key, int[] nodes)
        {
            i = Mathf.Clamp(i, 0, pts.Count - 1);
            Vector3 at = SurfacePoint(pts[i] + rights[i] * (TotalHalf + 1.1f));
            float yaw = headings[Mathf.Clamp(i, 0, headings.Count - 1)];
            EcuadorSignKit.Place(key, parent, at, yaw, nodes);
        }
    }
}
