using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HablaCamaron.Core
{
    /// <summary>
    /// Pone la música que MusicKit compone (Fase 4). Vive una sola vez entre
    /// escenas (lo crea Bootstrap): al cargar una escena consulta
    /// MusicKit.MoodForScene y hace fundido de entrada/salida del pad de menús;
    /// en las zonas de manejo calla (ahí mandan el motor y el ambiente).
    /// PlaySting() lo llaman las pantallas de evaluación/final con el veredicto.
    /// El volumen sale del mixer (GameAudioSettings.MusicVolume) cada frame,
    /// así el slider de Opciones se oye en vivo.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        public static MusicDirector Instance { get; private set; }

        private AudioSource _loop;    // pad de menús (loop)
        private AudioSource _oneShot; // stings de veredicto
        private AudioClip _menuClip, _stingPass, _stingFail;
        private float _fade;          // 0-1: fundido actual del loop
        private float _fadeTarget;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            int rate = AudioSettings.outputSampleRate;
            if (rate <= 0) rate = 44100; // batch/tests: dispositivo de audio nulo
            _menuClip = ClipFrom("musica_menu", MusicKit.RenderMenuLoop(rate), rate);
            _stingPass = ClipFrom("sting_aprobado", MusicKit.RenderSting(true, rate), rate);
            _stingFail = ClipFrom("sting_reprobado", MusicKit.RenderSting(false, rate), rate);

            _loop = gameObject.AddComponent<AudioSource>();
            _loop.clip = _menuClip;
            _loop.loop = true;
            _loop.spatialBlend = 0f;
            _loop.volume = 0f;
            _loop.ignoreListenerPause = true; // la música sigue con el juego pausado

            _oneShot = gameObject.AddComponent<AudioSource>();
            _oneShot.spatialBlend = 0f;
            _oneShot.ignoreListenerPause = true;

            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private static AudioClip ClipFrom(string name, float[] samples, int rate)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private string _sceneName;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene.name);

        private void Apply(string sceneName)
        {
            _sceneName = sceneName;
            _fadeTarget = MusicKit.MoodForScene(sceneName) == MusicMood.Warm ? 1f : 0f;
            if (_fadeTarget > 0f && !_loop.isPlaying) _loop.Play();
        }

        /// <summary>
        /// Música del menú de PAUSA: al abrirla entra el pad cálido (el motor
        /// calla por la mordaza del mixer); al cerrarla vuelve lo que toque en
        /// la escena (silencio en las zonas de manejo). Lo llama PauseController.
        /// </summary>
        public void SetPauseMusic(bool paused)
        {
            if (paused)
            {
                _fadeTarget = 1f;
                if (!_loop.isPlaying) _loop.Play();
            }
            else Apply(_sceneName);
        }

        /// <summary>Veredicto sonoro (lo llaman EvaluationScreen y EndingScreen).</summary>
        public void PlaySting(bool passed)
        {
            _oneShot.volume = GameAudioSettings.MusicVolume;
            _oneShot.PlayOneShot(passed ? _stingPass : _stingFail);
        }

        /// <summary>
        /// Momento del veredicto (ganar/perder): suena el sting Y entra el pad
        /// cálido de fondo mientras se mira la evaluación o el final — el
        /// gameplay quedó en silencio (MissionRunner pausa el AudioListener).
        /// Al cargar la siguiente escena, Apply() decide de nuevo.
        /// </summary>
        public void PlayVerdict(bool passed)
        {
            PlaySting(passed);
            _fadeTarget = 1f;
            if (!_loop.isPlaying) _loop.Play();
        }

        private void Update()
        {
            // Fundido con tiempo REAL (las pantallas congelan el juego) y
            // volumen del mixer en vivo.
            _fade = Mathf.MoveTowards(_fade, _fadeTarget, Time.unscaledDeltaTime / 0.8f);
            _loop.volume = _fade * GameAudioSettings.MusicVolume;
            if (_fade <= 0f && _loop.isPlaying && _fadeTarget <= 0f) _loop.Pause();
        }
    }
}
