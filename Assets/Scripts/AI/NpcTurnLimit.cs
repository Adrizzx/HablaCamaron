using UnityEngine;

namespace HablaCamaron.AI
{
    /// <summary>
    /// Cuánto puede girar un NPC por segundo, PURO. Un auto no pivota sobre su
    /// eje: parado apenas endereza las ruedas y rodando dobla como un auto.
    /// Sin este tope, al replanificar la ruta el NPC se daba media vuelta en
    /// medio de la calle (playtest 2026-07-27).
    /// </summary>
    public static class NpcTurnLimit
    {
        /// <summary>Grados por segundo con el auto detenido.</summary>
        public const float ParadoDegPerSec = 25f;
        /// <summary>Grados por segundo a velocidad de calle.</summary>
        public const float RodandoDegPerSec = 110f;
        /// <summary>m/s desde donde ya gira a tope.</summary>
        public const float VelocidadPlena = 8f;

        public static float MaxYawRate(float speedMs) => Mathf.Lerp(
            ParadoDegPerSec, RodandoDegPerSec, Mathf.Clamp01(speedMs / VelocidadPlena));

        public static Quaternion Steer(Quaternion actual, Quaternion deseada,
            float speedMs, float dt) =>
            Quaternion.RotateTowards(actual, deseada, MaxYawRate(speedMs) * dt);
    }
}
