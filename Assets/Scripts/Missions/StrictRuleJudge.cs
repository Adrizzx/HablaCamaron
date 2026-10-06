namespace HablaCamaron.Missions
{
    public enum StrictFail { None, RedLight, WrongWay }

    /// <summary>
    /// Muerte súbita (misiones con StrictRules): decide si una señal de los
    /// jueces existentes amerita reprobado INMEDIATO. Pura: sin Unity.
    /// La contravía exige sostenerse (el cruce diagonal legítimo de una
    /// intersección pinta WrongWay unos frames y no debe matar la misión).
    /// </summary>
    public sealed class StrictRuleJudge
    {
        public const float ContraviaSostenida = 2f; // segundos

        private float _contravia;

        public StrictFail Evaluate(bool cruceEnRojo, LaneVerdict carril, float dt)
        {
            if (cruceEnRojo) return StrictFail.RedLight;

            _contravia = carril == LaneVerdict.WrongWay ? _contravia + dt : 0f;
            return _contravia >= ContraviaSostenida ? StrictFail.WrongWay
                                                    : StrictFail.None;
        }
    }
}
