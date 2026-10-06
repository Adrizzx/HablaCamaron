using UnityEngine;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// Cuenta VUELTAS AL REDONDEL de verdad (playtest 2026-07-25: "la parte
    /// final del redondel no tiene sentido, es como que te hace dar la vuelta
    /// en la calle").
    ///
    /// Antes, una misión de dos vueltas se resolvía tocando la baliza,
    /// alejándose 60 m y volviendo: en un redondel eso no significa nada. Aquí
    /// se acumula el ÁNGULO recorrido alrededor del centro; cuando el jugador
    /// ha girado 360° a su alrededor, eso es una vuelta — que es exactamente lo
    /// que uno entiende por "dar la vuelta al redondel".
    ///
    /// PURO (solo Vector2/Mathf) para probarlo en EditMode.
    /// </summary>
    public sealed class LapCounter
    {
        /// <summary>Radio dentro del cual cuenta el giro (m). Fuera de esto el
        /// jugador está circulando por la ciudad, no rodeando el redondel.</summary>
        public const float RingRadius = 45f;

        /// <summary>Grados que hay que acumular para una vuelta.</summary>
        public const float FullTurnDeg = 330f; // algo menos de 360: perdona el último trozo

        private float _acumulado;
        private float _anguloPrevio;
        private bool _siguiendo;

        /// <summary>Vueltas completadas.</summary>
        public int Laps { get; private set; }

        /// <summary>Progreso de la vuelta en curso, de 0 a 1 (para el HUD).</summary>
        public float Progress => Mathf.Clamp01(Mathf.Abs(_acumulado) / FullTurnDeg);

        /// <summary>
        /// Un latido con el vector del CENTRO del redondel al jugador (en
        /// planta). Devuelve true en el instante en que se completa una vuelta.
        /// </summary>
        public bool Tick(Vector2 centroAJugador)
        {
            float dist = centroAJugador.magnitude;

            // Lejos del redondel: se deja de seguir, pero NO se pierde lo
            // acumulado (dar una vuelta ancha sigue siendo dar la vuelta).
            if (dist > RingRadius || dist < 0.5f)
            {
                _siguiendo = false;
                return false;
            }

            float ang = Mathf.Atan2(centroAJugador.y, centroAJugador.x) * Mathf.Rad2Deg;
            if (!_siguiendo)
            {
                _siguiendo = true;
                _anguloPrevio = ang;
                return false;
            }

            _acumulado += Mathf.DeltaAngle(_anguloPrevio, ang);
            _anguloPrevio = ang;

            if (Mathf.Abs(_acumulado) < FullTurnDeg) return false;
            _acumulado = 0f;
            Laps++;
            return true;
        }
    }
}
