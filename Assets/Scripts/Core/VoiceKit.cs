using UnityEngine;

namespace HablaCamaron.Core
{
    /// <summary>
    /// La "voz" procedural de Don Pancho (cierre post-Fase 4): balbuceo de
    /// sílabas graves estilo villager — no dice palabras, ACOMPAÑA la burbuja
    /// de texto para que el instructor se sienta presente. Funciones PURAS
    /// (mensaje → sílabas/semilla → samples) probadas en EditMode, como
    /// MusicKit. La misma frase suena siempre igual (determinista).
    /// El slider "Voz de Don Pancho" de Opciones por fin controla algo.
    /// </summary>
    public static class VoiceKit
    {
        /// <summary>Tope de sílabas por frase (que no se eternice el balbuceo).</summary>
        public const int MaxSyllables = 12;

        private const float SyllableSeconds = 0.085f;
        private const float GapSeconds = 0.055f;

        /// <summary>Cuántas sílabas "dice" la voz para un texto: proporcional
        /// al largo, mínimo 2 (hasta un "¡Ojo!" suena a dos golpes), con tope.</summary>
        public static int SyllableCount(string message)
        {
            if (string.IsNullOrEmpty(message)) return 0;
            return Mathf.Clamp(1 + message.Length / 6, 2, MaxSyllables);
        }

        /// <summary>Semilla estable por mensaje: la misma frase = la misma
        /// melodía de balbuceo (suena a que "dice" algo concreto).</summary>
        public static int SeedFor(string message)
        {
            if (string.IsNullOrEmpty(message)) return 0;
            unchecked
            {
                int h = 17;
                foreach (char c in message) h = h * 31 + c;
                return h;
            }
        }

        /// <summary>Duración total en segundos de una frase de n sílabas.</summary>
        public static float PhraseSeconds(int syllables) =>
            Mathf.Max(0, syllables) * (SyllableSeconds + GapSeconds) + 0.05f;

        /// <summary>
        /// Renderiza el balbuceo, mono. Voz grave de señor mayor (~105-165 Hz)
        /// con dos armónicos y vibrato leve; cada sílaba tiene su tono (la
        /// semilla decide) y una envolvente que abre y cierra en silencio.
        /// </summary>
        public static float[] RenderPhrase(int seed, int syllables, int sampleRate)
        {
            syllables = Mathf.Clamp(syllables, 0, MaxSyllables);
            int total = Mathf.RoundToInt(PhraseSeconds(syllables) * sampleRate);
            var data = new float[total];
            if (syllables == 0) return data;

            var rng = new System.Random(seed);
            float slot = SyllableSeconds + GapSeconds;

            for (int s = 0; s < syllables; s++)
            {
                float f0 = 105f + (float)rng.NextDouble() * 60f; // tono de la sílaba
                int start = Mathf.RoundToInt(s * slot * sampleRate);
                int len = Mathf.RoundToInt(SyllableSeconds * sampleRate);

                for (int n = 0; n < len && start + n < total; n++)
                {
                    float t = n / (float)sampleRate;
                    // Envolvente: ataque rápido, caída suave, bordes en cero.
                    float env = Mathf.Sin(Mathf.Clamp01(t / SyllableSeconds) * Mathf.PI);
                    float vib = 1f + 0.02f * Mathf.Sin(t * 34f);
                    float w = 2f * Mathf.PI * f0 * vib * t;
                    float sample = (Mathf.Sin(w) * 0.55f +
                                    Mathf.Sin(w * 2f) * 0.28f +
                                    Mathf.Sin(w * 3f) * 0.12f) * env * 0.30f;
                    data[start + n] = Mathf.Clamp(sample, -1f, 1f);
                }
            }
            return data;
        }
    }
}
