using UnityEngine;
using HablaCamaron.UI;

namespace HablaCamaron.Vehicle
{
    /// <summary>
    /// Conecta la física real del auto con la UI y el audio:
    ///  - HUD: velocidad, RPM, marcha (con destello al cambiar), freno de mano,
    ///    direccional parpadeante, motor calado.
    ///  - Feedback por acción: toast + sonido procedural para cada cosa que el
    ///    jugador hace (cambio, luces, freno de mano, encendido...).
    ///  - Don Pancho: reacciona a los eventos reales con cooldowns.
    ///  - Crea automáticamente el cluster de pedales, el tutor de arranque y el
    ///    panel de ayuda (H) — no hay que armar nada en el editor.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleHUDBridge : MonoBehaviour
    {
        private VehicleController _car;
        private EngineAudio _audio;
        private DrivingFeedbackHUD _feedback;

        // Parpadeo del direccional en el HUD.
        private float _blinkTimer;
        private bool _blinkVisible;
        private int _lastBlinker;

        // Detección de cambios para dar feedback (se comparan por frame).
        private int _lastLights;
        private bool _lastHandbrake;

        // Cooldowns para no saturar (segundos).
        private float _goodShiftCd, _hillHintCd, _handbrakeWarnCd;
        private int _cleanShifts;

        private void Awake()
        {
            _car = GetComponent<VehicleController>();
            _audio = GetComponent<EngineAudio>();

            // Auto-crear los ayudantes visuales si no existen (escenas ya generadas
            // los reciben gratis, sin regenerar nada).
            _feedback = GetComponent<DrivingFeedbackHUD>();
            if (_feedback == null) _feedback = gameObject.AddComponent<DrivingFeedbackHUD>();
            _feedback.Car = _car;

            if (GetComponent<StartupTutor>() == null) gameObject.AddComponent<StartupTutor>();

            _lastHandbrake = _car.HandbrakeOn;
        }

        private void OnEnable()
        {
            _car.OnStalled += HandleStalled;
            _car.OnEngineStarted += HandleEngineStarted;
            _car.OnGearChanged += HandleGearChanged;
            _car.OnGearGrind += HandleGearGrind;
            _car.OnIgnitionBlocked += HandleIgnitionBlocked;
        }

        private void OnDisable()
        {
            _car.OnStalled -= HandleStalled;
            _car.OnEngineStarted -= HandleEngineStarted;
            _car.OnGearChanged -= HandleGearChanged;
            _car.OnGearGrind -= HandleGearGrind;
            _car.OnIgnitionBlocked -= HandleIgnitionBlocked;
        }

        private void Start()
        {
            // En escena con misión el saludo lo da MissionRunner al cerrar el
            // briefing (aquí sonaría dos veces); esto cubre el manejo libre.
            if (FindFirstObjectByType<Missions.MissionRunner>() == null)
                DonPanchoDialogue.Instance?.Trigger(DialogueEvent.MissionStart);
        }

        private void Update()
        {
            var hud = HUDController.Instance;
            if (hud != null)
            {
                hud.SetSpeed(_car.SpeedKmh);
                hud.SetRpm(_car.Rpm01);
                hud.SetGear(_car.GearLabel);
                hud.SetHandbrake(_car.HandbrakeOn);
                hud.SetStalled(_car.IsStalled);
                UpdateBlinker(hud);
            }

            DetectStateChanges();
            UpdateHillHint();
            UpdateHandbrakeWarning();

            _goodShiftCd -= Time.deltaTime;
            _hillHintCd -= Time.deltaTime;
            _handbrakeWarnCd -= Time.deltaTime;
        }

        // ---------- Feedback de acciones sin evento propio (se detectan por frame) ----------

        private void DetectStateChanges()
        {
            if (_car.LightsState != _lastLights)
            {
                _lastLights = _car.LightsState;
                string[] names = { "apagadas", "cortas", "LARGAS" };
                _feedback.Toast("Luces: " + names[_lastLights]);
            }

            if (_car.HandbrakeOn != _lastHandbrake)
            {
                _lastHandbrake = _car.HandbrakeOn;
                _audio?.Play(EngineAudio.Fx.Handbrake);
                _feedback.Toast(_car.HandbrakeOn ? "Freno de mano PUESTO" : "Freno de mano quitado");
            }
        }

        // Aviso si intenta arrancar con el freno de mano puesto (error clásico).
        private void UpdateHandbrakeWarning()
        {
            if (_handbrakeWarnCd > 0f) return;
            if (_car.HandbrakeOn && _car.ThrottleInput > 0.4f && _car.EngineOn && _car.Gear != 0)
            {
                HUDController.Instance?.ShowMessage("¡El freno de mano está puesto, camarón! Suéltalo con Espacio.", 3.5f);
                _handbrakeWarnCd = 8f;
            }
        }

        // El ícono del direccional parpadea como uno real (no queda fijo).
        private void UpdateBlinker(HUDController hud)
        {
            if (_car.BlinkerState == 0)
            {
                if (_lastBlinker != 0)
                {
                    hud.SetBlinker(false);
                    _feedback.Toast("Direccional apagada");
                    _lastBlinker = 0;
                }
                return;
            }

            if (_lastBlinker != _car.BlinkerState)
            {
                _lastBlinker = _car.BlinkerState;
                _blinkTimer = 0f;
                _blinkVisible = true;
                // La flecha del HUD apunta hacia el lado señalizado.
                hud.SetBlinker(true, _car.BlinkerState);
                _feedback.Toast(_car.BlinkerState < 0 ? "Direccional ← izquierda" : "Direccional derecha →");
            }

            _blinkTimer += Time.deltaTime;
            if (_blinkTimer >= 0.5f)
            {
                _blinkTimer = 0f;
                _blinkVisible = !_blinkVisible;
                hud.SetBlinker(_blinkVisible, _car.BlinkerState);
            }
        }

        // Consejo de Don Pancho al detenerse en una pendiente (misión clave del juego).
        private void UpdateHillHint()
        {
            if (_hillHintCd > 0f) return;
            if (_car.SpeedKmh < 2f && _car.PitchDegrees() > 5f && _car.EngineOn)
            {
                DonPanchoDialogue.Instance?.Trigger(DialogueEvent.HillStart);
                _hillHintCd = 30f;
            }
        }

        // ---------------- Reacciones a eventos del auto ----------------

        private void HandleStalled()
        {
            _audio?.Play(EngineAudio.Fx.Stall);
            DonPanchoDialogue.Instance?.Trigger(DialogueEvent.StallEngine);
        }

        private void HandleEngineStarted()
        {
            _audio?.Play(EngineAudio.Fx.Ignition);
            _feedback.Toast("Motor encendido");
        }

        private void HandleGearChanged(int gear)
        {
            _audio?.Play(EngineAudio.Fx.Shift);
            HUDController.Instance?.FlashGear(true);
            _feedback.Toast(gear switch
            {
                -1 => "Marcha: REVERSA",
                0 => "Marcha: neutro",
                _ => $"Marcha: {gear}ª"
            });

            // Celebrar de vez en cuando los cambios limpios (no cada uno: satura).
            _cleanShifts++;
            if (gear > 1 && _goodShiftCd <= 0f && _cleanShifts % 4 == 0)
            {
                DonPanchoDialogue.Instance?.Trigger(DialogueEvent.GoodShift);
                _goodShiftCd = 20f;
            }
        }

        private void HandleGearGrind()
        {
            _audio?.Play(EngineAudio.Fx.Grind);
            HUDController.Instance?.FlashGear(false);
            _feedback.Toast("¡Pisa el embrague (Shift) para cambiar!");
            DonPanchoDialogue.Instance?.Trigger(DialogueEvent.BadShift);
        }

        private void HandleIgnitionBlocked()
        {
            _feedback.Toast("Está en marcha: pisa el embrague o pon N");
            HUDController.Instance?.ShowMessage("Pisa el embrague (Shift) o pon neutro (N) para encender.", 4f);
        }
    }
}
