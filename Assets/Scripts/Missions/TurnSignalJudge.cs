using UnityEngine;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// El juez de las direccionales (GDD: "penalización por no usarlas en
    /// giros"). Lógica PURA alimentada frame a frame: acumula el giro real del
    /// auto (yaw) y, si completa un giro franco sin haber avisado con la
    /// direccional correcta, lo cuenta como omisión. Las curvas suaves se
    /// perdonan (el acumulado decae solo) y avisar unos segundos ANTES del
    /// giro también vale, como en la vida real. Testeada en EditMode.
    /// </summary>
    public class TurnSignalJudge
    {
        /// <summary>Yaw acumulado (grados) que ya cuenta como giro franco.</summary>
        public const float TurnAngle = 60f;
        /// <summary>Cuánto acumulado se olvida por segundo (perdona curvas suaves).</summary>
        public const float DecayPerSecond = 12f;
        /// <summary>Memoria de la direccional: avisar hasta estos segundos antes vale.</summary>
        public const float SignalMemory = 3f;
        /// <summary>Tras castigar, tregua para no ametrallar (p. ej. en un redondel).</summary>
        public const float Cooldown = 5f;

        /// <summary>Giros completados sin la direccional correspondiente.</summary>
        public int Misses { get; private set; }

        private float _accum;        // yaw acumulado (+ = derecha, − = izquierda)
        private float _leftMemory;   // segundos restantes de "avisó a la izquierda"
        private float _rightMemory;
        private float _cooldown;

        /// <summary>
        /// Avanza un frame. deltaYawDeg = cambio de rumbo del auto (grados,
        /// + horario/derecha); blinker = -1 izq, 0 nada, 1 der; moving = va a
        /// velocidad de giro real (parado no se juzga). Devuelve true SOLO en
        /// el frame en que se detecta una omisión nueva.
        /// </summary>
        public bool Tick(float deltaYawDeg, int blinker, bool moving, float dt)
        {
            _cooldown = Mathf.Max(0f, _cooldown - dt);
            _leftMemory = Mathf.Max(0f, _leftMemory - dt);
            _rightMemory = Mathf.Max(0f, _rightMemory - dt);

            if (blinker < 0) _leftMemory = SignalMemory;
            else if (blinker > 0) _rightMemory = SignalMemory;

            if (!moving)
            {
                _accum = 0f; // maniobras detenido (parqueo) no son giros de vía
                return false;
            }

            _accum = Mathf.MoveTowards(_accum, 0f, DecayPerSecond * dt);
            _accum += deltaYawDeg;

            if (Mathf.Abs(_accum) < TurnAngle) return false;

            bool signaled = _accum > 0f ? _rightMemory > 0f : _leftMemory > 0f;
            _accum = 0f;

            if (signaled || _cooldown > 0f) return false;

            Misses++;
            _cooldown = Cooldown;
            return true;
        }
    }
}
