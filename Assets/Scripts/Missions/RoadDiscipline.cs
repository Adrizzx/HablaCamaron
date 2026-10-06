using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.UI;
using HablaCamaron.Vehicle;
using HablaCamaron.World;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// La disciplina vial del jugador (pedido del playtest: "no debe dejar ir
    /// en el carril contrario" y "solo por las calles"). Consulta al juez puro
    /// LaneJudge cada frame y aplica las consecuencias:
    /// - CONTRAVÍA sostenida → aviso rojo en el HUD, reclamo de Don Pancho e
    ///   infracción de señales (−4, como pasarse un rojo). Reincide cada tanto.
    /// - FUERA DE LA VÍA → cuenta regresiva "VUELVE A LA CALLE" y, si no hace
    ///   caso, el auto vuelve solo al punto de vía más cercano (con su
    ///   infracción de defensiva). El garaje del arranque no cuenta: recién
    ///   se juzga después de haber pisado la calle una vez.
    /// Lo agrega MissionRunner junto a PlayerInfractions.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class RoadDiscipline : MonoBehaviour
    {
        /// <summary>Segundos sostenidos en contravía antes del castigo.</summary>
        public const float WrongWayGrace = 2.5f;

        /// <summary>Segundos entre castigos si sigue en contravía.</summary>
        public const float WrongWayRepeat = 8f;

        /// <summary>Segundos fuera de la vía antes de empezar la cuenta.</summary>
        public const float OffRoadGrace = 3f;

        /// <summary>Segundos fuera de la vía para devolverlo a la calle.</summary>
        public const float OffRoadReturnAt = 9f;

        /// <summary>Sólo se juzga contravía en marcha franca (km/h).</summary>
        public const float MinSpeedKmh = 10f;

        /// <summary>El veredicto de carril del frame actual (muerte súbita, StrictRuleJudge).
        /// OnRoad mientras no haya grafo (no hay nada que juzgar).</summary>
        public LaneVerdict CurrentVerdict { get; private set; } = LaneVerdict.OnRoad;

        private VehicleController _car;
        private PlayerInfractions _infractions;
        private Rigidbody _rb;
        private float _wrongTime, _offTime, _nextWrongPunish;
        private bool _wasOnRoad; // ya pisó la calle (el garaje inicial no juzga)

        private void Awake()
        {
            _car = GetComponent<VehicleController>();
            _rb = GetComponent<Rigidbody>();
            _infractions = GetComponent<PlayerInfractions>();
            BuildWarnUI();
        }

        private void Update()
        {
            if (Time.timeScale == 0f) { Show(null); return; }

            var graph = RoadGraph.Instance;
            if (graph == null || graph.Data == null || graph.Data.Edges.Count == 0)
            { CurrentVerdict = LaneVerdict.OnRoad; Show(null); return; }

            // El rumbo REAL de avance (la velocidad); parado, la trompa.
            Vector3 dir = _rb.linearVelocity;
            if (dir.sqrMagnitude < 1f) dir = transform.forward;

            var verdict = LaneJudge.Judge(graph.Data, transform.position, dir);

            // Reversa de corrección: SpeedKmh es la MAGNITUD de la velocidad
            // (sin signo), así que retroceder rápido en el propio carril
            // apunta la velocidad en sentido contrario a la vía y el juez lo
            // marca WrongWay igual que una contravía real — pero dar reversa
            // para corregir una maniobra no es manejar en contravía.
            // ForwardSpeed (Vector3.Dot de la velocidad con transform.forward,
            // ya expuesto por VehicleController) negativo = el auto se mueve
            // para atrás respecto a su propia trompa: ahí el veredicto se
            // reporta OnRoad. Si en cambio avanza DE FRENTE por el carril
            // equivocado (ForwardSpeed positivo), el castigo de contravía de
            // abajo sigue intacto.
            bool reversing = _car.ForwardSpeed < -0.1f;
            if (verdict == LaneVerdict.WrongWay && reversing) verdict = LaneVerdict.OnRoad;

            // El juez estricto (StrictRuleJudge, muerte súbita) consume esta
            // señal cruda cada frame: debe respetar el MISMO umbral de
            // velocidad que ya protege el castigo de abajo (línea ~84) — si
            // no, un auto PARADO orientado en contra (cediendo el paso en el
            // redondel, un giro en tres puntos) acumula "contravía" sin
            // moverse y reprueba injustamente. Por debajo de MinSpeedKmh se
            // reporta OnRoad (el juez puro StrictRuleJudge no cambia).
            CurrentVerdict = (verdict == LaneVerdict.WrongWay && _car.SpeedKmh < MinSpeedKmh)
                ? LaneVerdict.OnRoad
                : verdict;

            if (verdict == LaneVerdict.OnRoad)
            {
                _wasOnRoad = true;
                _wrongTime = 0f; _offTime = 0f; _nextWrongPunish = WrongWayGrace;
                Show(null);
                return;
            }

            if (verdict == LaneVerdict.WrongWay)
            {
                _offTime = 0f;
                if (_car.SpeedKmh < MinSpeedKmh) { Show(null); return; }

                _wrongTime += Time.deltaTime;
                Show("¡CONTRAVÍA! CAMBIA DE CARRIL");
                if (_wrongTime >= _nextWrongPunish)
                {
                    _nextWrongPunish = _wrongTime + WrongWayRepeat;
                    _infractions?.AddWrongWay();
                    DonPanchoDialogue.Instance?.Trigger(DialogueEvent.WrongWay);
                }
                return;
            }

            // Fuera de la vía: sólo cuenta si ya estuvo en la calle.
            _wrongTime = 0f; _nextWrongPunish = WrongWayGrace;
            if (!_wasOnRoad) { Show(null); return; }

            _offTime += Time.deltaTime;
            if (_offTime < OffRoadGrace) return;

            int left = Mathf.CeilToInt(OffRoadReturnAt - _offTime);
            Show($"VUELVE A LA CALLE — {Mathf.Max(left, 0)}");
            if (_offTime >= OffRoadReturnAt) ReturnToRoad();
        }

        /// <summary>De vuelta al punto de vía más cercano, mirando con el carril.</summary>
        private void ReturnToRoad()
        {
            _offTime = 0f;
            var graph = RoadGraph.Instance;
            if (graph == null ||
                !LaneJudge.NearestLanePoint(graph.Data, transform.position,
                                            out var point, out var laneDir)) return;

            var rot = Quaternion.LookRotation(laneDir, Vector3.up);
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(point + Vector3.up * 0.6f, rot);
            _rb.position = transform.position;
            _rb.rotation = rot;

            _infractions?.AddOffRoad();
            DonPanchoDialogue.Instance?.Trigger(DialogueEvent.OutOfBounds);
            Show(null);
        }

        // ---------------- Aviso en pantalla ----------------
        // Ya no se dibuja aquí: todo va al CANAL ÚNICO (UI.AlertBanner), que
        // muestra solo el aviso más urgente. Antes cada sistema pintaba en su
        // propia franja y contravía / fuera de ruta / guía / toasts se
        // solapaban en pantalla.

        private void BuildWarnUI() => AlertBanner.Ensure();

        private void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            // La contravía es lo más grave que vigila este componente; salirse
            // de la calzada es un aviso de cuidado.
            var prioridad = message.Contains("CONTRAVÍA")
                ? AlertPriority.Danger : AlertPriority.Warning;
            AlertBanner.Instance?.Show(message, prioridad);
        }
    }
}
