namespace HablaCamaron.Missions
{
    /// <summary>Por qué se reprueba de golpe (None = todo en orden).</summary>
    public enum FailReason { None, OffRoad, Rollover }

    /// <summary>
    /// El juez de la reprobación DURA (playtest 2026-07-14): salirse de la vía
    /// o volcar el auto termina la misión al instante y obliga a repetir el
    /// nivel. PURO (testeado en EditMode): acumula tiempo SOSTENIDO en cada
    /// falta y perdona los parpadeos (un brinco de la física no reprueba a
    /// nadie). MissionRunner lo consulta cada ratito con la distancia a la
    /// vía (RoadGraphData.DistanceToNearestEdge) y la inclinación del auto.
    /// </summary>
    public class DrivingFailJudge
    {
        /// <summary>Más lejos que esto de cualquier vía del grafo = fuera de la calle.
        /// Holgado a propósito: el anillo del redondel se recorre por fuera de la
        /// línea de nodos (las aristas son cuerdas del círculo).</summary>
        public const float OffRoadMeters = 10f;
        /// <summary>Segundos sostenidos fuera de la vía para reprobar.
        /// TIENE que ser mayor que `RoadDiscipline.OffRoadReturnAt`: ese sistema
        /// ya avisa ("VUELVE A LA CALLE") y devuelve solo al jugador a la vía
        /// más cercana antes de esto. Con los 2s originales, la reprobación
        /// dura ganaba SIEMPRE la carrera contra el aviso suave — el jugador
        /// veía la pantalla de "reprobado" sin ningún aviso previo por
        /// cualquier desvío de 10m sostenido 2s (esquivar un NPC, un choque
        /// que empuja el auto), y el teletransporte de `RoadDiscipline` quedaba
        /// como código inalcanzable. Este juez pasa a ser el ÚLTIMO RECURSO,
        /// solo si el sistema suave no logra devolver al jugador a la vía
        /// (grafo roto, `NearestLanePoint` sin resultado).</summary>
        public const float OffRoadSeconds = RoadDiscipline.OffRoadReturnAt + 3f;
        /// <summary>up·Y por debajo de esto = el auto está de lado o de techo.</summary>
        public const float RolloverUpDot = 0.35f;
        /// <summary>Segundos sostenidos volcado para reprobar.</summary>
        public const float RolloverSeconds = 1.5f;

        private float _offRoadFor, _rolledFor;
        // El garaje/spawn inicial de una misión puede caer a más de
        // OffRoadMeters del grafo (mismo caso ya documentado en RoadDiscipline):
        // no se juzga "fuera de la vía" hasta que el auto haya pisado la calle
        // una vez. Sin este guard, el tutorial del embrague (misión 0) podía
        // reprobarse solo por tardar en sacar el auto del garaje.
        private bool _touchedRoad;

        /// <summary>Un latido del juez. El vuelco manda sobre la salida de vía.</summary>
        public FailReason Tick(float distToRoad, float upDot, float dt)
        {
            if (distToRoad <= OffRoadMeters) _touchedRoad = true;

            _offRoadFor = (_touchedRoad && distToRoad > OffRoadMeters) ? _offRoadFor + dt : 0f;
            _rolledFor = upDot < RolloverUpDot ? _rolledFor + dt : 0f;

            if (_rolledFor >= RolloverSeconds) return FailReason.Rollover;
            if (_offRoadFor >= OffRoadSeconds) return FailReason.OffRoad;
            return FailReason.None;
        }
    }
}
