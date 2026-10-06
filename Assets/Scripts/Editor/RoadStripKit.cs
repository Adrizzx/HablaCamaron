using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using HablaCamaron.World;
using static HablaCamaron.EditorTools.ToonCityKit;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// Sistema de TRAMOS de vía compartido por los armadores de zonas (Sur,
    /// Simón Bolívar, corredor del examen). Un tramo (Arm) se construye en su
    /// contenedor EN EL ORIGEN mirando a +Z (donde PlaceAligned funciona),
    /// se decora, y recién entonces se rota/ubica con PlaceArm. Los nodos del
    /// grafo se generan JUNTO con las calles, así el grafo nunca miente.
    ///
    /// Convenciones (tránsito por la derecha, como Ecuador):
    ///  - Carril de ida (+z, alejándose del ancla) en x = +offset.
    ///  - Carril de vuelta (−z, hacia el ancla) en x = −offset.
    ///  - Un módulo con subida (rise&gt;0) usa la pieza tal cual; con BAJADA
    ///    (rise&lt;0) usa la MISMA pieza girada 180° — sube hacia −z = baja hacia +z.
    /// </summary>
    public static class RoadStripKit
    {
        /// <summary>Un módulo del tramo (para decorar y rellenar con precisión).</summary>
        public struct Module
        {
            public float Z0, Z1;   // rango local en z
            public float Y0;       // altura al inicio del módulo
            public float Rise;     // cambio de altura (+sube, −baja, 0 plano)
        }

        /// <summary>Un tramo de vía con sus carriles y nodos.</summary>
        public class Arm
        {
            public Transform Container;
            public float RoadWidth, Length;
            public List<Module> Modules = new List<Module>();
            public List<Vector3> OutboundLocal = new List<Vector3>(); // ida (se aleja)
            public List<Vector3> InboundLocal = new List<Vector3>();  // vuelta (regresa)
            public List<RoadNode> Outbound = new List<RoadNode>();
            public List<RoadNode> Inbound = new List<RoadNode>();
            public float EndY; // altura al final del tramo
        }

        /// <summary>Arma un tramo módulo a módulo. (prefab, rise) por módulo.</summary>
        public static Arm BuildArm(string name, Transform world, float roadYaw,
            IEnumerable<(GameObject prefab, float rise)> modules)
        {
            var arm = new Arm { Container = new GameObject(name).transform };
            arm.Container.SetParent(world, false);

            float z = 0f, y = 0f, laneOff = 0f;

            foreach (var (prefab, rise) in modules)
            {
                var b = PlaceAligned(prefab, arm.Container, roadYaw, z, y, 0f);
                if (arm.RoadWidth <= 0f) { arm.RoadWidth = b.size.x; laneOff = b.size.x * 0.22f; }

                float zNext = b.max.z;
                if (Mathf.Abs(rise) > 0.01f)
                {
                    // Las piezas "+2" de Toon City son PLANAS (bounds ~0.36 m de
                    // alto: la malla NO contiene la subida). Apilarlas con offset
                    // dejaba ACANTILADOS verticales de 2 m a mitad de cuesta
                    // (playtest 2026-07-14: "el auto no puede subir" — chocaba una
                    // pared). La rampa REAL se logra INCLINANDO la pieza alrededor
                    // de su borde bajo; la bajada es la misma pieza inclinada al
                    // revés (ya no hace falta girarla 180°).
                    float len = b.max.z - z;
                    float angRad = Mathf.Asin(Mathf.Clamp(rise / len, -0.9f, 0.9f));
                    var go = arm.Container.GetChild(arm.Container.childCount - 1);
                    go.RotateAround(new Vector3(b.center.x, y, z), Vector3.right,
                        -angRad * Mathf.Rad2Deg);
                    zNext = z + len * Mathf.Cos(angRad); // el borde lejano REAL
                }

                float midZ = (z + zNext) * 0.5f;
                float midY = y + rise * 0.5f;
                arm.OutboundLocal.Add(new Vector3(+laneOff, midY, midZ));
                arm.InboundLocal.Add(new Vector3(-laneOff, midY, midZ));
                arm.Modules.Add(new Module { Z0 = z, Z1 = zNext, Y0 = y, Rise = rise });

                z = zNext;
                y += rise;
            }

            arm.Length = z;
            arm.EndY = y;
            return arm;
        }

        /// <summary>Módulos típicos de un brazo: planos + subida + meseta.</summary>
        public static IEnumerable<(GameObject, float)> ArmModules(GameObject flat,
            GameObject slope, int flats, int slopes, int flatsTop, float rise)
        {
            for (int i = 0; i < flats; i++) yield return (flat, 0f);
            for (int i = 0; i < slopes; i++) yield return (slope, rise);
            for (int i = 0; i < flatsTop; i++) yield return (flat, 0f);
        }

        /// <summary>
        /// Muros invisibles a los COSTADOS del brazo: el juego pasa en la
        /// calle, no entre las casas (y la cámara nunca se mete en un edificio
        /// = adiós pantallas negras). Altos para cubrir también las cuestas.
        /// Hijos del contenedor (local): vale llamarlo antes o después de PlaceArm.
        /// </summary>
        public static void AddSideWalls(Arm arm, float margin = 1.6f)
        {
            float x = arm.RoadWidth * 0.5f + margin;
            // Desde z=0.8 (no invadir el anillo del redondel) hasta pasado el extremo.
            var size = new Vector3(0.6f, 10f, arm.Length + 1.5f);
            var center = new Vector3(0f, 5f, arm.Length * 0.5f + 1.55f);
            var izq = new GameObject("MuroCalle_Izq");
            izq.transform.SetParent(arm.Container, false);
            izq.transform.localPosition = center + new Vector3(-x, 0f, 0f);
            izq.AddComponent<BoxCollider>().size = size;
            izq.AddComponent<WorldBoundary>();
            var der = new GameObject("MuroCalle_Der");
            der.transform.SetParent(arm.Container, false);
            der.transform.localPosition = center + new Vector3(x, 0f, 0f);
            der.AddComponent<BoxCollider>().size = size;
            der.AddComponent<WorldBoundary>();
        }

        /// <summary>Rota y ubica el contenedor (¡decorar ANTES de llamar esto!).</summary>
        public static void PlaceArm(Arm arm, float yawDeg, float anchorDistance)
        {
            var dir = Quaternion.Euler(0f, yawDeg, 0f) * Vector3.forward;
            arm.Container.SetPositionAndRotation(dir * anchorDistance,
                Quaternion.Euler(0f, yawDeg, 0f));
        }

        /// <summary>
        /// 8 nodos de anillo en sentido ANTIHORARIO (así circula un redondel con
        /// tránsito por la derecha) conectados en ciclo.
        /// </summary>
        public static List<RoadNode> BuildRingNodes(RoadGraphData g, float radius)
        {
            var nodes = new List<RoadNode>();
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                nodes.Add(g.AddNode(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius));
            }
            for (int i = 0; i < 8; i++)
                g.Connect(nodes[i].Id, nodes[(i + 1) % 8].Id);
            return nodes;
        }

        /// <summary>
        /// Crea los nodos del tramo (en mundo — llamar DESPUÉS de PlaceArm) y los
        /// conecta: cadenas de ida/vuelta + retorno en el extremo lejano.
        /// Con anillo: entrada/salida al redondel. Sin anillo (ring = null):
        /// retorno también en el extremo cercano (circuito cerrado).
        /// </summary>
        public static void AddArmToGraph(RoadGraphData g, Arm arm, List<RoadNode> ring)
        {
            foreach (var local in arm.OutboundLocal)
                arm.Outbound.Add(g.AddNode(arm.Container.TransformPoint(local)));
            foreach (var local in arm.InboundLocal)
                arm.Inbound.Add(g.AddNode(arm.Container.TransformPoint(local)));

            for (int i = 0; i < arm.Outbound.Count - 1; i++)
                g.Connect(arm.Outbound[i].Id, arm.Outbound[i + 1].Id);
            for (int i = arm.Inbound.Count - 1; i > 0; i--)
                g.Connect(arm.Inbound[i].Id, arm.Inbound[i - 1].Id);

            // Retorno lejano: ningún carril debe ser un callejón para la IA.
            g.Connect(arm.Outbound[arm.Outbound.Count - 1].Id,
                      arm.Inbound[arm.Inbound.Count - 1].Id);

            if (ring != null)
            {
                var entry = arm.Inbound[0];
                var exit = arm.Outbound[0];
                g.Connect(entry.Id, Nearest(ring, entry.Position).Id);
                g.Connect(Nearest(ring, exit.Position).Id, exit.Id);
            }
            else
            {
                // Tramo suelto: retorno también en el extremo cercano.
                g.Connect(arm.Inbound[0].Id, arm.Outbound[0].Id);
            }
        }

        public static RoadNode Nearest(List<RoadNode> nodes, Vector3 pos)
        {
            RoadNode best = nodes[0];
            float bestSq = float.MaxValue;
            foreach (var n in nodes)
            {
                float sq = (n.Position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = n; }
            }
            return best;
        }

        /// <summary>
        /// Rellena bajo los tramos elevados (mesetas y cuñas de "colina") para que
        /// la vía no flote. Recorre los módulos medidos — llamar antes de PlaceArm.
        /// </summary>
        public static void FillUnder(Arm arm, Color? color = null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color ?? new Color(0.30f, 0.27f, 0.20f);
            float w = arm.RoadWidth + 10f;

            foreach (var m in arm.Modules)
            {
                if (Mathf.Abs(m.Rise) > 0.05f)
                {
                    // Cuña bajo la rampa (sube o baja).
                    float len = m.Z1 - m.Z0;
                    float angle = Mathf.Atan2(m.Rise, len) * Mathf.Rad2Deg;
                    var wedge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wedge.name = "Colina_Rampa";
                    wedge.transform.SetParent(arm.Container, false);
                    Vector3 normal = Quaternion.Euler(-angle, 0f, 0f) * Vector3.up;
                    wedge.transform.localPosition =
                        new Vector3(0f, m.Y0 + m.Rise * 0.5f, (m.Z0 + m.Z1) * 0.5f)
                        - normal * (1.5f + 0.06f);
                    wedge.transform.localRotation = Quaternion.Euler(-angle, 0f, 0f);
                    wedge.transform.localScale =
                        new Vector3(w, 3f, Mathf.Sqrt(len * len + m.Rise * m.Rise));
                    wedge.GetComponent<Renderer>().sharedMaterial = mat;
                    Object.DestroyImmediate(wedge.GetComponent<Collider>());
                }
                else if (m.Y0 > 0.05f)
                {
                    // Meseta plana elevada.
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.name = "Colina_Meseta";
                    box.transform.SetParent(arm.Container, false);
                    box.transform.localPosition =
                        new Vector3(0f, m.Y0 * 0.5f - 0.05f, (m.Z0 + m.Z1) * 0.5f);
                    box.transform.localScale = new Vector3(w, m.Y0 - 0.1f, m.Z1 - m.Z0);
                    box.GetComponent<Renderer>().sharedMaterial = mat;
                    Object.DestroyImmediate(box.GetComponent<Collider>());
                }
            }
        }

        // ============ Suavizado de la superficie (fix playtest 2026-07-14) ============

        /// <summary>
        /// Colliders de suavizado INVISIBLES sobre cada corrida de pendiente:
        /// una caja inclinada que une la superficie REAL (medida por raycast)
        /// del llano de abajo con la del llano de arriba, extendida sobre ambos.
        /// Las ruedas ruedan sobre una línea continua y nunca sienten labios ni
        /// desconexiones entre módulos. Llamar DESPUÉS de PlaceArm (mide en
        /// espacio local del contenedor ya colocado).
        /// </summary>
        public static void AddSlopeSmoothColliders(Arm arm)
        {
            Physics.SyncTransforms();
            int i = 0;
            while (i < arm.Modules.Count)
            {
                if (Mathf.Abs(arm.Modules[i].Rise) < 0.05f) { i++; continue; }
                int j = i;
                // Solo se funden rampas de la MISMA inclinación: con rampas
                // crecientes (la cuesta desafiante del nivel 3) una sola cuerda
                // recta de abajo a arriba flotaría sobre la vía a media subida.
                while (j + 1 < arm.Modules.Count &&
                       Mathf.Abs(arm.Modules[j + 1].Rise) > 0.05f &&
                       Mathf.Sign(arm.Modules[j + 1].Rise) == Mathf.Sign(arm.Modules[i].Rise) &&
                       Mathf.Abs(arm.Modules[j + 1].Rise - arm.Modules[i].Rise) < 0.05f)
                    j++;

                float za = Mathf.Max(arm.Modules[i].Z0 - 0.7f, 0.2f);
                float zb = Mathf.Min(arm.Modules[j].Z1 + 0.7f, arm.Length - 0.2f);
                if (SurfaceYAt(arm, za, out float ya) && SurfaceYAt(arm, zb, out float yb))
                    BuildRampCollider(arm.Container, arm.RoadWidth + 0.6f,
                        za, ya, zb, yb, "RampaSuave_Cuesta");
                i = j + 1;
            }
        }

        /// <summary>
        /// Rampa de costura en la ENTRADA del brazo: el borde de la malla del
        /// redondel queda ~11 cm por ENCIMA de la calzada del brazo (medido) y
        /// se sentía como un salto en cada cruce. Se escanea el perfil real,
        /// se encuentra el ESCALÓN exacto y se tiende una rampa que nace en el
        /// borde alto y baja hasta la calzada — sirve en ambos sentidos.
        /// Llamar DESPUÉS de PlaceArm (raycastea en mundo).
        /// </summary>
        public static void AddEntrySeamRamp(Arm arm)
        {
            Physics.SyncTransforms();

            // Perfil de −1 a 4 m: dónde está el peor escalón.
            float worstZ = float.NaN, worstDelta = 0f, prevY = float.NaN;
            for (float z = -1f; z <= 4f; z += 0.2f)
            {
                if (!SurfaceYAt(arm, z, out float y)) { prevY = float.NaN; continue; }
                if (!float.IsNaN(prevY) && Mathf.Abs(y - prevY) > Mathf.Abs(worstDelta))
                {
                    worstDelta = y - prevY;
                    worstZ = z;
                }
                prevY = y;
            }
            if (float.IsNaN(worstZ) || Mathf.Abs(worstDelta) < 0.03f) return; // ya está liso

            // La rampa nace EN el borde alto (5 mm sobre él, para ganarle a la
            // malla justo en la arista) y muere en la calzada 2.2 m más allá.
            float zHigh = worstDelta < 0f ? worstZ - 0.25f : worstZ + 0.05f;
            float zLow = worstDelta < 0f ? worstZ + 2.2f : worstZ - 2.45f;
            if (!SurfaceYAt(arm, zHigh, out float yHigh)) return;
            if (!SurfaceYAt(arm, zLow, out float yLow)) return;
            float za = Mathf.Min(zHigh, zLow), zb = Mathf.Max(zHigh, zLow);
            float ya = za == zHigh ? yHigh : yLow, yb = zb == zHigh ? yHigh : yLow;
            BuildRampCollider(arm.Container, arm.RoadWidth + 0.6f,
                za, ya + 0.008f, zb, yb + 0.008f, "RampaSuave_Entrada");
        }

        /// <summary>Altura local de la superficie bajo (x=0, z) del brazo, por raycast.</summary>
        private static bool SurfaceYAt(Arm arm, float z, out float y)
        {
            y = 0f;
            Vector3 origin = arm.Container.TransformPoint(new Vector3(0f, 60f, z));
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 120f)) return false;
            y = arm.Container.InverseTransformPoint(hit.point).y;
            return true;
        }

        /// <summary>Caja-collider inclinada (sin malla = invisible) cuya cara superior
        /// queda 1 cm bajo la línea de superficie (za,ya)→(zb,yb): atrapa la rueda en
        /// las costuras sin pelear con la malla visible donde esta ya es lisa.</summary>
        private static void BuildRampCollider(Transform parent, float width,
            float za, float ya, float zb, float yb, string name)
        {
            float dz = zb - za, dy = yb - ya;
            float len = Mathf.Sqrt(dz * dz + dy * dy);
            float ang = Mathf.Atan2(dy, dz) * Mathf.Rad2Deg;
            const float t = 0.6f;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Vector3 normal = Quaternion.Euler(-ang, 0f, 0f) * Vector3.up;
            go.transform.localPosition =
                new Vector3(0f, (ya + yb) * 0.5f, (za + zb) * 0.5f) - normal * (t * 0.5f + 0.01f);
            go.transform.localRotation = Quaternion.Euler(-ang, 0f, 0f);
            go.AddComponent<BoxCollider>().size = new Vector3(width, t, len);
        }
    }
}
