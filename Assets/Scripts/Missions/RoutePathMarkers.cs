using System.Collections.Generic;
using UnityEngine;
using HablaCamaron.AI;
using HablaCamaron.World;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// EL CAMINO PINTADO EN EL SUELO (playtest 2026-07-25: "mejora la guía por
    /// dónde hay que ir"). La píldora de texto de RouteGuide dice qué hacer,
    /// pero hay que LEERLA; esto se ve de un vistazo: una hilera de flechas
    /// doradas sobre la calzada marcando la ruta A* hacia la meta, como las
    /// marcas de un circuito.
    ///
    /// Las flechas son un POOL fijo que se reposiciona: no se instancia ni se
    /// destruye nada por frame. La ruta se recalcula cada RefreshEvery segundos
    /// desde donde está el jugador, así que si se desvía las flechas le indican
    /// cómo volver (a diferencia del corredor de RouteCorridor, que es fijo a
    /// propósito porque de él depende una penalización).
    /// Lo agrega MissionRunner en toda misión con meta.
    /// </summary>
    public class RoutePathMarkers : MonoBehaviour
    {
        /// <summary>Cuántas flechas se ven por delante.</summary>
        public const int MarkerCount = 14;

        /// <summary>Separación entre flechas a lo largo de la ruta (m).</summary>
        public const float SpacingMeters = 12f;

        /// <summary>Cada cuánto se recalcula la ruta (s).</summary>
        public const float RefreshEvery = 1.0f;

        /// <summary>La flecha más cercana que se pinta, medida hacia adelante
        /// desde el auto (m): más cerca que esto ya pasó por debajo del capó.</summary>
        public const float MinAheadMeters = 8f;

        /// <summary>Hasta dónde se pinta el camino (m).</summary>
        public const float MaxAheadMeters = 150f;

        private MissionRunner _runner;
        private Transform _player;
        private readonly List<Transform> _pool = new List<Transform>();
        private readonly List<Vector3> _route = new List<Vector3>();
        private float _refreshIn;
        private RoadNode _awayNode;

        /// <summary>La ruta viva hacia la meta (la dibuja también el minimapa,
        /// para no calcular el mismo A* dos veces por segundo).</summary>
        public IReadOnlyList<Vector3> Route => _route;

        public void Init(MissionRunner runner, Transform player)
        {
            _runner = runner;
            _player = player;
            BuildPool();
        }

        // ---------------- El pool de flechas ----------------

        private void BuildPool()
        {
            var root = new GameObject("[MarcasDeRuta]").transform;
            root.SetParent(transform, false);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            { color = new Color(1f, 0.78f, 0.28f, 0.85f) };
            mat.SetFloat("_Surface", 1f); // transparente
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            for (int i = 0; i < MarkerCount; i++)
            {
                var flecha = new GameObject($"Flecha_{i}").transform;
                flecha.SetParent(root, false);
                BuildArrowMesh(flecha, mat);
                flecha.gameObject.SetActive(false);
                _pool.Add(flecha);
            }
        }

        /// <summary>Una punta de flecha plana, tumbada sobre el asfalto.</summary>
        private static void BuildArrowMesh(Transform parent, Material mat)
        {
            var go = new GameObject("Punta", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);

            // Triángulo apuntando a +Z (el sentido de la marcha) con la cola
            // recortada: se lee como flecha desde la altura del conductor.
            var verts = new[]
            {
                new Vector3(0f, 0f, 2.2f),    // punta
                new Vector3(-1.5f, 0f, 0f),   // ala izquierda
                new Vector3(-0.6f, 0f, 0f),
                new Vector3(-0.6f, 0f, -1.4f),
                new Vector3(0.6f, 0f, -1.4f),
                new Vector3(0.6f, 0f, 0f),
                new Vector3(1.5f, 0f, 0f),    // ala derecha
            };
            // OJO al ORDEN de los vértices: con el sentido natural de la lista
            // las normales salen hacia ABAJO (−Y) y el backface culling deja
            // las flechas INVISIBLES desde el asiento del conductor — estaban
            // ahí, bien colocadas, y no se veía ninguna. Van al revés para que
            // miren al cielo.
            var tris = new[] { 0, 2, 1, 0, 5, 2, 0, 6, 5, 2, 4, 3, 2, 5, 4 };

            var mesh = new Mesh { name = "FlechaRuta" };
            mesh.SetVertices(new List<Vector3>(verts));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ---------------- Colocación sobre la ruta ----------------

        private void Update()
        {
            if (_runner == null || _player == null) return;

            bool show = _runner.Running;
            if (!show) { HideAll(); return; }

            _refreshIn -= Time.deltaTime;
            if (_refreshIn <= 0f)
            {
                _refreshIn = RefreshEvery;
                RecomputeRoute();
            }
            PlaceMarkers();
        }

        private void HideAll()
        {
            foreach (var m in _pool)
                if (m != null && m.gameObject.activeSelf) m.gameObject.SetActive(false);
        }

        /// <summary>Ruta A* desde donde está el jugador hasta la meta activa.</summary>
        private void RecomputeRoute()
        {
            _route.Clear();
            var graph = RoadGraph.Instance != null ? RoadGraph.Instance.Data : null;
            if (graph == null || _runner.GoalTransform == null) return;

            // A dónde guiar depende de POR QUÉ la meta está apagada:
            //  · por checkpoints (nivel 3) → al SIGUIENTE checkpoint;
            //  · por "volver al inicio" (nivel 2) → a ALEJARSE primero.
            // Sin la primera rama, el nivel 3 tomaba la de alejarse y mandaba
            // al jugador al nodo MÁS LEJANO DE LA CIMA: las flechas apuntaban
            // justo al revés de donde había que ir (playtest: "las
            // indicaciones no están claras, puedes ir donde puedas").
            Vector3 destino;
            var siguienteCp = _runner.NextCheckpoint;
            if (_runner.GoalArmed) destino = _runner.GoalTransform.position;
            else if (siguienteCp != null) destino = siguienteCp.position;
            else
            {
                _awayNode ??= MissionGoals.Farthest(graph, _runner.GoalTransform.position);
                if (_awayNode == null) return;
                destino = _awayNode.Position;
            }

            var start = graph.NearestNode(_player.position);
            var end = graph.NearestNode(destino);
            if (start == null || end == null) return;

            var path = AStarPlanner.FindPath(graph, start.Id, end.Id);
            if (path == null) return;
            foreach (var n in path) _route.Add(n.Position);
        }

        /// <summary>
        /// Reparte las flechas cada SpacingMeters a lo largo de la ruta, desde
        /// el punto más cercano al jugador hacia adelante. Cada una se apoya en
        /// el asfalto por raycast y mira hacia el siguiente tramo.
        /// </summary>
        private void PlaceMarkers()
        {
            if (_route.Count < 2) { HideAll(); return; }

            // Desde dónde empezar a pintar: el tramo más cercano al jugador.
            int tramo = 0;
            float mejor = float.MaxValue;
            for (int i = 1; i < _route.Count; i++)
            {
                float d = Vector3.SqrMagnitude(
                    (_route[i] + _route[i - 1]) * 0.5f - _player.position);
                if (d < mejor) { mejor = d; tramo = i - 1; }
            }

            int usados = 0;
            // Distancia acumulada a lo largo de la ruta: se pone una flecha
            // cada SpacingMeters REALES. (El cálculo anterior arrastraba un
            // "resto" por tramo y, con los nodos tan juntos de la ciudad, se
            // saltaba tramos enteros: salían 4 flechas de 14.)
            float recorrido = 0f, siguienteEn = 0f;
            // Las flechas van POR DELANTE (playtest: "debe ser antes del camino
            // para que se vea"): las que quedan detrás del capó no sirven de
            // nada y encima ensucian el retrovisor. Se descarta todo lo que no
            // esté por delante del morro del auto.
            Vector3 morro = _player.position;
            Vector3 rumbo = _player.forward; rumbo.y = 0f;
            if (rumbo.sqrMagnitude < 1e-4f) rumbo = Vector3.forward;
            rumbo.Normalize();
            for (int i = tramo; i < _route.Count - 1 && usados < _pool.Count; i++)
            {
                Vector3 a = _route[i], b = _route[i + 1];
                Vector3 dir = b - a;
                float largo = dir.magnitude;
                if (largo < 0.01f) continue;
                dir /= largo;

                while (siguienteEn < recorrido + largo && usados < _pool.Count)
                {
                    float t = siguienteEn - recorrido;
                    siguienteEn += SpacingMeters;
                    Vector3 pos = a + dir * t;

                    // Solo lo que está por DELANTE y a una distancia útil: ni
                    // pegada al parachoques ni tan lejos que no se distinga.
                    Vector3 haciaFlecha = pos - morro; haciaFlecha.y = 0f;
                    float alFrente = Vector3.Dot(haciaFlecha, rumbo);
                    if (alFrente < MinAheadMeters) continue;
                    if (haciaFlecha.magnitude > MaxAheadMeters) continue;

                    var flecha = _pool[usados++];
                    if (Physics.Raycast(pos + Vector3.up * 6f, Vector3.down,
                                        out var hit, 30f, Physics.DefaultRaycastLayers,
                                        QueryTriggerInteraction.Ignore))
                        pos = hit.point;
                    flecha.SetPositionAndRotation(pos + Vector3.up * 0.06f,
                                                  Quaternion.LookRotation(dir, Vector3.up));
                    if (!flecha.gameObject.activeSelf) flecha.gameObject.SetActive(true);
                }
                recorrido += largo;
            }

            for (int i = usados; i < _pool.Count; i++)
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
        }
    }
}
