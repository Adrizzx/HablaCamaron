using System;
using UnityEngine;

namespace HablaCamaron.Vehicle
{
    /// <summary>
    /// Física del auto manual: motor con RPM, embrague que cala, caja de 5 marchas
    /// + N + R, freno de mano y dirección sensible a la velocidad.
    /// Es la mecánica central del juego ("dominar el embrague es el 50% de la campaña").
    ///
    /// Modelo simplificado pero coherente:
    ///  - Con el embrague pisado (o en N) el motor gira "libre" según el acelerador.
    ///  - Al soltar el pedal, las RPM del motor se acoplan a las de las ruedas.
    ///  - Si el acople arrastra las RPM por debajo de StallRpm → motor calado.
    ///  - Soltarlo gradual + algo de acelerador = arranque limpio. Eso se aprende.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Configuración")]
        public VehicleSpec Spec;

        [Header("Ruedas (las delanteras dirigen y traccionan — FWD)")]
        public WheelCollider WheelFL;
        public WheelCollider WheelFR;
        public WheelCollider WheelRL;
        public WheelCollider WheelRR;

        // ---- Entradas (las escribe VehicleInput cada frame, todas 0..1) ----
        [HideInInspector] public float ThrottleInput;
        [HideInInspector] public float BrakeInput;
        [HideInInspector] public float SteerInput;   // -1..1
        [HideInInspector] public float ClutchPedal;  // 1 = pisado a fondo (desacoplado)
        [HideInInspector] public bool HandbrakeOn = true; // arranca con freno de mano puesto

        // ---- Estado del auto ----
        public bool EngineOn { get; private set; }
        public bool IsStalled { get; private set; }
        public int Gear { get; private set; }          // -1 = R, 0 = N, 1..5
        public float Rpm { get; private set; }
        public int BlinkerState { get; private set; }  // -1 izq, 0 no, 1 der
        public int LightsState { get; private set; }   // 0 off, 1 cortas, 2 largas
        public bool HornOn { get; set; }

        // ---- Telemetría derivada ----
        public float SpeedKmh => _rb.linearVelocity.magnitude * 3.6f;
        public float Rpm01 => Spec == null ? 0f : Mathf.InverseLerp(0f, Spec.MaxRpm, Rpm);
        public float ForwardSpeed => Vector3.Dot(_rb.linearVelocity, transform.forward); // m/s con signo
        public float SlopeAngle => Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(transform.forward, Vector3.up));
        public string GearLabel => Gear switch { -1 => "R", 0 => "N", _ => Gear.ToString() };
        /// <summary>0..1: cuánto está "mordiendo" el embrague (0 = pedal a fondo).</summary>
        public float ClutchEngagement => Mathf.InverseLerp(Spec.BitePoint, 0f, ClutchPedal);

        // ---- Eventos para HUD, Don Pancho y el sistema de puntaje (Fase 3) ----
        public event Action OnStalled;                 // se caló el motor
        public event Action OnEngineStarted;
        public event Action<int> OnGearChanged;        // cambio limpio (con embrague)
        public event Action OnGearGrind;               // intentó cambiar sin embrague
        public event Action OnIgnitionBlocked;         // intentó encender en marcha sin embrague

        /// <summary>Tolerancia antes de calar (s): perdona el tirón de un instante.</summary>
        public const float StallGrace = 0.5f;
        /// <summary>Asistencia de ralentí: aporte de acelerador automático cuando
        /// el motor pelea por no morirse (como un auto real moderno).</summary>
        public const float IdleAssistThrottle = 0.22f;
        /// <summary>Resistencia al rodar (Nm/rueda): el auto no plancha eterno sin motor.</summary>
        public const float RollingBrake = 25f;

        private Rigidbody _rb;
        private float _steerAngle;    // ángulo actual suavizado
        private float _rpmFloat;      // RPM internas (float de trabajo)
        private float _stallTimer;    // acumula el tiempo en zona de calado
        private bool _wheelsMissingLogged;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (Spec != null)
            {
                _rb.mass = Spec.Mass;
                _rb.centerOfMass = Spec.CenterOfMass;
            }
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Un choque no debe mandarte de trompos por media cuadra:
            // amortiguación angular alta y tope de giro (arcade perdonador).
            _rb.angularDamping = 1.5f;
            _rb.maxAngularVelocity = 3f;
            Gear = 0;
            ClutchPedal = 1f; // arranca con el pedal pisado, como en la vida real
        }

        // ================= API pública (la llama VehicleInput) =================

        /// <summary>Encender o apagar el motor. Enseña: en marcha, primero pisa el embrague.</summary>
        public void ToggleIgnition()
        {
            if (EngineOn) { EngineOn = false; return; }

            // Seguridad real: no enciende en marcha con el embrague suelto.
            if (Gear != 0 && ClutchPedal < 0.7f)
            {
                OnIgnitionBlocked?.Invoke();
                return;
            }

            EngineOn = true;
            IsStalled = false;
            _rpmFloat = Spec.IdleRpm;
            OnEngineStarted?.Invoke();
        }

        /// <summary>Pedal de embrague necesario para que un cambio entre limpio.</summary>
        public const float ClutchShiftThreshold = 0.7f;

        /// <summary>¿El pedal ya está lo bastante pisado para cambiar? PURA (testeada).</summary>
        public static bool ClutchReady(float clutchPedal) => clutchPedal >= ClutchShiftThreshold;

        /// <summary>La tecla R como TOGGLE: en reversa vuelve a neutro (salir de
        /// reversa fácil); en cualquier otra marcha, mete reversa. PURA (testeada).</summary>
        public static int ReverseToggleTarget(int currentGear) => currentGear == -1 ? 0 : -1;

        /// <summary>Cambiar de marcha. Sin embrague pisado → rechinido y no entra.</summary>
        public void ShiftTo(int gear)
        {
            gear = Mathf.Clamp(gear, -1, Spec.GearRatios.Length);
            if (gear == Gear) return;

            if (!ClutchReady(ClutchPedal))
            {
                OnGearGrind?.Invoke();
                return;
            }
            // La reversa solo entra casi detenido (proteger la caja, como en la realidad).
            if (gear == -1 && SpeedKmh > 6f)
            {
                OnGearGrind?.Invoke();
                return;
            }

            Gear = gear;
            OnGearChanged?.Invoke(gear);
        }

        public void ToggleHandbrake() => HandbrakeOn = !HandbrakeOn;

        /// <summary>
        /// Cambia la ficha técnica EN CALIENTE (garaje de la Fase 4: la escena
        /// se genera con el Aveo y aquí se re-equipa como BT-50). Reaplica
        /// masa, centro de masa y suspensión — lo que Awake ya hizo del spec viejo.
        /// </summary>
        public void ApplySpec(VehicleSpec spec)
        {
            if (spec == null) return;
            Spec = spec;

            var rb = _rb != null ? _rb : GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = spec.Mass;
                rb.centerOfMass = spec.CenterOfMass;
            }

            foreach (var wheel in new[] { WheelFL, WheelFR, WheelRL, WheelRR })
            {
                if (wheel == null) continue;
                wheel.suspensionDistance = spec.SuspensionDistance;
                var spring = wheel.suspensionSpring;
                spring.spring = spec.SpringForce;
                spring.damper = spec.SpringDamper;
                wheel.suspensionSpring = spring;
            }
        }

        /// <summary>-1 izquierda, 1 derecha. Repetir la misma dirección la apaga.</summary>
        public void ToggleBlinker(int dir) => BlinkerState = (BlinkerState == dir) ? 0 : dir;

        /// <summary>
        /// Cambio SECUENCIAL (mando/volante: subir o bajar una marcha).
        /// PURA y testeada: recorre R(-1) → N(0) → 1..maxGear sin saltos ni
        /// salirse de la caja. La protección (embrague, reversa en movimiento)
        /// la sigue poniendo ShiftTo.
        /// </summary>
        public static int NextGear(int current, bool up, int maxGear) =>
            Mathf.Clamp(current + (up ? 1 : -1), -1, Mathf.Max(1, maxGear));

        public void CycleLights() => LightsState = (LightsState + 1) % 3;

        // ========================= Simulación =========================

        private void FixedUpdate()
        {
            if (Spec == null) return;
            if (WheelFL == null || WheelFR == null || WheelRL == null || WheelRR == null)
            {
                // Sin las 4 ruedas asignadas la física no puede aplicarse — mejor
                // quedarse quieto con UN aviso que un NullReferenceException por
                // cada FixedUpdate (silencioso en un build empaquetado).
                if (!_wheelsMissingLogged)
                {
                    Debug.LogError("[Habla Camarón] VehicleController sin las 4 ruedas asignadas: física desactivada.");
                    _wheelsMissingLogged = true;
                }
                return;
            }
            float dt = Time.fixedDeltaTime;

            SimulateEngine(dt);
            ApplySteering(dt);
            ApplyDrive();
            ApplyBrakes();
        }

        private void SimulateEngine(float dt)
        {
            if (!EngineOn)
            {
                _rpmFloat = Mathf.MoveTowards(_rpmFloat, 0f, Spec.EngineResponse * 1.5f * dt);
                Rpm = _rpmFloat;
                return;
            }

            float engagement = ClutchEngagement;
            bool coupled = Gear != 0 && engagement > 0.02f;

            // RPM "libres": lo que el motor quiere girar según el acelerador.
            float freeTarget = Mathf.Lerp(Spec.IdleRpm, Spec.MaxRpm, EffectiveThrottle());

            float target;
            if (!coupled)
            {
                target = freeTarget;
            }
            else
            {
                // Acoplado: las ruedas mandan. Cuanto más suelto el pedal, más mandan.
                float wheelRpm = Mathf.Abs(DrivenWheelRpm()) * CurrentRatio();
                target = Mathf.Lerp(freeTarget, wheelRpm, Mathf.Pow(engagement, 0.75f));
            }

            _rpmFloat = Mathf.MoveTowards(_rpmFloat, target, Spec.EngineResponse * dt);
            _rpmFloat = Mathf.Min(_rpmFloat, Spec.MaxRpm);
            Rpm = _rpmFloat;

            // EL MOMENTO PEDAGÓGICO: embrague suelto + RPM bajas = motor calado.
            // Con una gracia corta: el tirón de un instante se perdona, el
            // descuido sostenido no (más justo de jugar, misma lección).
            if (coupled && engagement > 0.55f && _rpmFloat < Spec.StallRpm)
            {
                _stallTimer += dt;
                if (_stallTimer >= StallGrace) Stall();
            }
            else _stallTimer = 0f;
        }

        private void Stall()
        {
            EngineOn = false;
            IsStalled = true;
            _rpmFloat = 0f;
            OnStalled?.Invoke();
        }

        private void ApplySteering(float dt)
        {
            // A más velocidad, menos ángulo: en teclado esto evita trompos injustos.
            float speedFactor = Mathf.InverseLerp(0f, Spec.SteerSpeedKmhRef, SpeedKmh);
            float maxAngle = Mathf.Lerp(Spec.MaxSteerAngle, Spec.SteerAngleAtSpeed, speedFactor);

            _steerAngle = Mathf.Lerp(_steerAngle, SteerInput * maxAngle, Spec.SteerLerpSpeed * dt);
            WheelFL.steerAngle = _steerAngle;
            WheelFR.steerAngle = _steerAngle;
        }

        private void ApplyDrive()
        {
            float torquePerWheel = 0f;

            if (EngineOn && Gear != 0)
            {
                float engagement = ClutchEngagement;
                float engineTorque = Spec.TorqueCurve.Evaluate(Rpm01) * Spec.MaxTorque * EffectiveThrottle();

                // Corte de inyección en la zona roja: obliga a cambiar de marcha.
                if (Rpm >= Spec.MaxRpm * 0.99f) engineTorque = 0f;

                float ratio = CurrentRatio();
                float sign = Gear == -1 ? -1f : 1f;
                torquePerWheel = sign * engineTorque * ratio * Spec.DrivetrainEfficiency * engagement * 0.5f;

                // Retención del motor (freno motor) al soltar el acelerador acoplado.
                if (ThrottleInput < 0.05f && engagement > 0.5f && Mathf.Abs(ForwardSpeed) > 0.5f)
                    torquePerWheel -= Mathf.Sign(ForwardSpeed) * Spec.EngineBrake * 0.5f;
            }
            else if (Gear != 0 && ClutchEngagement > 0.3f && Mathf.Abs(ForwardSpeed) > 0.3f)
            {
                // Motor APAGADO o calado con el embrague acoplado: el motor
                // muerto arrastra — el auto NO sigue avanzando como si nada.
                torquePerWheel = -Mathf.Sign(ForwardSpeed) * Spec.EngineBrake * 1.5f;
            }

            WheelFL.motorTorque = torquePerWheel;
            WheelFR.motorTorque = torquePerWheel;
            WheelRL.motorTorque = 0f;
            WheelRR.motorTorque = 0f;
        }

        private void ApplyBrakes()
        {
            float perWheel = BrakeInput * Spec.BrakeTorque * 0.25f;
            // Resistencia al rodar: sin acelerador, el auto pierde velocidad
            // solo (suave: en pendiente igual rueda hacia atrás — pedagogía).
            if (ThrottleInput < 0.05f) perWheel += RollingBrake;
            float rear = perWheel + (HandbrakeOn ? Spec.HandbrakeTorque * 0.5f : 0f);

            WheelFL.brakeTorque = perWheel;
            WheelFR.brakeTorque = perWheel;
            WheelRL.brakeTorque = rear;
            WheelRR.brakeTorque = rear;
        }

        /// <summary>
        /// Acelerador efectivo: con el embrague mordiendo, poco acelerador y
        /// RPM cayendo hacia el ralentí, el motor "se ayuda" solo (control de
        /// ralentí de un auto real). Soltar el pedal DE GOLPE sigue calando:
        /// el acople manda las RPM a cero más rápido de lo que esto compensa.
        /// </summary>
        private float EffectiveThrottle()
        {
            bool needsHelp = EngineOn && Gear != 0 && ClutchEngagement > 0.1f &&
                             ThrottleInput < IdleAssistThrottle &&
                             Rpm < Spec.IdleRpm * 1.35f;
            return needsHelp ? IdleAssistThrottle : ThrottleInput;
        }

        private float CurrentRatio()
        {
            float g = Gear == -1 ? Spec.ReverseRatio : Spec.GearRatios[Mathf.Clamp(Gear - 1, 0, Spec.GearRatios.Length - 1)];
            return g * Spec.FinalDrive;
        }

        private float DrivenWheelRpm() => (WheelFL.rpm + WheelFR.rpm) * 0.5f;

        /// <summary>Inclinación de la vía bajo el auto en grados (+ subiendo, - bajando).</summary>
        public float PitchDegrees()
        {
            float pitch = Vector3.SignedAngle(
                Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized,
                transform.forward,
                transform.right);
            return -pitch; // convención: positivo = cuesta arriba
        }
    }
}
