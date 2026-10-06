using UnityEngine;

namespace HablaCamaron.AI
{
    /// <summary>
    /// La bocina de los NPCs (¡Quito saluda!): cuando el jugador los deja
    /// bloqueados, PITAN en vez de embestir. Un solo clip procedural
    /// compartido por todo el tráfico (bitonal, como la del Aveo pero ajena),
    /// generado sin archivos de audio como todo el sonido del juego.
    /// El render es PURO y determinista (testeado en EditMode).
    /// </summary>
    public static class NpcHorn
    {
        /// <summary>Duración del pitazo en segundos.</summary>
        public const float Seconds = 0.5f;

        private static AudioClip _clip;

        /// <summary>El clip compartido (se crea una sola vez por sesión).</summary>
        public static AudioClip Clip
        {
            get
            {
                if (_clip == null)
                {
                    int rate = AudioSettings.outputSampleRate;
                    if (rate <= 0) rate = 44100; // batch/tests: audio nulo
                    var samples = Render(rate);
                    _clip = AudioClip.Create("bocina_npc", samples.Length, 1, rate, false);
                    _clip.SetData(samples, 0);
                }
                return _clip;
            }
        }

        /// <summary>
        /// El pitazo, mono: dos tonos cuadrados (390/490 Hz — más agudo que la
        /// bocina del jugador, para distinguirlos) con envolvente que abre y
        /// cierra en silencio (sin clics). PURO: mismo resultado siempre.
        /// </summary>
        public static float[] Render(int sampleRate)
        {
            int total = Mathf.RoundToInt(Seconds * sampleRate);
            var data = new float[total];

            for (int n = 0; n < total; n++)
            {
                float t = n / (float)sampleRate;
                float a = (t * 390f) % 1f < 0.5f ? 1f : -1f;
                float b = (t * 490f) % 1f < 0.5f ? 1f : -1f;
                // Envolvente en campana: 0 en los bordes, pleno al centro.
                float env = Mathf.Sin(Mathf.Clamp01(t / Seconds) * Mathf.PI);
                data[n] = Mathf.Clamp((a + b) * 0.14f * env, -1f, 1f);
            }
            return data;
        }
    }
}
