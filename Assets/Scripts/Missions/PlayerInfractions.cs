using System.Collections.Generic;
using UnityEngine;
using HablaCamaron.Vehicle;
using HablaCamaron.World;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// El "inspector" a bordo: cuenta lo que el jugador hace mal (y bien) durante
    /// la misión, escuchando los eventos REALES del auto y vigilando los
    /// semáforos del grafo. MissionRunner lo lee al final para la evaluación.
    /// Va montado en el GameObject del Aveo (lo agrega MissionRunner).
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class PlayerInfractions : MonoBehaviour
    {
        public int Stalls { get; private set; }
        public int Grinds { get; private set; }
        public int RedLightsRun { get; private set; }
        public int SpeedingTickets { get; private set; }
        public int Collisions { get; private set; }
        public int MissedBlinkers => _turnJudge.Misses;
        public int CrosswalkBlocks { get; private set; }
        public int WrongWays { get; private set; }
        public int OffRoads { get; private set; }

        /// <summary>Cruzó un semáforo en rojo (muerte súbita, misiones StrictRules).</summary>
        public event System.Action OnRedLightCrossed;

        /// <summary>Contravía sostenida (la detecta RoadDiscipline con LaneJudge).</summary>
        public void AddWrongWay() => WrongWays++;

        /// <summary>Se salió de las calles y hubo que devolverlo (RoadDiscipline).</summary>
        public void AddOffRoad() => OffRoads++;

        /// <summary>Margen de tolerancia sobre el límite señalizado (km/h).</summary>
        public const float SpeedTolerance = 5f;

        private VehicleController _car;
        private readonly HashSet<int> _lightNodesPunished = new HashSet<int>();
        private readonly HashSet<int> _speedNodesPunished = new HashSet<int>();
        private readonly TurnSignalJudge _turnJudge = new TurnSignalJudge();
        private readonly CrosswalkJudge _crosswalkJudge = new CrosswalkJudge();
        private Crosswalk[] _crosswalks;
        private float _collisionCd;
        private float _lastYaw;

        private void Awake()
        {
            _car = GetComponent<VehicleController>();
            _lastYaw = transform.eulerAngles.y;
            // Las cebras de la zona (las pintan los builders; puede no haber).
            _crosswalks = FindObjectsByType<Crosswalk>(FindObjectsSortMode.None);
        }

        private void OnEnable()
        {
            _car.OnStalled += CountStall;
            _car.OnGearGrind += CountGrind;
        }

        private void OnDisable()
        {
            _car.OnStalled -= CountStall;
            _car.OnGearGrind -= CountGrind;
        }

        private void CountStall() => Stalls++;
        private void CountGrind() => Grinds++;

        private void Update()
        {
            _collisionCd -= Time.deltaTime;
            WatchRedLights();
            WatchSpeedLimits();
            WatchTurnSignals();
            WatchCrosswalks();
        }

        // Bloquear un paso cebra (detenido ENCIMA con el semáforo en rojo):
        // la regla pura acumula la estadía y castiga una vez (−2 en señales).
        private void WatchCrosswalks()
        {
            if (_crosswalks == null || _crosswalks.Length == 0) return;
            var lights = TrafficLightController.Instance;

            bool encima = false, enRojo = false;
            foreach (var cw in _crosswalks)
            {
                if (cw == null || !cw.Contains(transform.position)) continue;
                encima = true;
                enRojo = lights != null &&
                         lights.GetGroupState(cw.LightGroup) == LightState.Red;
                break;
            }

            if (_crosswalkJudge.Tick(encima, enRojo, _car.SpeedKmh, Time.deltaTime))
            {
                CrosswalkBlocks++;
                UI.DonPanchoDialogue.Instance?.Trigger(UI.DialogueEvent.CrosswalkBlock);
            }
        }

        // Girar sin direccional (GDD): el juez puro acumula el yaw real del
        // auto y castiga giros francos sin haber avisado. Solo en movimiento.
        private void WatchTurnSignals()
        {
            float yaw = transform.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(_lastYaw, yaw);
            _lastYaw = yaw;

            if (_turnJudge.Tick(deltaYaw, _car.BlinkerState,
                                _car.SpeedKmh >= 8f, Time.deltaTime))
                UI.DonPanchoDialogue.Instance?.Trigger(UI.DialogueEvent.MissedBlinker);
        }

        // Pasarse un semáforo: cruzar cerca de un nodo con grupo en rojo, en
        // movimiento. Cada semáforo castiga UNA vez por pasada (sin ametrallar).
        private void WatchRedLights()
        {
            var graph = RoadGraph.Instance;
            var lights = TrafficLightController.Instance;
            if (graph == null || lights == null || _car.SpeedKmh < 8f) return;

            foreach (var node in graph.Data.Nodes)
            {
                if (node.TrafficLightGroup < 0) continue;
                float sq = (node.Position - transform.position).sqrMagnitude;

                if (sq > 30f)
                {
                    _lightNodesPunished.Remove(node.Id); // ya se alejó: rearmar
                    continue;
                }
                if (_lightNodesPunished.Contains(node.Id)) continue;

                if (lights.GetGroupState(node.TrafficLightGroup) == LightState.Red)
                {
                    // Llegar frenando cerca de la línea NO es infracción: el
                    // juez puro castiga recién al CRUZAR el nodo en rojo.
                    // Mientras el nodo siga adelante, se sigue observando.
                    if (!RedLightJudge.CrossedNode(transform.position,
                                                   transform.forward, node.Position))
                        continue;
                    _lightNodesPunished.Add(node.Id);
                    RedLightsRun++;
                    UI.DonPanchoDialogue.Instance?.Trigger(UI.DialogueEvent.RedLightRun);
                    OnRedLightCrossed?.Invoke();
                }
                else
                {
                    _lightNodesPunished.Add(node.Id); // pasó en verde: no re-evaluar
                }
            }
        }

        // Pasar embalado junto a una señal de límite: multa UNA vez por pasada
        // (mismo patrón de rearme que los semáforos). Margen de SpeedTolerance.
        private void WatchSpeedLimits()
        {
            var graph = RoadGraph.Instance;
            if (graph == null) return;

            foreach (var node in graph.Data.Nodes)
            {
                int limit = RoadSign.LimitKmh(node.Sign);
                if (limit <= 0) continue;
                float sq = (node.Position - transform.position).sqrMagnitude;

                if (sq > 144f) // fuera de la zona de la señal (12 m): rearmar
                {
                    _speedNodesPunished.Remove(node.Id);
                    continue;
                }
                if (_speedNodesPunished.Contains(node.Id)) continue;

                _speedNodesPunished.Add(node.Id); // se evalúa una vez por pasada
                if (_car.SpeedKmh > limit + SpeedTolerance)
                {
                    SpeedingTickets++;
                    UI.DonPanchoDialogue.Instance?.Trigger(UI.DialogueEvent.Speeding);
                }
            }
        }

        // Choques contra NPCs o el entorno (con colchón para no contar rebotes).
        private void OnCollisionEnter(Collision c)
        {
            if (_collisionCd > 0f) return;
            if (c.relativeVelocity.magnitude < 2.5f) return;

            _collisionCd = 1.5f;
            Collisions++;
            UI.DonPanchoDialogue.Instance?.Trigger(UI.DialogueEvent.NearCrash);
        }

        public MissionStats ToStats(bool reachedGoal, float timeUsed, float timeLimit) =>
            new MissionStats
            {
                ReachedGoal = reachedGoal,
                TimeUsed = timeUsed,
                TimeLimit = timeLimit,
                Stalls = Stalls,
                Grinds = Grinds,
                RedLightsRun = RedLightsRun,
                SpeedingTickets = SpeedingTickets,
                Collisions = Collisions,
                MissedBlinkers = MissedBlinkers,
                CrosswalkBlocks = CrosswalkBlocks,
                WrongWays = WrongWays,
                OffRoads = OffRoads,
            };
    }
}
