using System.Collections.Generic;
using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// VALLAS DE DESVÍO (playtest 2026-07-25: "bloquea bien por donde no tiene
    /// que ir"). El corredor de ruta ya avisa y castiga, pero en una ciudad de
    /// 1600 nodos el jugador igual se mete por cualquier bocacalle y descubre
    /// tarde que se equivocó. Esto lo impide ANTES: en los cruces del camino
    /// se plantan vallas naranja sobre las salidas que NO son la ruta.
    ///
    /// Se colocan una sola vez al empezar (la ruta del corredor es fija) y solo
    /// en misiones con RouteLocked. La calzada de la ruta queda SIEMPRE libre:
    /// se descarta cualquier valla que caiga cerca de un tramo del camino, así
    /// que es imposible cerrarle el paso al propio jugador.
    /// </summary>
    public class RouteBarriers : MonoBehaviour
    {
        /// <summary>Radio alrededor de un nodo de la ruta donde se buscan
        /// salidas que cerrar (m).</summary>
        public const float JunctionRadius = 6f;

        /// <summary>Distancia mínima de una valla a CUALQUIER tramo de la ruta:
        /// por debajo de esto no se planta (taparía el propio camino) (m).</summary>
        public const float ClearOfRoute = 7f;

        /// <summary>Cuánto se adentra la valla por la calle prohibida (m).
        /// TIENE que ser mayor que ClearOfRoute: la valla nace en un nodo que
        /// está EN la ruta y se aleja por la rama, así que si se mete menos que
        /// el margen, el filtro de "no tapar el camino" la descarta SIEMPRE.
        /// Con 6 m contra un margen de 7 no se plantaba ni una (medido:
        /// `vallas=0` en el nivel 4) y el bloqueo no existía.</summary>
        public const float IntoBranch = 14f;

        /// <summary>Tope de vallas por misión (rendimiento y estética).</summary>
        public const int MaxBarriers = 60;

        /// <summary>Vallas realmente plantadas (lo usa el test de humo visual).</summary>
        public int Placed { get; private set; }

        /// <summary>
        /// Cierra las bocacalles que no son del camino. `route` es la ruta fija
        /// de la misión (la misma que vigila RouteCorridor).
        /// </summary>
        public void Build(IReadOnlyList<Vector3> route)
        {
            var graph = RoadGraph.Instance != null ? RoadGraph.Instance.Data : null;
            if (graph == null || route == null || route.Count < 2) return;

            var root = new GameObject("[VallasDeDesvio]").transform;
            root.SetParent(transform, false);
            var mat = BarrierMaterial();

            // Los nodos que YA son del camino no se cierran nunca.
            var enRuta = new HashSet<int>();
            foreach (var p in route)
            {
                var n = graph.NearestNode(p);
                if (n != null) enRuta.Add(n.Id);
            }

            var hechas = new List<Vector3>();
            foreach (var p in route)
            {
                if (Placed >= MaxBarriers) break;
                var nodo = graph.NearestNode(p);
                if (nodo == null) continue;

                // Salidas de este cruce que no llevan por la ruta.
                foreach (int vecinoId in graph.Neighbors(nodo.Id))
                {
                    if (Placed >= MaxBarriers) break;
                    if (enRuta.Contains(vecinoId)) continue;

                    var vecino = graph.GetNode(vecinoId);
                    if (vecino == null) continue;

                    Vector3 dir = vecino.Position - nodo.Position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 1f) continue;
                    dir.Normalize();

                    Vector3 pos = nodo.Position + dir * IntoBranch;

                    // Nunca sobre el camino del jugador.
                    if (RouteCorridorJudge.DistanceToRoute(route, pos) < ClearOfRoute) continue;

                    // Ni encima de otra valla ya puesta.
                    bool repetida = false;
                    foreach (var h in hechas)
                        if ((h - pos).sqrMagnitude < 25f) { repetida = true; break; }
                    if (repetida) continue;

                    hechas.Add(pos);
                    PlaceBarrier(root, pos, dir, mat);
                    Placed++;
                }
            }

            if (Placed > 0)
                Debug.Log($"[Habla Camarón] {Placed} vallas cierran las salidas " +
                          "que no son de la ruta.");
        }

        private static Material BarrierMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            return new Material(shader) { color = new Color(0.95f, 0.45f, 0.10f) };
        }

        /// <summary>Una valla de obra: tablero horizontal sobre dos patas,
        /// atravesada en la boca de la calle prohibida.</summary>
        private static void PlaceBarrier(Transform root, Vector3 at, Vector3 dir, Material mat)
        {
            // Apoyarla en el asfalto real.
            if (Physics.Raycast(at + Vector3.up * 8f, Vector3.down, out var hit, 40f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                at = hit.point;

            var valla = new GameObject("Valla").transform;
            valla.SetParent(root, false);
            valla.SetPositionAndRotation(at, Quaternion.LookRotation(dir, Vector3.up));

            // NOTA (auditoría de producción): la valla NO lleva Rigidbody a
            // propósito, así que el sensor frontal de NpcDriver —que descarta
            // todo impacto con `hit.rigidbody == null`— no la ve y los NPC la
            // atraviesan. Se probó ponerle un Rigidbody cinemático para que
            // frenaran, y salió PEOR, medido: la valla cierra ramas que el
            // grafo sigue dando por transitables, así que el NPC frena, se
            // bloquea, replanifica y termina circulando EN CONTRAVÍA
            // (NpcCarrilDiagTests lo cazó). Que un NPC cruce una valla de una
            // calle lateral es un defecto visual menor; que circule en contra
            // del tráfico, en un juego que enseña a manejar, no lo es.
            // Arreglo de fondo pendiente: quitar del grafo de los NPC las
            // aristas que la valla cierra, y ahí sí darle cuerpo físico.
            var tablero = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tablero.name = "Tablero";
            tablero.transform.SetParent(valla, false);
            tablero.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            tablero.transform.localScale = new Vector3(7.5f, 0.55f, 0.25f);
            tablero.GetComponent<Renderer>().sharedMaterial = mat;

            foreach (float s in new[] { -1f, 1f })
            {
                var pata = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pata.name = "Pata";
                pata.transform.SetParent(valla, false);
                pata.transform.localPosition = new Vector3(s * 3.2f, 0.5f, 0f);
                pata.transform.localScale = new Vector3(0.25f, 1.0f, 0.25f);
                pata.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }
    }
}
