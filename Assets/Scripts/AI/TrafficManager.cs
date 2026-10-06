using System.Collections.Generic;
using UnityEngine;
using HablaCamaron.Vehicle;
using HablaCamaron.World;

namespace HablaCamaron.AI
{
    /// <summary>
    /// Orquesta el tráfico NPC de la zona: mantiene hasta MaxNpcs circulando
    /// alrededor del jugador (spawn lejos de su vista, despawn cuando quedan
    /// atrás), asignando perfiles quiteños con pesos (particular > taxista >
    /// buseta). Si el armador de escena le dio prefabs de Toon City los usa;
    /// si no, fabrica autos-caja de colores (fallback para escenas viejas).
    /// F9: overlay de debug con el estado FSM de cada NPC.
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        public static TrafficManager Instance { get; private set; }

        [Header("Densidad (la ajusta cada misión en Fase 3)")]
        public int MaxNpcs = 12;
        public float SpawnRadiusMin = 45f; // lejos del jugador: nadie "aparece" encima
        public float SpawnRadiusMax = 110f;
        public float DespawnRadius = 150f;

        [Header("Prefabs de Toon City (los asigna el menú 6; vacío = autos-caja)")]
        public List<GameObject> CarPrefabs = new List<GameObject>();

        private readonly List<NpcDriver> _npcs = new List<NpcDriver>();
        private DriverProfile _taxista, _buseta, _particular, _patrullero;
        private Transform _player;

        /// <summary>El auto del jugador (para la burbuja defensiva de los NPCs).</summary>
        public Transform Player => _player;
        private bool _debugOverlay;

        /// <summary>La cámara del jugador. La crea DriverCamera en runtime, así
        /// que se busca perezosamente y se re-busca si se destruyó (cambio de
        /// misión, reintento).</summary>
        private Camera _cam;

        /// <summary>Planos del frustum de este frame (null si no hay cámara):
        /// se calculan UNA vez por Update y los reusan spawn y despawn. El
        /// array se reutiliza (la sobrecarga de CalculateFrustumPlanes que
        /// recibe buffer) para no dejar basura cada frame.</summary>
        private Plane[] _frustum;
        private readonly Plane[] _bufferFrustum = new Plane[6];

        /// <summary>Buffer del raycast de línea de vista: RaycastAll reserva un
        /// array por llamada y esto corre varias veces por frame.</summary>
        private readonly RaycastHit[] _bufferVista = new RaycastHit[16];

        /// <summary>Intentos de nodo por frame al buscar dónde nacer. Con uno
        /// solo, exigir además que quede fuera de la vista hacía que la ciudad
        /// tardara muchísimo en poblarse (el anillo válido se reduce a la parte
        /// que el jugador NO mira). Varios tiros por frame recuperan el ritmo
        /// de aparición sin relajar la regla.</summary>
        private const int IntentosSpawn = 12;

        /// <summary>
        /// ¿Se le VERÍA aparecer/desaparecer? Frustum primero (barato) y solo
        /// entonces la línea de vista (un raycast): tras un edificio no se ve
        /// nada, y sin esa segunda mitad la Zona Sur se queda sin tráfico —
        /// medido, ninguno de sus nodos en rango cae fuera del frustum.
        /// OJO: los VEHÍCULOS no tapan. Un auto escondido detrás de otro auto
        /// queda al descubierto en cuanto el de delante avanza, así que
        /// aceptarlo como escondite reintroduciría el bug por la puerta de
        /// atrás (mismo criterio que ya usa NpcDriver para el apoyo al piso:
        /// un auto no es suelo; aquí, un auto no es pared).
        /// </summary>
        private bool SeLeVeria(Vector3 punto)
        {
            if (!NpcSpawnRules.EnFrustum(_frustum, punto)) return false;
            if (_cam == null) return false;

            // La SILUETA entera, no un punto: un muro bajo tapa el capó y deja
            // el techo a la vista (ver NpcSpawnRules.AlturasDeSilueta).
            Vector3 ojo = _cam.transform.position;
            foreach (float alto in NpcSpawnRules.AlturasDeSilueta)
                if (LineaLibre(ojo, punto + Vector3.up * alto)) return true;
            return false;
        }

        /// <summary>¿Nada sólido entre estos dos puntos? Los vehículos no
        /// cuentan (se apartan) ni el propio auto del jugador.</summary>
        private bool LineaLibre(Vector3 ojo, Vector3 destino)
        {
            Vector3 hacia = destino - ojo;
            float largo = hacia.magnitude - 1.5f; // no morder el propio destino
            if (largo <= 0.1f) return true;

            int n = Physics.RaycastNonAlloc(ojo, hacia.normalized, _bufferVista, largo,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = _bufferVista[i].collider;
                if (_player != null && col.transform.root == _player.root) continue;
                if (col.GetComponentInParent<NpcDriver>() != null) continue;
                return false; // algo sólido de por medio
            }
            // OJO si el buffer se llena (n == longitud): puede haber impactos
            // sin ver. Aquí no importa — cualquiera de ellos habría dicho
            // "tapado", y con el buffer lleno ya devolvimos false salvo que los
            // 16 fueran vehículos, en cuyo caso "libre" es la respuesta correcta.
            return true;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _taxista = DriverProfile.Taxista();
            _buseta = DriverProfile.Buseta();
            _particular = DriverProfile.Particular();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) _debugOverlay = !_debugOverlay;

            if (RoadGraph.Instance == null) return;
            if (_player == null)
            {
                var pc = FindFirstObjectByType<VehicleController>();
                if (pc == null) return;
                _player = pc.transform;
            }

            // Qué ve el jugador AHORA: nadie debe aparecer ni desaparecer
            // dentro de ese cono (playtest: "hay NPCs que aparecen de la nada").
            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
            {
                GeometryUtility.CalculateFrustumPlanes(_cam, _bufferFrustum);
                _frustum = _bufferFrustum;
            }
            else _frustum = null;

            // Despawn de los que quedaron lejos... salvo que se les esté viendo.
            for (int i = _npcs.Count - 1; i >= 0; i--)
            {
                if (_npcs[i] == null) { _npcs.RemoveAt(i); continue; }
                Vector3 p = _npcs[i].transform.position;
                float d = Vector3.Distance(p, _player.position);
                // Cortocircuito: la visibilidad solo importa si YA está en
                // distancia de retirada. Sin esto se pagaban los raycasts de
                // silueta de los 12 NPC en CADA frame, para nada.
                if (d <= DespawnRadius) continue;
                if (!NpcSpawnRules.DebeRetirarse(d, DespawnRadius, SeLeVeria(p))) continue;

                Destroy(_npcs[i].gameObject);
                _npcs.RemoveAt(i);
            }

            // Spawn hasta completar la densidad (uno por frame: sin hipos).
            if (_npcs.Count < MaxNpcs) TrySpawn();
        }

        private void TrySpawn()
        {
            var graph = RoadGraph.Instance.Data;
            if (graph.Nodes.Count == 0) return;

            for (int intento = 0; intento < IntentosSpawn; intento++)
            {
                // Nodo aleatorio dentro del anillo de spawn (lejos pero no
                // perdido) Y FUERA DE LA VISTA del jugador: un auto que se
                // materializa a 60 m de frente rompe la ilusión de ciudad.
                var node = graph.Nodes[Random.Range(0, graph.Nodes.Count)];
                float dist = Vector3.Distance(node.Position, _player.position);
                if (!NpcSpawnRules.PuedeNacer(dist, SpawnRadiusMin, SpawnRadiusMax,
                        SeLeVeria(node.Position))) continue;

                // Que no nazca encima de otro.
                bool ocupado = false;
                foreach (var other in _npcs)
                    if (other != null &&
                        (other.transform.position - node.Position).sqrMagnitude < 36f) { ocupado = true; break; }
                if (ocupado) continue;

                // El perfil sale del MODELO, no de un sorteo aparte: la
                // patrulla tiene que conducirse como patrulla siempre, no el
                // 25% de las veces (ver PerfilPara).
                var go = BuildCarBody(node.Position);
                var driver = go.AddComponent<NpcDriver>();
                driver.Init(graph, PerfilPara(go.name));
                _npcs.Add(driver);
                return;
            }
        }

        /// <summary>
        /// Perfil según el MODELO. Toon City no trae vehículo policial (no hay
        /// prefab ni material: comprobado), así que el modelo designado como
        /// patrulla se identifica por nombre — ver NpcFleetRules.EsPatrulla.
        /// El resto va al sorteo quiteño de siempre.
        /// </summary>
        private DriverProfile PerfilPara(string nombreDelObjeto)
        {
            if (NpcFleetRules.EsPatrulla(nombreDelObjeto))
                return _patrullero ??= DriverProfile.Patrullero();

            float r = Random.value;
            if (r < 0.25f) return _taxista;   // 25% taxistas
            if (r < 0.45f) return _buseta;    // 20% busetas
            return _particular;               // 55% particulares
        }

        // ---------------- Carrocería ----------------

        private GameObject BuildCarBody(Vector3 pos)
        {
            GameObject go;
            if (CarPrefabs.Count > 0)
            {
                var prefab = CarPrefabs[Random.Range(0, CarPrefabs.Count)];
                go = Instantiate(prefab, pos, Quaternion.identity);
                go.name = "NPC_" + prefab.name;
                // Los MeshColliders del asset no sirven en un kinemático que choca:
                // caja simple medida del modelo.
                foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
                var b = MeasureBounds(go);
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(b.center);
                box.size = b.size * 0.95f;
            }
            else
            {
                // Fallback: auto-caja de color (escenas sin prefabs asignados).
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "NPC_Caja";
                go.transform.position = pos + Vector3.up * 0.75f;
                go.transform.localScale = new Vector3(1.9f, 1.4f, 4.4f);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = Color.HSVToRGB(Random.value, 0.55f, 0.8f);
                go.GetComponent<Renderer>().material = mat;
            }

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            return go;
        }

        private static Bounds MeasureBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 2f);
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return b;
        }

        // ---------------- Overlay de debug (F9) ----------------

        private void OnGUI()
        {
            if (!_debugOverlay) return;

            var counts = new Dictionary<NpcState, int>();
            foreach (var npc in _npcs)
            {
                if (npc == null) continue;
                counts.TryGetValue(npc.CurrentState, out int c);
                counts[npc.CurrentState] = c + 1;
            }

            GUILayout.BeginArea(new Rect(12, 12, 340, 300), GUI.skin.box);
            GUILayout.Label($"<b>TRÁFICO NPC (F9)</b> — {_npcs.Count}/{MaxNpcs}",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15 });
            foreach (var kv in counts)
                GUILayout.Label($"  {kv.Key}: {kv.Value}");
            GUILayout.Label("Selecciona un NPC en la Hierarchy para ver su ruta A* (magenta).");
            GUILayout.EndArea();
        }
    }
}
