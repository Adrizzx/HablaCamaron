namespace HablaCamaron.Missions
{
    /// <summary>
    /// La regla del PASO CEBRA (playtest: "cebras funcionales"), PURA y
    /// testeada: quedarse DETENIDO encima de la cebra con su semáforo en rojo
    /// por más de StopSeconds bloquea el cruce de la gente = 1 infracción por
    /// estadía (al salir de la cebra se rearma). Pisarla en movimiento o
    /// pararse encima en verde no castiga: la falta es TAPARLA en rojo.
    /// </summary>
    public class CrosswalkJudge
    {
        /// <summary>Detenido = por debajo de esta velocidad.</summary>
        public const float StopSpeedKmh = 2f;
        /// <summary>Segundos detenido sobre la cebra en rojo para castigar.</summary>
        public const float StopSeconds = 1.5f;

        private float _stoppedFor;
        private bool _punishedThisStay;

        /// <summary>Un latido. true = infracción nueva (cebra bloqueada).</summary>
        public bool Tick(bool onCrosswalk, bool lightIsRed, float speedKmh, float dt)
        {
            if (!onCrosswalk)
            {
                _stoppedFor = 0f;
                _punishedThisStay = false; // salió: la próxima estadía cuenta de nuevo
                return false;
            }

            bool bloqueando = lightIsRed && speedKmh < StopSpeedKmh;
            _stoppedFor = bloqueando ? _stoppedFor + dt : 0f;

            if (_stoppedFor >= StopSeconds && !_punishedThisStay)
            {
                _punishedThisStay = true;
                return true;
            }
            return false;
        }
    }
}
