using UnityEngine;

namespace HablaCamaron.Vehicle
{
    /// <summary>
    /// Controles de teclado del auto (luego se suma el mando de carreras del laboratorio).
    ///
    ///   W / ↑        acelerador          S / ↓   freno
    ///   A / D        volante             Shift izq.  EMBRAGUE (mantener pisado)
    ///   1-5 / N / R  caja de cambios     Espacio     freno de mano (toggle)
    ///   F            encender / apagar   Q / E       direccional izq. / der.
    ///   L            luces (ciclo)       B           bocina (mantener)
    ///
    /// El truco pedagógico del embrague en teclado: la tecla es digital, pero el
    /// PEDAL simulado se mueve con inercia. Al soltar Shift, el pedal sube despacio
    /// (ClutchReleaseSpeed); si las RPM van muy bajas puedes "cazarlo" volviendo a
    /// pisar. Así el jugador aprende a coordinar embrague + acelerador de verdad.
    ///
    /// MANDO (best-effort con los ejes por defecto de Unity; falta validarlo con
    /// el volante del laboratorio):
    ///   Stick izq.   volante (analógico) y acelerar/frenar (arriba/abajo)
    ///   LB (btn 4)   EMBRAGUE (mantener)     RB (btn 5)  freno de mano
    ///   A / B (0/1)  subir / bajar marcha (secuencial R→N→1..5)
    ///   Y (btn 3)    encender/apagar          X (btn 2)  bocina
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleInput : MonoBehaviour
    {
        private VehicleController _car;

        // Suavizado de ejes digitales para que el auto no responda a golpes.
        private float _throttle, _brake, _steer;

        // Cambio pedido con el embrague aún en camino (int.MinValue = ninguno).
        private int _pendingGear = int.MinValue;
        private float _pendingFor;

        private const float ThrottleRise = 2.6f, ThrottleFall = 4.5f;
        private const float BrakeRise = 5.0f, BrakeFall = 6.0f;
        private const float SteerRise = 3.2f, SteerReturn = 4.0f;

        private void Awake() => _car = GetComponent<VehicleController>();

        private void Update()
        {
            // Con el juego en pausa (timeScale 0) no se procesa nada.
            if (Time.timeScale == 0f) return;
            // Igual que VehicleController.FixedUpdate: sin ficha de vehículo
            // no hay con qué calcular embrague/marchas (mismo guard que ya
            // usa Awake() de VehicleController para masa/centro de masa).
            if (_car.Spec == null) return;

            float dt = Time.deltaTime;

            // Ejes del mando (los ejes por defecto también cubren teclado; con
            // teclas pulsadas el resultado es el mismo, con stick es analógico).
            float padSteer = Input.GetAxisRaw("Horizontal");
            float padDrive = Input.GetAxisRaw("Vertical");

            // ---- Pedales analógicos simulados ----
            bool accel = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || padDrive > 0.25f;
            bool brake = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || padDrive < -0.25f;

            // Sensibilidad de pedales de Opciones (0.5 = el comportamiento
            // afinado en la Fase 0, exacto). Solo acelerador/freno: el pedal
            // de embrague es la pedagogía y no se toca.
            float sens = Core.GameAudioSettings.PedalMultiplier(
                Core.GameAudioSettings.PedalSensitivity);

            _throttle = Mathf.MoveTowards(_throttle, accel ? 1f : 0f, (accel ? ThrottleRise : ThrottleFall) * sens * dt);
            _brake = Mathf.MoveTowards(_brake, brake ? 1f : 0f, (brake ? BrakeRise : BrakeFall) * sens * dt);

            // ---- Volante ----
            float steerTarget = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) steerTarget -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) steerTarget += 1f;
            // Sin teclas, manda el stick (analógico, con zona muerta).
            if (Mathf.Approximately(steerTarget, 0f) && Mathf.Abs(padSteer) > 0.15f)
                steerTarget = Mathf.Clamp(padSteer, -1f, 1f);
            float steerSpeed = Mathf.Approximately(steerTarget, 0f) ? SteerReturn : SteerRise;
            _steer = Mathf.MoveTowards(_steer, steerTarget, steerSpeed * dt);

            // ---- Embrague: pedal con inercia (Shift o LB del mando) ----
            bool clutchKey = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
                             Input.GetKey(KeyCode.JoystickButton4);
            float pedalSpeed = clutchKey ? _car.Spec.ClutchPressSpeed : _car.Spec.ClutchReleaseSpeed;
            _car.ClutchPedal = Mathf.MoveTowards(_car.ClutchPedal, clutchKey ? 1f : 0f, pedalSpeed * dt);

            // Cambio en ESPERA: se pidió con el embrague aún bajando; entra
            // apenas el pedal llega (Shift y número casi a la vez SÍ cambian).
            if (_pendingGear != int.MinValue)
            {
                _pendingFor -= dt;
                if (VehicleController.ClutchReady(_car.ClutchPedal))
                {
                    _car.ShiftTo(_pendingGear);
                    _pendingGear = int.MinValue;
                }
                else if (!clutchKey || _pendingFor <= 0f)
                    _pendingGear = int.MinValue; // soltó el pedal a medio camino
            }

            _car.ThrottleInput = _throttle;
            _car.BrakeInput = _brake;
            _car.SteerInput = _steer;

            // ---- Caja de cambios (directa en teclado, secuencial en mando).
            //      R es TOGGLE: con reversa puesta, R vuelve a neutro. ----
            if (Input.GetKeyDown(KeyCode.Alpha1)) RequestShift(1, clutchKey);
            if (Input.GetKeyDown(KeyCode.Alpha2)) RequestShift(2, clutchKey);
            if (Input.GetKeyDown(KeyCode.Alpha3)) RequestShift(3, clutchKey);
            if (Input.GetKeyDown(KeyCode.Alpha4)) RequestShift(4, clutchKey);
            if (Input.GetKeyDown(KeyCode.Alpha5)) RequestShift(5, clutchKey);
            if (Input.GetKeyDown(KeyCode.N)) RequestShift(0, clutchKey);
            if (Input.GetKeyDown(KeyCode.R))
                RequestShift(VehicleController.ReverseToggleTarget(_car.Gear), clutchKey);

            int maxGear = _car.Spec.GearRatios.Length;
            if (Input.GetKeyDown(KeyCode.JoystickButton0)) // A: subir
                RequestShift(VehicleController.NextGear(_car.Gear, true, maxGear), clutchKey);
            if (Input.GetKeyDown(KeyCode.JoystickButton1)) // B: bajar
                RequestShift(VehicleController.NextGear(_car.Gear, false, maxGear), clutchKey);

            // ---- Otros controles ----
            if (Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.JoystickButton5)) _car.ToggleHandbrake();
            if (Input.GetKeyDown(KeyCode.F) ||
                Input.GetKeyDown(KeyCode.JoystickButton3)) _car.ToggleIgnition();
            if (Input.GetKeyDown(KeyCode.Q)) _car.ToggleBlinker(-1);
            if (Input.GetKeyDown(KeyCode.E)) _car.ToggleBlinker(1);
            if (Input.GetKeyDown(KeyCode.L)) _car.CycleLights();
            _car.HornOn = Input.GetKey(KeyCode.B) || Input.GetKey(KeyCode.JoystickButton2);
        }

        /// <summary>
        /// Pide una marcha. Si el embrague va EN CAMINO (tecla pisada pero el
        /// pedal todavía bajando), el cambio espera al pedal en vez de rechinar:
        /// Shift y número presionados casi a la vez SIEMPRE entran. Sin embrague
        /// pisado sigue rechinando — la pedagogía no se toca.
        /// </summary>
        private void RequestShift(int gear, bool clutchKeyHeld)
        {
            if (clutchKeyHeld && !VehicleController.ClutchReady(_car.ClutchPedal))
            {
                _pendingGear = gear;
                _pendingFor = 0.35f; // más que el viaje completo del pedal (~0.13 s)
                return;
            }
            _car.ShiftTo(gear);
        }
    }
}
