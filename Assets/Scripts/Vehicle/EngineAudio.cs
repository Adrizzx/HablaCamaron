using UnityEngine;

namespace HablaCamaron.Vehicle
{
    /// <summary>
    /// Sonido del motor 100% procedural (sin archivos de audio): genera la onda
    /// en OnAudioFilterRead según las RPM reales del VehicleController.
    /// Placeholder digno hasta grabar audio real / integrar FMOD (Fase 4).
    /// También genera la bocina y el "clic" del direccional.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class EngineAudio : MonoBehaviour
    {
        private VehicleController _car;
        private AudioSource _source;

        /// <summary>Efectos de una sola vez (feedback sonoro de cada acción).</summary>
        public enum Fx { None, Shift, Grind, Handbrake, Ignition, Stall }

        // Estado compartido con el hilo de audio (solo lectura/escritura simple).
        private volatile float _freq = 30f;      // frecuencia base del motor (Hz)
        private volatile float _amp;             // volumen del motor
        private volatile float _master = 1f;     // volumen de efectos (mixer de Opciones)
        private volatile bool _horn;
        private volatile float _clickBurst;      // >0 = suena un clic de direccional
        private volatile Fx _fxPending = Fx.None; // efecto solicitado desde el juego
        private Fx _fx = Fx.None;                 // efecto sonando (solo hilo de audio)
        private double _fxT;                      // tiempo transcurrido del efecto

        private double _phase, _phase2, _hornPhaseA, _hornPhaseB;
        private int _sampleRate;
        private System.Random _rng = new System.Random();

        // Temporizador del parpadeo de direccional (para el clic).
        private float _blinkTimer;

        private void Awake()
        {
            _car = GetComponent<VehicleController>();
            _sampleRate = AudioSettings.outputSampleRate;
            if (_sampleRate <= 0) _sampleRate = 44100; // batch/tests: dispositivo de audio nulo

            // Un AudioSource con clip silencioso en loop mantiene vivo el filtro.
            _source = gameObject.AddComponent<AudioSource>();
            var silent = AudioClip.Create("silencio", _sampleRate, 1, _sampleRate, false);
            _source.clip = silent;
            _source.loop = true;
            _source.spatialBlend = 0f; // 2D: es el auto del propio jugador
            _source.volume = 1f;
            _source.Play();
        }

        private void Update()
        {
            // Sin ficha de vehículo no hay MaxRpm con qué normalizar (mismo
            // guard que VehicleController.FixedUpdate usa para Spec).
            if (_car.Spec == null) return;

            // Frecuencia: un 4 cilindros suena a ~RPM/60*2 explosiones por segundo.
            float rpm = Mathf.Max(_car.Rpm, 0f);
            _freq = Mathf.Lerp(24f, 210f, Mathf.InverseLerp(0f, _car.Spec.MaxRpm, rpm));

            // Volumen: apagado = silencio; ralentí bajito; acelerando sube.
            if (!_car.EngineOn)
                _amp = Mathf.MoveTowards(_amp, 0f, Time.deltaTime * 1.5f);
            else
                _amp = Mathf.Lerp(0.05f, 0.16f, _car.Rpm01 * 0.6f + _car.ThrottleInput * 0.4f);

            _horn = _car.HornOn;
            // Slider de "Efectos" + mordaza del veredicto (0 = silencio total).
            _master = Core.GameAudioSettings.EffectiveSfxVolume;

            // Clic del direccional cada medio segundo mientras esté activo.
            if (_car.BlinkerState != 0)
            {
                _blinkTimer += Time.deltaTime;
                if (_blinkTimer >= 0.5f)
                {
                    _blinkTimer = 0f;
                    _clickBurst = 0.03f; // 30 ms de clic
                }
            }
            else _blinkTimer = 0.5f; // el primer clic suena apenas se activa
        }

        /// <summary>Dispara un efecto de una sola vez (lo llama VehicleHUDBridge).</summary>
        public void Play(Fx fx) => _fxPending = fx;

        // Genera la muestra del efecto activo. Corre en el hilo de audio.
        private float SampleFx(double dt)
        {
            if (_fxPending != Fx.None) { _fx = _fxPending; _fxPending = Fx.None; _fxT = 0.0; }
            if (_fx == Fx.None) return 0f;

            _fxT += dt;
            float t = (float)_fxT;
            float s = 0f;
            float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);

            switch (_fx)
            {
                case Fx.Shift: // golpe seco y corto: la palanca entró
                    if (t > 0.09f) { _fx = Fx.None; break; }
                    s = Mathf.Sin(t * 90f * 6.283f) * 0.5f + noise * 0.25f;
                    s *= Mathf.Exp(-t * 45f) * 0.6f;
                    break;

                case Fx.Grind: // rechinido áspero: cambió sin embrague
                    if (t > 0.38f) { _fx = Fx.None; break; }
                    float saw = (t * 270f) % 1f * 2f - 1f;
                    float tremolo = 0.6f + 0.4f * Mathf.Sin(t * 32f * 6.283f);
                    s = (saw * 0.5f + noise * 0.5f) * tremolo * 0.22f;
                    break;

                case Fx.Handbrake: // trinquete: clics rápidos
                    if (t > 0.15f) { _fx = Fx.None; break; }
                    s = ((t * 24f) % 1f) < 0.3f ? noise * 0.3f : 0f;
                    break;

                case Fx.Ignition: // arranque "ñi-ñi-ñi" del motor de partida
                    if (t > 0.5f) { _fx = Fx.None; break; }
                    float gate = Mathf.Abs(Mathf.Sin(t * 7f * 3.1416f));
                    s = (Mathf.Sin(t * 130f * 6.283f) * 0.6f + noise * 0.3f) * gate * 0.3f;
                    break;

                case Fx.Stall: // tono que cae + golpe: el motor murió
                    if (t > 0.35f) { _fx = Fx.None; break; }
                    float f = Mathf.Lerp(120f, 35f, t / 0.35f);
                    s = Mathf.Sin(t * f * 6.283f) * Mathf.Exp(-t * 6f) * 0.45f
                        + noise * Mathf.Exp(-t * 30f) * 0.2f;
                    break;
            }
            return s;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            double freq = _freq;
            float amp = _amp;
            bool horn = _horn;
            float click = _clickBurst;
            double dt = 1.0 / _sampleRate;

            for (int i = 0; i < data.Length; i += channels)
            {
                float sample = 0f;

                // Motor: dos "sierras" desafinadas + algo de ruido = ronroneo creíble.
                if (amp > 0.001f)
                {
                    _phase += freq * dt;
                    _phase2 += freq * 0.5 * dt; // subarmónico grave
                    float saw = (float)(_phase - System.Math.Floor(_phase)) * 2f - 1f;
                    float saw2 = (float)(_phase2 - System.Math.Floor(_phase2)) * 2f - 1f;
                    float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                    sample += (saw * 0.55f + saw2 * 0.35f + noise * 0.10f) * amp;
                }

                // Bocina: dos tonos cuadrados (la bocina bitonal clásica).
                if (horn)
                {
                    _hornPhaseA += 420.0 * dt;
                    _hornPhaseB += 530.0 * dt;
                    float a = (_hornPhaseA - System.Math.Floor(_hornPhaseA)) < 0.5 ? 1f : -1f;
                    float b = (_hornPhaseB - System.Math.Floor(_hornPhaseB)) < 0.5 ? 1f : -1f;
                    sample += (a + b) * 0.09f;
                }

                // Clic del direccional: ráfaga corta de ruido seco.
                if (click > 0f)
                {
                    sample += (float)(_rng.NextDouble() * 2.0 - 1.0) * 0.12f * (click / 0.03f);
                    click -= (float)dt;
                }

                // Efecto one-shot activo (cambio, rechinido, arranque...).
                sample += SampleFx(dt);

                // Mismo sample a todos los canales (mono → estéreo),
                // escalado por el volumen de efectos del mixer.
                for (int c = 0; c < channels; c++)
                    data[i + c] = Mathf.Clamp(sample * _master, -1f, 1f);
            }

            _clickBurst = Mathf.Max(0f, click);
        }
    }
}
