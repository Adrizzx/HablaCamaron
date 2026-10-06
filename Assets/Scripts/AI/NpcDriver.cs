using System.Collections.Generic;
using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.AI
{
    /// <summary>
    /// Conductor NPC: pide su ruta al A*, "ve" con raycasts (NpcSensors) y
    /// EJECUTA lo que NpcBrain decide. Cinemático (Rigidbody kinematic movido
    /// con MovePosition) para que 20-30 NPCs corran fluidos; el jugador choca
    /// contra ellos con su física normal.
    /// FSM visible: CurrentState se muestra en el overlay de debug (F9) y la
    /// ruta se dibuja con gizmos al seleccionar el NPC.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class NpcDriver : MonoBehaviour
    {
        public DriverProfile Profile;
        public NpcState CurrentState { get; private set; } = NpcState.Cruising;
        public float Speed { get; private set; }

        /// <summary>
        /// Lo último que este NPC "vio". Se expone para que los diagnósticos
        /// PlayMode puedan comprobar los sensores TAL COMO LOS LLENA EL JUEGO,
        /// en vez de asignarlos a mano: este proyecto ya tuvo tres sensores
        /// declarados que nadie rellenaba (estados inalcanzables) mientras los
        /// tests EditMode, que los ponían a dedo, seguían en verde.
        /// </summary>
        public NpcSensors UltimosSensores { get; private set; }

        /// <summary>
        /// Velocidad real del NPC. Es cinemático (MovePosition), así que su
        /// Rigidbody.linearVelocity es SIEMPRE cero: preguntárselo a la física
        /// daría "parado" para todos y el tiempo-a-colisión saldría infinito.
        /// </summary>
        public Vector3 Velocidad => transform.forward * Speed;

        /// <summary>
        /// Registro de NPC vivos, para que se vean ENTRE ELLOS. Va aquí y no en
        /// TrafficManager a propósito: el manager solo conoce a los que él
        /// mismo creó, así que los NPC sembrados por los diagnósticos (y los de
        /// cualquier herramienta futura) quedarían invisibles justo para el
        /// sensor que se está midiendo.
        /// </summary>
        private static readonly List<NpcDriver> _vivos = new List<NpcDriver>();
        public static IReadOnlyList<NpcDriver> Vivos => _vivos;

        private void OnEnable() { if (!_vivos.Contains(this)) _vivos.Add(this); }
        private void OnDisable() { _vivos.Remove(this); }

        /// <summary>
        /// ¿Este NPC cuenta como "esperando el semáforo" ahora mismo? La
        /// propagación hacia atrás en la cola (NpcQueueWait) hace que sea true
        /// tanto si YO veo el rojo y lo respeto, como si el auto de adelante ya
        /// espera y yo voy casi parado detrás de él — así toda la fila queda
        /// exenta del desatasco anti-atasco, no solo el que ve el semáforo con
        /// sus propios sensores (que solo alcanzan ~25 m / 3 nodos).
        /// </summary>
        public bool EsperandoSemaforo { get; private set; }

        private RoadGraphData _graph;
        private List<RoadNode> _path;
        private int _pathIndex;
        private bool _obeysThisLight = true;
        private int _lastLightNodeId = -1;
        private float _stateTimer;                 // Crashed / DoubleParked
        private float _blockedTime;                // segundos parado sin razón que avance (bocina)
        private readonly NpcUnstuckRoutine _unstuck = new NpcUnstuckRoutine();
        private AudioSource _horn;                 // la bocina (pita al que lo bloquea)
        private float _nextHonkAt = 1.4f;          // segundos de bloqueo para el 1er pito
        private Rigidbody _rb;
        private readonly List<Transform> _wheels = new List<Transform>();
        private float _groundOffset;               // pivote → punto más bajo del auto
        private Vector3 _normalPiso = Vector3.up;   // normal de la vía bajo el auto (sensor en cuesta)
        private NpcDriver _aheadNpc;                // el NPC más cercano de adelante (si hay), para propagar EsperandoSemaforo

        public void Init(RoadGraphData graph, DriverProfile profile)
        {
            _graph = graph;
            Profile = profile;
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;

            foreach (var t in GetComponentsInChildren<Transform>())
                if (t.name.Contains("Wheel_")) _wheels.Add(t);

            // Distancia del pivote al punto más bajo del auto: así lo apoyamos
            // sobre la vía sin flotar ni hundirse (caja de fallback o modelo Toon City).
            var b = SelfBounds();
            _groundOffset = Mathf.Max(0f, transform.position.y - b.min.y);
            StickToGround(); // recién nacido: apoyarlo YA (no aparecer flotando)

            // Bocina 3D: suena desde el auto (fuerte cerca, nada de lejos).
            _horn = gameObject.AddComponent<AudioSource>();
            _horn.clip = NpcHorn.Clip;
            _horn.spatialBlend = 1f;
            _horn.minDistance = 5f;
            _horn.maxDistance = 45f;
            _horn.rolloffMode = AudioRolloffMode.Linear;

            PlanNewRoute();

            // Nacer ya MIRANDO hacia su ruta: sin la pirueta de girar en el
            // sitio (que en el anillo parecía "cambiar de dirección").
            if (_path != null && _pathIndex < _path.Count)
            {
                Vector3 d = _path[_path.Count > _pathIndex + 1 ? _pathIndex + 1 : _pathIndex]
                                .Position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 1f)
                {
                    var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
                    transform.rotation = rot;
                    _rb.rotation = rot;
                }
            }
        }

        /// <summary>Cuánto se corre el NPC hacia el carril izquierdo al adelantar (m).</summary>
        private const float OvertakeOffset = 3.2f;

        /// <summary>Burbuja defensiva: cuánto por DELANTE cuenta el jugador (m).</summary>
        private const float PlayerBubbleAhead = 12f;

        /// <summary>Y cuánto puede desviarse de mi línea de marcha (m). Media
        /// calzada: si va por el otro carril no es asunto mío.</summary>
        private const float PlayerBubbleSide = 3.5f;

        /// <summary>Segundos parado por el jugador antes de rodearlo al paso.</summary>
        private const float PlayerStalemateSeconds = 4f;

        private float _playerStalled;

        /// <summary>Segundos que se aguanta un rojo antes de sospechar que está
        /// clavado. Un ciclo completo de TrafficLightCycle dura bastante menos,
        /// así que esto solo salta cuando de verdad hay un problema.</summary>
        private const float RedLightPatience = 45f;

        private float _stoppedTime;


        /// <summary>Intentos de destino por replanificación (ver PlanNewRoute).</summary>
        private const int RouteTries = 8;

        /// <summary>Ruta nueva hacia un nodo aleatorio del grafo (deambular urbano).</summary>
        private void PlanNewRoute()
        {
            if (_graph == null || _graph.Nodes.Count < 2) return;
            // Nodo de arranque ADELANTE y en el carril propio (no el más
            // cercano a secas: eso hacía dar media vuelta en el redondel).
            var start = RoutePlanning.BestStartNode(_graph, transform.position, transform.forward);

            // VARIOS destinos por intento, no uno. Con un solo tiro al azar,
            // cada destino sin ruta dejaba al NPC QUIETO ese frame (arriba se
            // hace `PlanNewRoute(); return;`), y en un grafo imperfecto eso son
            // rachas enteras de NPCs parados. Se prueba hasta dar con uno
            // alcanzable; si ninguno sirve, se reintenta el frame siguiente.
            _path = null;
            for (int i = 0; i < RouteTries && _path == null; i++)
            {
                var goal = _graph.Nodes[Random.Range(0, _graph.Nodes.Count)];
                if (goal.Id == start.Id) continue;
                _path = AStarPlanner.FindPath(_graph, start.Id, goal.Id);
            }

            // Primer waypoint que NO obliga a un giro en U (pasa al
            // replanificar en marcha): si toda la ruta queda atrás del
            // rumbo actual, el índice cae en _path.Count y el NPC sigue
            // por su calle — la propia FixedUpdate replanifica al vaciarse.
            _pathIndex = _path != null
                ? RoutePlanning.FirstUsefulWaypoint(_path, transform.position, transform.forward)
                : 0;
        }

        private void FixedUpdate()
        {
            if (_graph == null || Profile == null) return;
            float dt = Time.fixedDeltaTime;

            // Estados con temporizador (no deciden, esperan).
            if (CurrentState == NpcState.Crashed || CurrentState == NpcState.DoubleParked)
            {
                // No es una espera de semáforo legítima: que un seguidor no
                // herede por error un EsperandoSemaforo viejo de antes del
                // choque/parada (quedaría exento del desatasco mientras dura).
                EsperandoSemaforo = false;
                Speed = Mathf.MoveTowards(Speed, 0f, Profile.BrakeDecel * dt);
                _stateTimer -= dt;
                if (_stateTimer <= 0f) CurrentState = NpcState.Cruising;
                Move(dt);
                return;
            }

            if (_path == null || _pathIndex >= _path.Count) { PlanNewRoute(); return; }

            var sensors = Sense();
            UltimosSensores = sensors;
            var decision = NpcBrain.Decide(sensors, Profile, _obeysThisLight);
            CurrentState = decision.State;

            // La buseta se detiene en doble fila de vez en cuando (¡sube, sube!),
            // pero JAMÁS junto a un semáforo o un ceda (el anillo del redondel):
            // ahí una parada bloquea a toda la zona.
            if (CurrentState == NpcState.Cruising && Profile.DoubleParkChance > 0f &&
                !NearTrafficControl() &&
                Random.value < Profile.DoubleParkChance * dt * 0.2f)
            {
                CurrentState = NpcState.DoubleParked;
                _stateTimer = Profile.DoubleParkDuration;
                return;
            }

            // Frenar frena MÁS fuerte que acelerar; la emergencia, el doble
            // (si el cerebro dice Braking con objetivo 0, es porque va en serio).
            float rate = decision.TargetSpeed < Speed ? Profile.BrakeDecel : Profile.Acceleration;
            if (decision.State == NpcState.Braking && decision.TargetSpeed <= 0.01f)
                rate = Profile.BrakeDecel * 2.2f;
            Speed = Mathf.MoveTowards(Speed, decision.TargetSpeed, rate * dt);

            // ¿Cuenta este NPC como "esperando semáforo" AHORA? No solo si él
            // mismo ve el rojo (Sense() solo alcanza ~25 m / 3 nodos: en una
            // cola larga el 3º o 4º auto ya no lo ve) — también si va casi
            // parado detrás de uno que ya espera: la espera se PROPAGA hacia
            // atrás por toda la fila, auto por auto (un frame de retraso: el
            // de adelante calculó SU EsperandoSemaforo el frame anterior, y el
            // umbral del desatasco son 3 s completos, así que no importa).
            EsperandoSemaforo = NpcQueueWait.Espera(
                sensors.RedLightAhead && _obeysThisLight, Speed,
                _aheadNpc != null && _aheadNpc.EsperandoSemaforo);

            // ANTI-BLOQUEO: parado por obstáculo (no por semáforo) demasiado
            // tiempo → ruta nueva; si ni así se destraba y el jugador no está
            // mirando de cerca, se recicla (el manager pondrá otro en su lugar).
            // Esperar un rojo es legítimo... pero no eternamente. Si lleva más
            // que un ciclo entero de semáforo sin moverse, algo va mal (el
            // grupo del nodo no alterna, o hay un tapón delante) y hay que
            // destrabarlo: sin esta salida el NPC quedaba clavado PARA SIEMPRE
            // en el cruce, porque el rojo lo eximía del anti-bloqueo y el
            // reciclado nunca llegaba a contarlo (playtest: "los carros se
            // siguen bloqueando, pasa en los semáforos").
            bool esperandoRojo = sensors.RedLightAhead && _obeysThisLight;
            _stoppedTime = Speed < 0.3f ? _stoppedTime + dt : 0f;
            bool rojoEterno = esperandoRojo && _stoppedTime > RedLightPatience;

            bool blocked = Speed < 0.3f &&
                (CurrentState == NpcState.Following || CurrentState == NpcState.Braking ||
                 CurrentState == NpcState.Yielding) &&
                // Esperar el rojo (propio o de la fila) es legítimo... salvo que
                // lleve un ciclo entero clavado (rojoEterno): ahí sí hay que
                // destrabarlo aunque el semáforo diga que espere.
                (!EsperandoSemaforo || rojoEterno);
            if (blocked) _blockedTime += dt;
            else
            {
                // Se destrabó: rearmar la bocina para el próximo bloqueo.
                if (_blockedTime > 0f) _nextHonkAt = 1.4f + Random.value * 1.2f;
                _blockedTime = 0f;
            }

            // Quiteñísimo: bloqueado (no cediendo el paso, que sería de mala
            // educación) → PITA en vez de embestir, y repite si el estorbo sigue.
            if (blocked && CurrentState != NpcState.Yielding && _blockedTime >= _nextHonkAt)
            {
                _nextHonkAt = _blockedTime + 2.5f + Random.value * 2.5f;
                if (_horn != null)
                {
                    _horn.volume = Core.GameAudioSettings.EffectiveSfxVolume;
                    if (_horn.volume > 0.01f) _horn.Play();
                }
            }

            // ESCALERA DE DESATASCO (pedido del dueño: nada de Destroy() al
            // toque — los NPC trabados se destraban solos; el reciclado es
            // el ÚLTIMO recurso, a los 30 s). NpcUnstuckRoutine es PURA y
            // reutiliza el mismo `blocked` de arriba (no duplica el cálculo).
            switch (_unstuck.Tick(blocked, dt))
            {
                case UnstuckMove.Retroceso:
                {
                    // Un empujoncito atrás para despegarse del estorbo.
                    // Move() NO soporta Speed negativo (con Speed < 0.01f ni
                    // siquiera llama a MovePosition, ver arriba), así que el
                    // retroceso mueve el rígido a mano por -forward (mismo
                    // patrón que StickToGround: asignación directa a un
                    // Rigidbody kinemático) y este frame NO llama a Move()
                    // para que el avance normal no lo pise.
                    Speed = 0f;
                    float paso = Mathf.Min(1.6f, Profile.CruiseSpeed * 0.25f) * dt;
                    Vector3 atras = _rb.position - transform.forward * paso;
                    if (TryGroundHeight(atras, out float gy)) atras.y = gy;
                    _rb.position = atras;
                    transform.position = atras;
                    SpinWheels(-dt);
                    return;
                }
                case UnstuckMove.Esquive:
                {
                    // Medio carril a la derecha, sin invadir el contrario.
                    // Misma tasa que usa NpcUnstuckRoutine para llevar la
                    // cuenta de la deriva acumulada (techo MaxDerivaMetros):
                    // si aquí se moviera a otro ritmo, el techo de la rutina
                    // dejaría de corresponder al desplazamiento real.
                    Speed = 0f;
                    Vector3 lado = _rb.position + transform.right *
                        (NpcUnstuckRoutine.EsquiveMetrosPorSegundo * dt);
                    if (TryGroundHeight(lado, out float gy)) lado.y = gy;
                    _rb.position = lado;
                    transform.position = lado;
                    SpinWheels(dt);
                    return;
                }
                case UnstuckMove.Replanificar:
                    PlanNewRoute();
                    break;
                case UnstuckMove.Reciclar:
                    var player = TrafficManager.Instance != null ? TrafficManager.Instance.Player : null;
                    if (player == null || (player.position - transform.position).magnitude > 25f)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    break;
            }
            Move(dt);
        }

        // ---------------- Sentidos ----------------

        private NpcSensors Sense()
        {
            // OJO con los valores por defecto de la struct: un
            // NeighborTimeToCollision en 0 significaría "chocamos AHORA MISMO",
            // así que hay que inicializarlo explícito igual que AheadDistance.
            var s = new NpcSensors
            {
                AheadDistance = float.MaxValue,
                NeighborTimeToCollision = float.MaxValue,
            };
            _aheadNpc = null; // se vuelve a fijar abajo si el bigote encuentra un NPC

            // "Bigotes" hacia donde el auto SE MUEVE (el siguiente nodo de la
            // ruta, no la trompa): en curvas y redondel el rayo recto miente.
            // Tres conos (centro y ±18°) y alcance que crece con la velocidad.
            Vector3 moveDir = transform.forward;
            if (_path != null && _pathIndex < _path.Count)
            {
                Vector3 toTarget = _path[_pathIndex].Position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.5f) moveDir = toTarget.normalized;
            }
            float range = Mathf.Max(18f, Speed * 3.5f);
            foreach (float ang in new[] { 0f, -18f, 18f })
            {
                Vector3 forwardDir = Quaternion.Euler(0f, ang, 0f) * moveDir;
                // El origen sube sobre la NORMAL de la vía (no un Vector3.up fijo)
                // y la dirección se proyecta al plano de la pendiente: en cuesta
                // un cast horizontal muerde el propio asfalto y la FSM ve un
                // "obstáculo" eterno (playtest: NPCs parados sin razón).
                var (origin, dir) = NpcSensorMath.ForwardRay(transform.position, forwardDir, _normalPiso);
                if (!Physics.SphereCast(origin, 0.8f, dir, out var hit, range) ||
                    hit.rigidbody == null || hit.rigidbody == _rb) continue;
                if (hit.distance >= s.AheadDistance) continue;

                // DISCIPLINA DE CARRIL: lo que está lejos Y desplazado de mi
                // línea de marcha es el carril contrario — no es obstáculo.
                // (Cerca sí cuenta siempre: seguridad primero.)
                Vector3 d = hit.point - origin;
                float along = Vector3.Dot(d, moveDir);
                float lateral = (d - moveDir * along).magnitude;
                if (along > 8f && lateral > 2.2f) continue;

                s.AheadDistance = hit.distance;
                // Velocidad de cierre: la mía menos la de eso, proyectadas.
                var otherNpc = hit.rigidbody.GetComponent<NpcDriver>();
                _aheadNpc = otherNpc; // se reutiliza en FixedUpdate para propagar EsperandoSemaforo (sin otro raycast)
                float otherAlong = otherNpc != null
                    ? otherNpc.Speed * Vector3.Dot(hit.rigidbody.transform.forward, dir)
                    : Vector3.Dot(hit.rigidbody.linearVelocity, dir);
                s.AheadClosingSpeed = Mathf.Max(0f, Speed - otherAlong);

                // ¿El de adelante está DETENIDO? (doble fila, choque, atasco).
                // Sin esto, NpcBrain jamás llegaba a Overtaking ni al desempate
                // anti-atasco: los campos existían y NADIE los llenaba, así que
                // el NPC se quedaba clavado detrás del estorbo para siempre
                // (playtest: "a veces se quedan parados"). Mismo bug que tuvo
                // en su día el sensor de ceda.
                float velOtro = otherNpc != null
                    ? otherNpc.Speed : hit.rigidbody.linearVelocity.magnitude;
                s.AheadIsStatic = velOtro < 0.4f;

                // Desempate anti-deadlock: si los dos estamos parados, avanza
                // UNO solo — el de menor id de instancia. Es simétrico y
                // determinista, así que nunca arrancan los dos a la vez.
                // OJO — se asigna SIEMPRE, no solo cuando se cumple la
                // condición: era el único campo del bucle que no se
                // reasignaba, y con tres bigotes eso es una FUGA DE ESTADO.
                // Secuencia real: el rayo central engancha un auto PARADO
                // (AheadIsStatic=true, HasPriorityOverBlocker=true) y luego el
                // de -18° engancha uno EN MOVIMIENTO más cerca; AheadIsStatic
                // se reasigna a false, pero la prioridad conservaba el true
                // viejo y el cerebro reanudaba crucero contra un auto que sí
                // se movía (regla 4b). Lo cazó la auditoría de lectura.
                s.HasPriorityOverBlocker = s.AheadIsStatic && Speed < 0.4f &&
                                           otherNpc != null &&
                                           GetInstanceID() < otherNpc.GetInstanceID();
            }

            // Carril izquierdo libre para rodear al que está detenido: se mira
            // en paralelo, desplazado a la izquierda, el mismo tramo de adelante.
            if (s.AheadIsStatic)
            {
                Vector3 izq = -Vector3.Cross(Vector3.up, moveDir).normalized;
                var (oIzq, dIzq) = NpcSensorMath.ForwardRay(
                    transform.position + izq * 3.2f, moveDir, _normalPiso);
                s.LeftLaneClear = !Physics.SphereCast(oIzq, 0.8f, dIzq, out var hIzq,
                                       Mathf.Max(s.AheadDistance + 4f, 10f)) ||
                                  hIzq.rigidbody == null || hIzq.rigidbody == _rb;
            }

            // Burbuja defensiva alrededor del JUGADOR: a menos de 12 m y no
            // claramente detrás → el NPC se DETIENE y espera a que se quite
            // (aunque los bigotes no lo pesquen). Jamás lo embiste.
            var player = TrafficManager.Instance != null ? TrafficManager.Instance.Player : null;
            if (player != null)
            {
                Vector3 toPlayer = player.position - transform.position;
                toPlayer.y = 0f;
                // El jugador cuenta si está DELANTE y en mi línea de marcha.
                // El cono de 75° (150° de apertura) era demasiado ancho: en un
                // redondel o una curva, moveDir apunta al siguiente nodo y el
                // jugador quedaba "delante" aunque fuera por detrás o por otro
                // carril — el NPC frenaba en seco y tapaba el anillo.
                float delante = Vector3.Dot(toPlayer, moveDir);
                float lateral = (toPlayer - moveDir * delante).magnitude;
                s.PlayerNear = delante > 0f && delante < PlayerBubbleAhead &&
                               lateral < PlayerBubbleSide;
            }

            // Empate con el jugador: llevamos demasiado tiempo parados por él
            // y no se aparta (calado, leyendo el mapa...). El cerebro pasa a
            // rodearlo al paso en vez de quedarse clavado tapando la calle.
            _playerStalled = s.PlayerNear && Speed < 0.4f
                ? _playerStalled + Time.fixedDeltaTime : 0f;
            s.PlayerStalemate = _playerStalled >= PlayerStalemateSeconds;

            // Semáforo en la ruta próxima.
            var lights = TrafficLightController.Instance;
            if (lights != null && _path != null)
            {
                for (int i = _pathIndex; i < Mathf.Min(_pathIndex + 3, _path.Count); i++)
                {
                    var node = _path[i];
                    if (node.TrafficLightGroup < 0) continue;

                    float dist = Vector3.Distance(transform.position, node.Position);
                    if (dist > 25f) break;

                    // Al ACERCARSE a un semáforo nuevo, el perfil decide si lo respeta
                    // (una vez, no por frame: así el taxista "se lanza" con convicción).
                    if (node.Id != _lastLightNodeId)
                    {
                        _lastLightNodeId = node.Id;
                        _obeysThisLight = Random.value <= Profile.LightObedience;
                    }

                    var state = lights.GetGroupState(node.TrafficLightGroup);
                    if (state != LightState.Green)
                    {
                        s.RedLightAhead = true;
                        s.LightDistance = dist;
                    }
                    break;
                }
            }

            // PERCEPCIÓN MUTUA: el peor conflicto con cualquier vecino, venga
            // de donde venga. Los bigotes de arriba solo miran hacia adelante;
            // en un cruce o un redondel el otro llega DE COSTADO y no lo ven.
            float peorTtc = float.MaxValue;
            NpcDriver enConflicto = null;
            Vector2 miPos = NpcConflictMath.Plano(transform.position);
            Vector2 miVel = NpcConflictMath.Plano(Velocidad);
            for (int i = 0; i < _vivos.Count; i++)
            {
                var otro = _vivos[i];
                if (otro == null || otro == this) continue;

                Vector3 delta = otro.transform.position - transform.position;
                if (Mathf.Abs(delta.y) > 4f) continue; // paso a desnivel: no es mi problema
                if (delta.sqrMagnitude > NpcConflictMath.RadioVecinos * NpcConflictMath.RadioVecinos) continue;

                float ttc = NpcConflictMath.TiempoAColision(
                    NpcConflictMath.Plano(otro.transform.position) - miPos,
                    NpcConflictMath.Plano(otro.Velocidad) - miVel,
                    NpcConflictMath.RadioConflicto);
                if (ttc >= peorTtc) continue;

                peorTtc = ttc;
                enConflicto = otro;
            }
            s.NeighborTimeToCollision = peorTtc;
            s.NeighborIsOnMyRight = enConflicto != null && NpcConflictMath.PorLaDerecha(
                transform.right, enConflicto.transform.position - transform.position);

            // ¿Y me toca ceder a mí? Cede quien llega DESPUÉS al punto donde se
            // cruzan las trayectorias (desempate por id si llegan a la vez).
            // Si las trayectorias no llegan a cruzarse (paralelas, o el cruce
            // ya quedó atrás) NADIE cede por esta regla: de eso se encarga el
            // seguimiento normal al de adelante.
            s.NeighborHasPriority = false;
            if (enConflicto != null && NpcConflictMath.TiemposAlCruce(
                    miPos, miVel,
                    NpcConflictMath.Plano(enConflicto.transform.position),
                    NpcConflictMath.Plano(enConflicto.Velocidad),
                    out float miT, out float suT))
            {
                s.NeighborHasPriority = NpcConflictMath.DeboCeder(
                    miT, suT, GetInstanceID(), enConflicto.GetInstanceID());
            }

            // Señal de CEDA próxima en la ruta: se cede solo si hay tráfico
            // moviéndose en el cruce (dos parados cediéndose no es tránsito).
            if (_path != null)
            {
                for (int i = _pathIndex; i < Mathf.Min(_pathIndex + 2, _path.Count); i++)
                {
                    var node = _path[i];
                    if (node.Sign != SignType.Yield) continue;
                    if (Vector3.Distance(transform.position, node.Position) > 12f) break;
                    s.YieldAhead = MovingTrafficNear(node.Position, 10f);
                    break;
                }
            }
            return s;
        }

        /// <summary>¿La ruta inmediata pasa por un semáforo o un ceda? (ahí no
        /// se estaciona nadie en doble fila: sería tapar el redondel).</summary>
        private bool NearTrafficControl()
        {
            if (_path == null) return false;
            for (int i = _pathIndex; i < Mathf.Min(_pathIndex + 3, _path.Count); i++)
                if (_path[i].TrafficLightGroup >= 0 || _path[i].Sign == SignType.Yield)
                    return true;
            return false;
        }

        /// <summary>¿Hay un vehículo EN MOVIMIENTO cerca de un punto (el cruce)?</summary>
        private bool MovingTrafficNear(Vector3 point, float radius)
        {
            foreach (var col in Physics.OverlapSphere(point, radius))
            {
                var rb = col.attachedRigidbody;
                if (rb == null || rb == _rb) continue;

                var npc = rb.GetComponent<NpcDriver>();
                float speed = npc != null ? npc.Speed : rb.linearVelocity.magnitude;
                if (speed > 1f) return true;
            }
            return false;
        }

        // ---------------- Movimiento cinemático ----------------

        private void Move(float dt)
        {
            if (_path == null || _pathIndex >= _path.Count || Speed < 0.01f)
            {
                StickToGround(); // detenido/recién nacido: no flotar
                SpinWheels(dt);
                return;
            }

            // El nodo se guarda aparte porque más abajo se consulta SU semáforo
            // (el target puede desplazarse al adelantar y ya no ser el nodo).
            var nodoActual = _path[_pathIndex];
            Vector3 target = nodoActual.Position;

            // ADELANTAMIENTO: el cerebro ya decidió que se puede rodear al que
            // está parado, pero rodear es MOVERSE A UN LADO — con solo bajar la
            // velocidad el NPC seguía embistiendo la misma línea. Se corre el
            // objetivo hacia el carril izquierdo mientras dure el estado.
            if (CurrentState == NpcState.Overtaking)
            {
                Vector3 haciaTarget = target - transform.position;
                haciaTarget.y = 0f;
                if (haciaTarget.sqrMagnitude > 0.01f)
                {
                    Vector3 izq = -Vector3.Cross(Vector3.up, haciaTarget.normalized).normalized;
                    target += izq * OvertakeOffset;
                }
            }

            Vector3 flatDir = target - transform.position;
            flatDir.y = 0f;

            // No saltar al siguiente waypoint mientras el semáforo de ESTE
            // nodo siga en rojo/amarillo y el NPC decidió respetarlo (ver
            // NpcWaypointHold): saltar aquí es lo que soltaba el freno a
            // metros de la línea y dejaba cruzar en rojo acelerando.
            bool esperaSemaforo = false;
            if (nodoActual.TrafficLightGroup >= 0 && TrafficLightController.Instance != null)
            {
                var estado = TrafficLightController.Instance.GetGroupState(nodoActual.TrafficLightGroup);
                esperaSemaforo = NpcWaypointHold.EsperaSemaforo(nodoActual.TrafficLightGroup, _obeysThisLight, estado);
            }

            if (flatDir.magnitude < 3f && !esperaSemaforo)
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count) PlanNewRoute();
                return;
            }

            var look = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
            var rot = NpcTurnLimit.Steer(_rb.rotation, look, Speed, dt);
            Vector3 pos = _rb.position + rot * Vector3.forward * Speed * dt;

            // Pegar el auto a la superficie de la vía (cuestas incluidas).
            if (TryGroundHeight(pos, out float gy)) pos.y = gy;

            _rb.MoveRotation(rot);
            _rb.MovePosition(pos);
            SpinWheels(dt);
        }

        /// <summary>Apoya el auto sobre la superficie (vía o suelo) bajo su pivote.</summary>
        private void StickToGround()
        {
            Vector3 p = transform.position;
            if (TryGroundHeight(p, out float gy))
            {
                p.y = gy;
                transform.position = p;
                if (_rb != null) _rb.position = p;
            }
        }

        /// <summary>
        /// Altura a la que debe ir el pivote para apoyarse en la VÍA que tiene
        /// DEBAJO, IGNORANDO el propio auto. Suma el offset del pivote.
        /// Doble blindaje contra el bug de playtest "los autos vuelan":
        /// (1) el rayo arranca APENAS por encima del auto (~1.5 m, no 6 m) para
        /// no agarrar estructuras que pasan por ARRIBA — la rampa de un paso a
        /// desnivel, el techo de un edificio re-asentado o la autopista elevada
        /// de la ciudad (con el origen alto tomaba la superficie más alta del
        /// rango y SUBÍA el auto por los aires); las cuestas suben poquísimo por
        /// paso de física, así que las sigue igual.
        /// (2) entre las superficies bajo el auto, NpcGroundMath.TryPick elige la
        /// PISABLE (la vía sobre el relleno) y DESCARTA techos de arcos, vallas o
        /// puentes por escalón máximo desde donde está.
        /// </summary>
        private bool TryGroundHeight(Vector3 at, out float y)
        {
            y = at.y;
            var hits = Physics.RaycastAll(at + Vector3.up * 1.5f, Vector3.down, 42f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var surfaces = new List<float>(hits.Length);
            var normals = new List<Vector3>(hits.Length);
            var aceras = new List<bool>(hits.Length);
            foreach (var h in hits)
            {
                if (h.collider.transform.root == transform.root) continue; // no a sí mismo

                // NI OTRO VEHÍCULO (playtest: "hay NPCs que se suben encima de
                // otros carros"). El filtro de altura no basta: MaxStepUp son
                // 1.6 m y un auto de Toon City mide ~1.5 m, así que el TECHO de
                // otro coche entraba como "piso pisable" y el NPC se le montaba.
                // El suelo es la vía; un auto nunca es suelo.
                if (EsVehiculo(h.collider)) continue;

                surfaces.Add(h.point.y);
                normals.Add(h.normal);
                aceras.Add(EsAcera(h.collider.transform));
            }

            float currentGround = transform.position.y - _groundOffset;
            // La ACERA no es suelo mientras haya calzada (playtest: "se suben a
            // las veredas"): en una esquina la vereda se apoya encima de la
            // pieza de calle, así que "lo más alto pisable" era el bordillo.
            if (!NpcGroundMath.TryPickCalzada(surfaces, aceras, currentGround, out float picked)) return false;
            y = picked + _groundOffset;

            // Guardar la normal de la vía elegida: el sensor frontal la usa
            // para no clavarse en el propio asfalto en pendiente (Sense()).
            int idx = surfaces.IndexOf(picked);
            _normalPiso = idx >= 0 ? normals[idx] : Vector3.up;
            return true;
        }

        /// <summary>¿Esta superficie es acera? El nombre puede estar en la
        /// pieza o en cualquier padre: los builders agrupan ("Vereda_*") y Toon
        /// City usa los prefabs "Pavement_*".</summary>
        private static bool EsAcera(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                string n = p.name;
                if (n.IndexOf("Pavement", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Vereda", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>¿Este collider es de un auto (NPC o jugador)? Los autos no
        /// son suelo: nadie se apoya encima de otro.</summary>
        private static bool EsVehiculo(Collider col)
        {
            if (col == null) return false;
            var root = col.transform.root;
            return root.GetComponentInChildren<NpcDriver>() != null ||
                   root.GetComponentInChildren<Vehicle.VehicleController>() != null;
        }

        private Bounds SelfBounds()
        {
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(transform.position, Vector3.one);
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return b;
        }

        private void SpinWheels(float dt)
        {
            float deg = Speed / 0.38f * Mathf.Rad2Deg * dt;
            foreach (var w in _wheels) w.Rotate(deg, 0f, 0f, Space.Self);
        }

        // El jugador nos chocó: quedarse detenido un momento (obstáculo realista).
        private void OnCollisionEnter(Collision c)
        {
            if (c.relativeVelocity.magnitude > 2f)
            {
                CurrentState = NpcState.Crashed;
                _stateTimer = 3f;
            }
        }

        // Ruta visible al seleccionar el NPC (defensa del A* ante el profe).
        private void OnDrawGizmosSelected()
        {
            if (_path == null) return;
            Gizmos.color = Color.magenta;
            for (int i = Mathf.Max(_pathIndex - 1, 0); i < _path.Count - 1; i++)
                Gizmos.DrawLine(_path[i].Position + Vector3.up * 0.5f,
                                _path[i + 1].Position + Vector3.up * 0.5f);
        }
    }
}
