using UnityEngine;

namespace HablaCamaron.Core
{
    /// <summary>
    /// El "mixer" del juego (Fase 4): un solo lugar que lee los volúmenes por
    /// capa y la sensibilidad de pedales que OptionsPanel guarda en PlayerPrefs.
    /// Los consumidores (EngineAudio, MusicDirector, ZoneAmbience, VehicleInput)
    /// leen los valores CACHEADOS; OptionsPanel llama Refresh() al mover un
    /// slider para que el cambio suene al instante.
    /// Las claves opt_* son las que OptionsPanel ya usaba desde la Fase de UI
    /// (se conservan para no perder las opciones guardadas de los jugadores).
    /// </summary>
    public static class GameAudioSettings
    {
        public const string KEY_MUSIC = "opt_vol_music";
        public const string KEY_SFX = "opt_vol_sfx";
        public const string KEY_VOICE = "opt_vol_voice";
        public const string KEY_PEDALS = "opt_pedal_sens";

        public const float DefaultMusic = 0.30f;
        public const float DefaultSfx = 0.75f;
        public const float DefaultVoice = 0.90f;
        public const float DefaultPedals = 0.50f;

        private static float _music = -1f, _sfx = -1f, _voice = -1f, _pedals = -1f;

        /// <summary>Volumen de música 0-1 (menús, mapa, sting de evaluación).</summary>
        public static float MusicVolume { get { EnsureLoaded(); return _music; } }

        /// <summary>Volumen de efectos 0-1 (motor, bocina, ambiente de la zona).</summary>
        public static float SfxVolume { get { EnsureLoaded(); return _sfx; } }

        /// <summary>
        /// Mordaza del audio de gameplay: true durante el veredicto de misión
        /// (el motor rugiendo sobre la evaluación era insoportable). La ponen
        /// MissionRunner.Finish (true) y MissionSystemBootstrap al cargar
        /// escena (false). OJO: AudioListener.pause NO detiene el audio
        /// procedural de OnAudioFilterRead — por eso existe esta llave, que
        /// multiplica las muestras por cero (silencio garantizado).
        /// </summary>
        public static bool GameplayMuted { get; set; }

        /// <summary>El volumen de efectos que de VERDAD debe sonar ahora:
        /// 0 con la mordaza puesta, el slider si no. PURA (testeada).</summary>
        public static float EffectiveSfxVolume => GameplayMuted ? 0f : SfxVolume;

        /// <summary>Volumen de voz 0-1 (reservado: las frases de Don Pancho aún son texto).</summary>
        public static float VoiceVolume { get { EnsureLoaded(); return _voice; } }

        /// <summary>Sensibilidad de pedales 0-1 tal como está en el slider.</summary>
        public static float PedalSensitivity { get { EnsureLoaded(); return _pedals; } }

        /// <summary>Relee PlayerPrefs (lo llama OptionsPanel al mover un slider).</summary>
        public static void Refresh()
        {
            _music = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_MUSIC, DefaultMusic));
            _sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_SFX, DefaultSfx));
            _voice = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_VOICE, DefaultVoice));
            _pedals = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_PEDALS, DefaultPedals));
        }

        private static void EnsureLoaded()
        {
            if (_music < 0f) Refresh();
        }

        /// <summary>
        /// Sensibilidad de pedal → multiplicador de velocidad de los ejes.
        /// PURA (testeada): 0 = 0.6× (pedales calmados), 0.5 = 1.0× EXACTO
        /// (el comportamiento afinado en la Fase 0 no cambia con el valor por
        /// defecto), 1 = 1.6× (respuesta ágil).
        /// </summary>
        public static float PedalMultiplier(float sensitivity01)
        {
            float s = Mathf.Clamp01(sensitivity01);
            return s <= 0.5f
                ? Mathf.Lerp(0.6f, 1.0f, s / 0.5f)
                : Mathf.Lerp(1.0f, 1.6f, (s - 0.5f) / 0.5f);
        }
    }
}
