using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using static HablaCamaron.EditorTools.ToonCityKit;
using static HablaCamaron.EditorTools.RoadStripKit;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// Decoración urbana compartida (el pase de DISEÑO): veredas en todos los
    /// brazos, filas de casas, mobiliario de acera (postes, bancas, basureros,
    /// árboles), el centro del redondel con su monumento, un skyline de fondo
    /// más allá de los muros límite y nubes estilizadas. Todo determinista
    /// (semillas fijas) y medido por bounds — nunca a ojo.
    /// Las piezas van FUERA de la calzada: el juego pasa en la calle (los
    /// muros de RoadStripKit siguen mandando); esto es el mundo que se VE.
    /// </summary>
    public static class CityDecorKit
    {
        // ---------------- Veredas ----------------

        /// <summary>
        /// Baldosas de vereda a ambos lados del brazo, solo sobre los módulos
        /// PLANOS iniciales (las cuestas no llevan vereda). Devuelve el ancho
        /// de la vereda para apoyar lo demás sobre ella.
        /// Llamar ANTES de PlaceArm (PlaceAligned es world-space).
        /// </summary>
        public static float Sidewalks(Arm arm, string tile = "Pavement_1A_2x2")
        {
            var pave = Load($"{TC}/Pavement/{tile}.prefab");
            if (pave == null) return 3f;

            // Hasta dónde llega el llano inicial (la vereda no trepa cuestas).
            float flatEnd = arm.Length;
            foreach (var m in arm.Modules)
                if (Mathf.Abs(m.Rise) > 0.05f) { flatEnd = m.Z0; break; }

            float half = arm.RoadWidth * 0.5f;
            var probe = PlaceAligned(pave, arm.Container, 0f, -100f, 0f, 0f);
            float tileZ = probe.size.z, paveW = probe.size.x;
            Object.DestroyImmediate(arm.Container.GetChild(arm.Container.childCount - 1).gameObject);

            foreach (float s in new[] { -1f, 1f })
                for (float z = 1f; z < flatEnd - tileZ * 0.5f; z += tileZ)
                    PlaceAligned(pave, arm.Container, 0f, z, 0f, s * (half + paveW * 0.5f));
            return paveW;
        }

        // ---------------- Casas y edificios ----------------

        /// <summary>Fila de edificios a ambos lados (mismo patrón que el barrio
        /// del Corredor, ahora compartido). Llamar ANTES de PlaceArm.</summary>
        public static void BuildingRows(Arm arm, string[] names, float innerX, int seed,
            float zFrom = 2f, float zTo = -1f)
        {
            if (zTo < 0f) zTo = arm.Length - 4f;
            var rng = new System.Random(seed);
            foreach (float s in new[] { -1f, 1f })
            {
                float z = zFrom;
                while (z < zTo)
                {
                    var prefab = Load($"{TC}/Buildings/{names[rng.Next(names.Length)]}.prefab");
                    if (prefab == null) break;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, arm.Container);
                    go.transform.localRotation = Quaternion.Euler(0f, s > 0 ? -90f : 90f, 0f);
                    var b = WorldBounds(go);
                    float x = s * (innerX + b.size.x * 0.5f + 0.4f);
                    go.transform.position += new Vector3(x - b.center.x, -b.min.y, 0f);
                    var lp = go.transform.localPosition;
                    go.transform.localPosition = new Vector3(lp.x, lp.y, z + b.size.z * 0.5f);
                    z += b.size.z + 1.2f;
                }
            }
        }

        // ---------------- Mobiliario de acera ----------------

        private static readonly string[] AceraProps =
        {
            "Props/Trash_Can_1A", "Props/Bench_1A", "Vegetation/Plant_Pot_2A",
            "Props/Trash_Can_1B", "Props/Bench_2A", "Vegetation/Plant_Pot_1A",
        };

        /// <summary>
        /// Vida de vereda: árboles, un poste con cables cada tanto (¡Quito!),
        /// y mobiliario alternado (bancas, basureros, macetas) sobre el llano
        /// del brazo. Llamar ANTES de PlaceArm.
        /// </summary>
        public static void SidewalkLife(Arm arm, float edgeX, int seed)
        {
            float flatEnd = arm.Length;
            foreach (var m in arm.Modules)
                if (Mathf.Abs(m.Rise) > 0.05f) { flatEnd = m.Z0; break; }

            var rng = new System.Random(seed);
            var tree = Load($"{TC}/Vegetation/Tree_1A.prefab");
            var cablePole = Load($"{TC}/Industrial/Cable_Pole_1A.prefab");

            for (float z = 5f; z < flatEnd - 3f; z += 11f)
            {
                float side = rng.Next(2) == 0 ? -1f : 1f;
                int pick = rng.Next(4);
                if (pick == 0 && tree != null)
                    PlaceAligned(tree, arm.Container, 0f, z, 0f, side * edgeX);
                else if (pick == 1 && cablePole != null)
                    PlaceAligned(cablePole, arm.Container, side > 0 ? 90f : -90f, z, 0f, side * (edgeX + 0.6f));
                else
                {
                    var prop = Load($"{TC}/{AceraProps[rng.Next(AceraProps.Length)]}.prefab");
                    if (prop != null)
                        PlaceAligned(prop, arm.Container, side > 0 ? -90f : 90f, z, 0f, side * edgeX);
                }
            }
        }

        // ---------------- El centro del redondel ----------------

        /// <summary>
        /// Decoración completa del redondel central (playtest 2026-07-24:
        /// "demasiado simple, con errores ridículos — estatua flotando sobre
        /// un árbol"): isla con monumento + anillo de flores + bancas mirando
        /// afuera, y una corona exterior de veredas y faroles en los 4 huecos
        /// entre brazos.
        /// EL BUG RAÍZ (diagnosticado por raycast, 2026-07-24): el propio mesh
        /// de `Roundabout_2A`/`_1A` YA modela un árbol al centro de la isla
        /// (copa a ~6.78 m de altura, ~6 m de radio en la base). Un
        /// `Physics.Raycast` normal desde arriba se queda con el primer
        /// impacto — la copa, no el asfalto — así que el monumento anterior
        /// quedaba flotando ahí arriba. Ahora TODO se apoya con
        /// <see cref="RingSurfaceY"/>: raycast FILTRADO a los colliders del
        /// propio redondel, tomando el impacto MÁS BAJO (la copa siempre da
        /// un segundo impacto muy por encima del asfalto, que así se
        /// descarta) — mismo espíritu que `SuperficieDelBrazo` en
        /// ZonaSurBuilder, pero aquí hace falta el MÍNIMO en vez del máximo
        /// porque lo que hay que evitar es un techo, no un piso fantasma.
        /// El monumento y su cortejo se apoyan más allá del radio del árbol
        /// (medido) para no clavarse en su tronco ni taparlo — el árbol
        /// SIGUE ahí, es parte de la isla, solo que ya no comparte sitio con
        /// la estatua. Llamar DESPUÉS de armar el redondel (con
        /// Physics.SyncTransforms hecho).
        /// RADIOS PROPORCIONALES, no metros fijos (revisión ronda 1,
        /// 2026-07-25): el MISMO mesh de `Roundabout_2A`/`_1A` se usa a
        /// escalas distintas por zona — 1.9x en la Zona Sur (`ringRadius`≈
        /// 26.6 m), 1.4x en el Corredor del Examen (`ringRadius`≈19.6 m) —
        /// y el árbol modelado en su isla escala CON el mesh, así que su
        /// proporción respecto a `ringRadius` es constante sin importar la
        /// escala (medido en Zona Sur: copa ~6 m de radio / 26.6 m de
        /// ringRadius ≈ 0.226). Un piso ABSOLUTO (los 8 m originales de esta
        /// función) ignoraba eso: en el redondel 1.4x del Corredor
        /// (ringRadius≈19.6 m) el piso ganaba a la proporción y empujaba la
        /// banca hasta ~0.55 m del carril circulante (`ringRadius * 0.62`,
        /// el mismo radio que usa `BuildRingNodes`/`AddArmToGraph` para los
        /// nodos del anillo) — casi ENCIMA de donde manejan el jugador y los
        /// NPC. Ahora todo es fracción de `ringRadius`, sin piso duro:
        ///   · monR    = 0.30·ringRadius  (> 0.24, con margen sobre el 0.226
        ///                medido de la copa: el monumento no se clava en ella)
        ///   · flowerR = 0.38·ringRadius
        ///   · benchR  = 0.44·ringRadius
        /// Margen banca→carril = ringRadius·(0.62−0.44) = ringRadius·0.18:
        /// Zona Sur ≈ 4.79 m, Corredor ≈ 3.53 m — ambos ≥ 3 m (verificado
        /// también por medición directa tras regenerar las dos escenas).
        /// </summary>
        public static void RoundaboutMonument(Transform world, Transform ring, float ringRadius, int seed)
        {
            Physics.SyncTransforms();
            var rng = new System.Random(seed);

            float RingSurfaceY(Vector3 xz)
            {
                float mejor = float.PositiveInfinity;
                bool found = false;
                foreach (var h in Physics.RaycastAll(xz + Vector3.up * 40f, Vector3.down, 90f))
                    if (h.collider.transform.IsChildOf(ring) && h.point.y < mejor)
                    { mejor = h.point.y; found = true; }
                return found ? mejor : SurfaceY(xz);
            }

            // ---- Isla: monumento separado del árbol del propio mesh ----
            float monR = ringRadius * 0.30f;
            float flowerR = ringRadius * 0.38f;
            float benchR = ringRadius * 0.44f;

            float monA = 50f * Mathf.Deg2Rad; // ángulo suelto: no se alinea con ningún brazo
            Vector3 monXZ = new Vector3(Mathf.Cos(monA), 0f, Mathf.Sin(monA)) * monR;
            var statue = Load($"{TC}/Props/Statue_3A.prefab") ?? Load($"{TC}/Props/Statue_1B.prefab");
            if (statue != null)
                PlaceAt(statue, world, new Vector3(monXZ.x, RingSurfaceY(monXZ), monXZ.z), 0f);

            string[] flores = { "Flower_1A", "Flower_1B", "Flower_2A", "Flower_2C" };
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * flowerR;
                var flor = Load($"{TC}/Vegetation/{flores[rng.Next(flores.Length)]}.prefab");
                if (flor != null) PlaceAt(flor, world, new Vector3(p.x, RingSurfaceY(p), p.z), 0f);
            }

            var bench = Load($"{TC}/Props/Bench_2A.prefab");
            if (bench != null)
                foreach (float a in new[] { 45f, 135f, 225f, 315f })
                {
                    // El banco mira hacia AFUERA: le da la espalda al monumento.
                    Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * benchR;
                    PlaceAt(bench, world, new Vector3(p.x, RingSurfaceY(p), p.z), a);
                }

            // ---- Corona exterior: veredas y faroles en los 4 huecos ENTRE
            //      brazos (los brazos viven en 0/90/180/270 — los huecos, en
            //      45/135/225/315, donde ninguna calzada cruza). El piso de
            //      ahí ya NO es el mesh del redondel: se apoya con el
            //      SurfaceY normal (sin filtrar) sobre BuildGround.
            var pave = Load($"{TC}/Pavement/Pavement_1A_2x2.prefab");
            var lamp = Load($"{TC}/Roads/Streetlight_1A.prefab");
            float coronaR = ringRadius * 1.08f; // 8% más allá del borde físico, proporcional también
            foreach (float centerA in new[] { 45f, 135f, 225f, 315f })
            {
                Vector3 lampP = Quaternion.Euler(0f, centerA, 0f) * Vector3.forward * coronaR;
                if (lamp != null)
                    PlaceAt(lamp, world, new Vector3(lampP.x, SurfaceY(lampP), lampP.z), centerA);

                if (pave == null) continue;
                foreach (float da in new[] { -14f, -7f, 0f, 7f, 14f })
                {
                    Vector3 p = Quaternion.Euler(0f, centerA + da, 0f) * Vector3.forward * coronaR;
                    PlaceAt(pave, world, new Vector3(p.x, SurfaceY(p), p.z), centerA + da);
                }
            }
        }

        // ---------------- Skyline y cielo ----------------

        /// <summary>
        /// Ciudad de FONDO: edificios en el perímetro, más allá de los muros
        /// límite (pura escenografía con profundidad — el jugador nunca llega).
        /// bounds = rectángulo de los nodos del grafo.
        /// </summary>
        public static void Skyline(Transform world, Vector3 min, Vector3 max,
            string[] names, int seed, float distance = 34f, float spacing = 17f)
        {
            var root = new GameObject("Skyline_Fondo").transform;
            root.SetParent(world, false);
            var rng = new System.Random(seed);

            Vector3 c = (min + max) * 0.5f;
            float halfX = (max.x - min.x) * 0.5f + distance;
            float halfZ = (max.z - min.z) * 0.5f + distance;

            // Las cuatro bandas del rectángulo exterior.
            foreach (var (dir, half, along) in new (Vector3, float, float)[]
            {
                (Vector3.forward, halfZ, halfX), (Vector3.back, halfZ, halfX),
                (Vector3.right, halfX, halfZ), (Vector3.left, halfX, halfZ),
            })
            {
                for (float t = -along; t <= along; t += spacing)
                {
                    if (rng.NextDouble() < 0.25) continue; // huecos: skyline vivo
                    var prefab = Load($"{TC}/Buildings/{names[rng.Next(names.Length)]}.prefab");
                    if (prefab == null) continue;
                    Vector3 lateral = Vector3.Cross(Vector3.up, dir);
                    Vector3 pos = c + dir * half + lateral * t;
                    float yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg; // de cara a la ciudad
                    var go = PlaceAt(prefab, root, new Vector3(pos.x, 0f, pos.z), yaw);
                    var b = WorldBounds(go);
                    go.transform.position += Vector3.down * b.min.y; // al piso
                }
            }
        }

        /// <summary>Nubes estilizadas de Toon City flotando alto (solo de día).</summary>
        public static void Clouds(Transform world, int seed, int count = 7, float spread = 130f)
        {
            var rng = new System.Random(seed);
            var root = new GameObject("Nubes").transform;
            root.SetParent(world, false);
            for (int i = 0; i < count; i++)
            {
                var prefab = Load($"{TC}/Clouds/Cloud_1{(rng.Next(2) == 0 ? "A" : "B")}.prefab");
                if (prefab == null) return;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.transform.position = new Vector3(
                    ((float)rng.NextDouble() * 2f - 1f) * spread,
                    55f + (float)rng.NextDouble() * 25f,
                    ((float)rng.NextDouble() * 2f - 1f) * spread);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                float sc = 1.2f + (float)rng.NextDouble() * 1.3f;
                go.transform.localScale *= sc;
            }
        }

        /// <summary>Valla publicitaria (carretera/ciudad). worldPos al piso por raycast.</summary>
        public static void Billboard(Transform parent, Vector3 at, float yaw, string name, bool lit = false)
        {
            var prefab = Load($"{TC}/Advertising/{name}.prefab");
            if (prefab == null) return;
            var go = PlaceAt(prefab, parent, at, yaw);
            if (!lit) return;

            // De noche una valla sin luz no existe: reflector cálido hacia ella.
            var b = WorldBounds(go);
            var lightGO = new GameObject("Reflector_Valla");
            lightGO.transform.SetParent(go.transform, true);
            lightGO.transform.position = b.center + go.transform.forward * -3f + Vector3.up * (b.extents.y * 0.4f);
            lightGO.transform.LookAt(b.center);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 80f;
            light.range = 12f;
            light.intensity = 3.5f;
            light.color = new Color(1f, 0.93f, 0.8f);
        }

        // ---------------- Re-asentado (edificios "voladores") ----------------

        /// <summary>Prefabs que VUELAN por diseño: no re-asentarlos.</summary>
        private static bool VuelaPorDiseno(string name) =>
            name.StartsWith("Cloud") || name.StartsWith("Helicopter");

        /// <summary>
        /// Baja al piso lo que flota DE VERDAD (playtest: "edificios
        /// voladores"): para cada instancia raíz de prefab que no sea vía ni
        /// vuele por diseño, mide el hueco entre su base y la superficie de
        /// abajo (raycast que ignora la propia jerarquía) y si flota más de
        /// 40 cm la asienta. El umbral es alto A PROPÓSITO: los objetos
        /// largos sobre pendientes (guardavías de la Simón) tienen huecos
        /// chicos legítimos bajo su bounds — moverlos los hundía en la vía
        /// (medido: la primera pasada con 5 cm movió 209 objetos sanos).
        /// Devuelve cuántas corrigió (va al log del builder).
        /// </summary>
        public static int ReGround(Transform root)
        {
            Physics.SyncTransforms();
            int fixedCount = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                // Solo raíces EXTERIORES: una raíz anidada (la lámpara del
                // balcón de un edificio) "flota" legítimamente — moverla la
                // arranca de su edificio.
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (src == null || RoadScanKit.EsVia(src.name) || VuelaPorDiseno(src.name))
                    continue;

                var b = WorldBounds(t.gameObject);
                if (b.size == Vector3.zero) continue;
                Vector3 origin = new Vector3(b.center.x, b.min.y + 0.25f, b.center.z);
                float mejorY = float.NegativeInfinity;
                foreach (var h in Physics.RaycastAll(origin, Vector3.down, 120f))
                    if (h.collider != null && !h.collider.transform.IsChildOf(t) &&
                        h.point.y > mejorY)
                        mejorY = h.point.y;
                if (float.IsNegativeInfinity(mejorY)) continue;

                float hueco = b.min.y - mejorY;
                if (hueco > 0.4f)
                {
                    t.position += Vector3.down * hueco;
                    fixedCount++;
                }
            }
            return fixedCount;
        }

        // ---------------- Parques ----------------

        /// <summary>
        /// Un PARQUE de barrio con lo que trae el paquete: fuente (o estatua)
        /// al centro, anillo de flores y pasto, árboles frondosos, bancas y
        /// basurero. Todo medido por bounds y apoyado por raycast.
        /// </summary>
        public static void Park(Transform parent, Vector3 worldCenter, int seed, float radius = 9f)
        {
            Physics.SyncTransforms();
            var root = new GameObject("Parque").transform;
            root.SetParent(parent, true);
            root.position = worldCenter;
            var rng = new System.Random(seed);

            var centro = Load($"{TC}/Props/Fountain_1A.prefab") ??
                         Load($"{TC}/Props/Statue_3A.prefab");
            if (centro != null)
                PlaceAt(centro, root, new Vector3(worldCenter.x, SurfaceY(worldCenter), worldCenter.z), 0f);

            string[] flores = { "Flower_1A", "Flower_2A", "Flower_1C", "Flower_2C" };
            string[] arboles = { "Tree_5A", "Tree_5C", "Tree_5F", "Tree_1A", "Tree_2A" };
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                Vector3 p = worldCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius * 0.45f);
                var flor = Load($"{TC}/Vegetation/{flores[rng.Next(flores.Length)]}.prefab");
                if (flor != null) PlaceAt(flor, root, new Vector3(p.x, SurfaceY(p), p.z), 0f);
            }
            for (int i = 0; i < 6; i++)
            {
                float a = (i + 0.5f) / 6f * Mathf.PI * 2f;
                Vector3 p = worldCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                var arbol = Load($"{TC}/Vegetation/{arboles[rng.Next(arboles.Length)]}.prefab");
                if (arbol != null) PlaceAt(arbol, root, new Vector3(p.x, SurfaceY(p), p.z), rng.Next(360));
            }
            var banca = Load($"{TC}/Props/Bench_2A.prefab");
            if (banca != null)
                foreach (float a in new[] { 30f, 150f, 270f })
                {
                    Vector3 p = worldCenter + Quaternion.Euler(0f, a, 0f) * Vector3.forward * (radius * 0.62f);
                    PlaceAt(banca, root, new Vector3(p.x, SurfaceY(p), p.z), a + 180f);
                }
            var pasto = Load($"{TC}/Vegetation/Grass_Patch_1A.prefab");
            if (pasto != null)
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = worldCenter + new Vector3(
                        ((float)rng.NextDouble() * 2f - 1f) * radius * 0.8f, 0f,
                        ((float)rng.NextDouble() * 2f - 1f) * radius * 0.8f);
                    PlaceAt(pasto, root, new Vector3(p.x, SurfaceY(p), p.z), rng.Next(360));
                }
        }

        // ---------------- Helpers ----------------

        /// <summary>Instancia apoyada en el piso (por bounds) en una posición del mundo.</summary>
        public static GameObject PlaceAt(GameObject prefab, Transform parent, Vector3 worldPos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var b = WorldBounds(go);
            go.transform.position += new Vector3(
                worldPos.x - b.center.x, worldPos.y - b.min.y, worldPos.z - b.center.z);
            return go;
        }

        /// <summary>Altura de la superficie bajo un punto (0 si el rayo no pega).
        /// Se queda con el impacto MÁS BAJO de todos los que toca (mismo criterio
        /// que `RingSurfaceY` arriba): un `Raycast` simple se conforma con el
        /// primer golpe, que puede ser la copa de un árbol o una banca ya puestos
        /// en el mismo punto por una decoración anterior, dejando la siguiente
        /// pieza flotando sobre esa copa en vez de en el piso real.</summary>
        public static float SurfaceY(Vector3 at)
        {
            float mejor = float.PositiveInfinity;
            bool found = false;
            foreach (var h in Physics.RaycastAll(at + Vector3.up * 30f, Vector3.down, 60f))
                if (h.point.y < mejor) { mejor = h.point.y; found = true; }
            return found ? mejor : 0f;
        }
    }
}
