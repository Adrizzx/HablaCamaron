using System.Collections.Generic;
using UnityEngine;
using HablaCamaron.Core;

namespace HablaCamaron.UI
{
    /// <summary>
    /// Pone en el aire el balbuceo que VoiceKit compone: cada burbuja de
    /// Don Pancho suena mientras se muestra (DonPanchoDialogue lo llama).
    /// Cachea el clip por frase (las frases se repiten) y respeta el slider
    /// "Voz de Don Pancho" EN VIVO. Lo crea DonPanchoDialogue en su Awake.
    /// </summary>
    public class DonPanchoVoice : MonoBehaviour
    {
        private AudioSource _source;
        private int _sampleRate;
        private readonly Dictionary<int, AudioClip> _cache = new();

        private void Awake()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            if (_sampleRate <= 0) _sampleRate = 44100; // batch/tests: audio nulo

            _source = gameObject.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;
            _source.ignoreListenerPause = true; // habla también con el juego pausado
        }

        /// <summary>Balbucea la frase (determinista: misma frase, misma "melodía").</summary>
        public void Speak(string message)
        {
            float vol = GameAudioSettings.VoiceVolume;
            if (vol <= 0.01f) return; // el slider en cero = Don Pancho calla

            int syllables = VoiceKit.SyllableCount(message);
            if (syllables == 0) return;

            int seed = VoiceKit.SeedFor(message);
            if (!_cache.TryGetValue(seed, out var clip))
            {
                var samples = VoiceKit.RenderPhrase(seed, syllables, _sampleRate);
                clip = AudioClip.Create("voz_" + seed, samples.Length, 1, _sampleRate, false);
                clip.SetData(samples, 0);
                _cache[seed] = clip;
            }

            _source.Stop(); // una frase a la vez (la nueva burbuja manda)
            _source.volume = vol;
            _source.clip = clip;
            _source.Play();
        }
    }
}
