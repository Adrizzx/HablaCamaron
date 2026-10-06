using UnityEngine;

namespace HablaCamaron.Core
{
    /// <summary>Qué música corresponde a una escena.</summary>
    public enum MusicMood
    {
        Warm,   // menús / mapa / garaje: pad cálido de atardecer quiteño
        Silent, // zonas de manejo: la música es el motor y la ciudad (ambience)
    }

    /// <summary>
    /// Compositor procedural (Fase 4): genera la música SIN archivos de audio,
    /// igual que EngineAudio genera el motor. Funciones PURAS (float[] de
    /// samples) para poder probarlas en EditMode: el loop de menús es un pad
    /// de cuerdas suaves en La menor con una melodía pentatónica determinista,
    /// y los "stings" de evaluación son arpegios (sube = aprobado, baja = no).
    /// </summary>
    public static class MusicKit
    {
        // ---- Mapa escena → modo (lo usa MusicDirector; testeado) ----

        /// <summary>Las zonas de manejo (N0_*/N1_*) van sin música; el resto
        /// (menú, mapa, garaje, créditos...) lleva el pad cálido.</summary>
        public static MusicMood MoodForScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return MusicMood.Silent;
            if (sceneName.StartsWith("N0_") || sceneName.StartsWith("N1_") ||
                sceneName.StartsWith("N2_"))
                return MusicMood.Silent;
            return MusicMood.Warm;
        }

        // ---- Render del loop de menús ----

        /// <summary>Duración del loop en segundos (4 acordes × 4 s).</summary>
        public const float LoopSeconds = 16f;

        private const float ChordSeconds = 4f;

        // Progresión i–VI–III–VII en La menor (melancolía andina amable).
        private static readonly float[][] Chords =
        {
            new[] { 220.00f, 261.63f, 329.63f }, // Am
            new[] { 174.61f, 220.00f, 261.63f }, // F
            new[] { 261.63f, 329.63f, 392.00f }, // C
            new[] { 196.00f, 246.94f, 293.66f }, // G
        };

        // Pentatónica de La menor para la melodía (una nota por medio compás).
        private static readonly float[] Penta =
            { 440.00f, 523.25f, 587.33f, 659.25f, 783.99f };

        /// <summary>
        /// Loop completo de menús, mono. Determinista (misma semilla, mismo
        /// resultado) y con envolventes que abren y cierran en silencio para
        /// que el loop no haga clic al repetirse.
        /// </summary>
        public static float[] RenderMenuLoop(int sampleRate)
        {
            int total = Mathf.RoundToInt(LoopSeconds * sampleRate);
            var data = new float[total];
            var rng = new System.Random(1717); // determinista: siempre la misma pieza

            // Melodía: una nota pentatónica cada 2 s, elegida con la semilla.
            int steps = Mathf.RoundToInt(LoopSeconds / 2f);
            var melody = new float[steps];
            for (int i = 0; i < steps; i++)
                melody[i] = Penta[rng.Next(Penta.Length)];

            for (int n = 0; n < total; n++)
            {
                float t = n / (float)sampleRate;
                float sample = 0f;

                // --- Pad de acordes con ataque/caída suaves ---
                int chord = Mathf.Min((int)(t / ChordSeconds), Chords.Length - 1);
                float tc = t - chord * ChordSeconds; // tiempo dentro del acorde
                float env = Envelope(tc, ChordSeconds, attack: 0.9f, release: 1.2f);
                foreach (float f in Chords[chord])
                {
                    // Dos osciladores levemente desafinados = coro cálido.
                    sample += Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f;
                    sample += Mathf.Sin(2f * Mathf.PI * (f * 1.003f) * t) * 0.5f;
                }
                sample *= env * 0.055f;

                // --- Melodía: campanita con decaimiento exponencial ---
                int step = Mathf.Min((int)(t / 2f), steps - 1);
                float ts = t - step * 2f;
                sample += Mathf.Sin(2f * Mathf.PI * melody[step] * t)
                          * Mathf.Exp(-ts * 2.2f) * 0.045f;

                data[n] = Mathf.Clamp(sample, -1f, 1f);
            }
            return data;
        }

        // ---- Stings de evaluación ----

        /// <summary>Duración del sting en segundos.</summary>
        public const float StingSeconds = 1.6f;

        /// <summary>
        /// Arpegio corto para el veredicto: aprobado = Do mayor subiendo;
        /// reprobado = La menor bajando (el mismo lenguaje que las pantallas).
        /// </summary>
        public static float[] RenderSting(bool passed, int sampleRate)
        {
            float[] notes = passed
                ? new[] { 261.63f, 329.63f, 392.00f, 523.25f }  // C E G C↑
                : new[] { 440.00f, 349.23f, 293.66f, 220.00f }; // A F D A↓

            int total = Mathf.RoundToInt(StingSeconds * sampleRate);
            var data = new float[total];
            const float noteDur = 0.22f;

            for (int n = 0; n < total; n++)
            {
                float t = n / (float)sampleRate;
                float sample = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float start = i * noteDur;
                    if (t < start) break;
                    float tn = t - start;
                    sample += Mathf.Sin(2f * Mathf.PI * notes[i] * tn)
                              * Mathf.Exp(-tn * 3.5f) * 0.16f;
                }
                data[n] = Mathf.Clamp(sample, -1f, 1f);
            }
            return data;
        }

        /// <summary>Envolvente trapezoidal: sube en attack, baja en release,
        /// y garantiza silencio exacto en los bordes (loops sin clic).</summary>
        private static float Envelope(float t, float duration, float attack, float release)
        {
            if (t <= 0f || t >= duration) return 0f;
            float a = Mathf.Clamp01(t / attack);
            float r = Mathf.Clamp01((duration - t) / release);
            return Mathf.Min(a, r);
        }
    }
}
