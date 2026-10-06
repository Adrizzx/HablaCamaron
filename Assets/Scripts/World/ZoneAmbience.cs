using UnityEngine;

namespace HablaCamaron.World
{
    /// <summary>Carácter sonoro de cada zona (Fase 4).</summary>
    public enum AmbienceProfile
    {
        None,      // menús y pantallas: sin ambiente
        Barrio,    // Zona Sur: brisa suave, pájaros y algún perro lejano
        Autopista, // Av. Simón Bolívar: viento nocturno sostenido
        Ciudad,    // Corredor del examen: rumor urbano y bocinas lejanas
    }

    /// <summary>
    /// Ambiente sonoro 100% procedural por zona (sin archivos, igual que
    /// EngineAudio): ruido filtrado = viento/rumor, más eventos one-shot
    /// (pájaro, perro, bocina) que se agendan con temporizadores aleatorios.
    /// Lo crea MissionSystemBootstrap al cargar una escena de manejo.
    /// El volumen sale del slider de Efectos (mixer de Opciones).
    /// </summary>
    public class ZoneAmbience : MonoBehaviour
    {
        public AmbienceProfile Profile = AmbienceProfile.None;

        /// <summary>Qué ambiente lleva cada escena. PURA (testeada).</summary>
        public static AmbienceProfile ProfileForScene(string sceneName) => sceneName switch
        {
            "N1_ZonaSur" => AmbienceProfile.Barrio,
            "N0_TestDrive" => AmbienceProfile.Barrio,
            "N1_SimonBolivar" => AmbienceProfile.Autopista,
            "N1_CorredorExamen" => AmbienceProfile.Ciudad,
            "N1_CiudadToon" => AmbienceProfile.Ciudad, // la ciudad grande del examen
            "N2_QuitoCiudad" => AmbienceProfile.Ciudad,
            _ => AmbienceProfile.None,
        };

        private enum Fx { None, Pajaro, Perro, Bocina }

        // Estado compartido con el hilo de audio.
        private volatile float _windAmp;
        private volatile float _rumbleAmp;
        private volatile float _master;
        private volatile Fx _fxPending = Fx.None;
        private Fx _fx = Fx.None;   // solo hilo de audio
        private double _fxT;

        private AudioSource _source;
        private int _sampleRate;
        private float _nextEventIn = 4f;
        private float _lp, _lp2;        // filtros pasa-bajos (estado)
        private double _hornA, _hornB;  // fases de la bocina
        private System.Random _rng = new System.Random();

        private void Awake()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            if (_sampleRate <= 0) _sampleRate = 44100; // batch/tests: audio nulo

            // Clip silencioso en loop: mantiene vivo OnAudioFilterRead.
            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = AudioClip.Create("silencio_amb", _sampleRate, 1, _sampleRate, false);
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.Play();
        }

        private void Update()
        {
            // Slider de "Efectos" + mordaza del veredicto (0 = silencio total).
            _master = Core.GameAudioSettings.EffectiveSfxVolume;

            // Carácter base por perfil (viento / rumor de ciudad).
            switch (Profile)
            {
                case AmbienceProfile.Barrio: _windAmp = 0.030f; _rumbleAmp = 0.010f; break;
                case AmbienceProfile.Autopista: _windAmp = 0.075f; _rumbleAmp = 0.018f; break;
                case AmbienceProfile.Ciudad: _windAmp = 0.028f; _rumbleAmp = 0.045f; break;
                default: _windAmp = 0f; _rumbleAmp = 0f; break;
            }

            // Eventos ocasionales, según la personalidad de la zona.
            _nextEventIn -= Time.deltaTime;
            if (_nextEventIn <= 0f)
            {
                switch (Profile)
                {
                    case AmbienceProfile.Barrio:
                        // Pájaros seguido; de vez en cuando un perro lejano.
                        _fxPending = _rng.NextDouble() < 0.3 ? Fx.Perro : Fx.Pajaro;
                        _nextEventIn = 3f + (float)_rng.NextDouble() * 6f;
                        break;
                    case AmbienceProfile.Ciudad:
                        _fxPending = Fx.Bocina;
                        _nextEventIn = 7f + (float)_rng.NextDouble() * 12f;
                        break;
                    default: // la Simón de noche es solo viento (soledad = GDD)
                        _nextEventIn = 10f;
                        break;
                }
            }
        }

        // Genera la muestra del evento activo. Corre en el hilo de audio.
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
                case Fx.Pajaro: // dos chiflidos agudos y cortos
                    if (t > 0.30f) { _fx = Fx.None; break; }
                    float ciclo = t % 0.15f;
                    float fp = Mathf.Lerp(2800f, 3400f, ciclo / 0.15f);
                    s = Mathf.Sin(t * fp * 6.283f) * Mathf.Exp(-ciclo * 25f) * 0.05f;
                    break;

                case Fx.Perro: // dos ladridos lejanos (sierra grave que cae)
                    if (t > 0.55f) { _fx = Fx.None; break; }
                    float lad = t % 0.28f;
                    if (lad < 0.12f)
                    {
                        float fd = Mathf.Lerp(330f, 180f, lad / 0.12f);
                        float saw = (lad * fd) % 1f * 2f - 1f;
                        s = (saw * 0.6f + noise * 0.4f) * Mathf.Exp(-lad * 18f) * 0.06f;
                    }
                    break;

                case Fx.Bocina: // pitazo bitonal lejano (Quito saluda)
                    if (t > 0.45f) { _fx = Fx.None; break; }
                    _hornA += 410.0 * dt;
                    _hornB += 515.0 * dt;
                    float a = (_hornA - System.Math.Floor(_hornA)) < 0.5 ? 1f : -1f;
                    float b = (_hornB - System.Math.Floor(_hornB)) < 0.5 ? 1f : -1f;
                    s = (a + b) * 0.02f * Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI);
                    break;
            }
            return s;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float wind = _windAmp, rumble = _rumbleAmp, master = _master;
            double dt = 1.0 / _sampleRate;
            double t0 = AudioSettings.dspTime;

            for (int i = 0; i < data.Length; i += channels)
            {
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);

                // Viento: ruido pasa-bajos con vaivén lento (ráfagas).
                _lp += 0.045f * (noise - _lp);
                float lfo = 0.65f + 0.35f * Mathf.Sin((float)((t0 + i * dt) * 0.8));
                float sample = _lp * wind * lfo;

                // Rumor urbano: el mismo ruido filtrado aún más grave.
                _lp2 += 0.008f * (noise - _lp2);
                sample += _lp2 * rumble;

                sample += SampleFx(dt);

                for (int c = 0; c < channels; c++)
                    data[i + c] = Mathf.Clamp(sample * master, -1f, 1f);
            }
        }
    }
}
