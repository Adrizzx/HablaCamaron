using UnityEngine;
using HablaCamaron.UI;

namespace HablaCamaron.Missions
{
    /// <summary>Lo que pasó durante la misión (lo cuenta PlayerInfractions).</summary>
    public struct MissionStats
    {
        public bool ReachedGoal;
        public float TimeUsed;
        public float TimeLimit;
        public int Stalls;          // motor calado
        public int Grinds;          // cambios sin embrague
        public int RedLightsRun;    // semáforos en rojo pasados
        public int SpeedingTickets; // señales de límite excedidas
        public int Collisions;      // golpes contra NPCs / entorno
        public int MissedBlinkers;  // giros francos sin direccional
        public int CrosswalkBlocks; // paradas ENCIMA del paso cebra en rojo
        public int WrongWays;       // contravía sostenida (carril contrario)
        public int OffRoads;        // se salió de las calles y hubo que devolverlo
    }

    /// <summary>
    /// La fórmula de evaluación del GDD (0-100, aprueba con 70) como función
    /// PURA: estadísticas → desglose. Los 6 criterios exactos del documento:
    /// objetivo 30 · señales 20 (−4 c/u) · técnica 20 · defensiva 15 ·
    /// tiempo 10 · vehículo 5. Testeada caso por caso en EditMode.
    /// </summary>
    public static class MissionScoring
    {
        // ---- Reparto de técnica y defensiva (2026-07-25) ----
        // El playtest pidió notas más exigentes: "está dando muy alta". Antes
        // los 70 puntos que no son el objetivo empezaban LLENOS y solo se
        // restaba, así que llegar de cualquier manera ya aprobaba. Ahora una
        // parte se GANA manejando limpio: la suma máxima no cambia (20 y 15,
        // el reparto del GDD), pero hay que merecerla.
        public const int TechniqueBase = 12, TechniqueBonus = 8;
        public const int DefensiveBase = 9, DefensiveBonus = 6;

        /// <summary>Hasta esta fracción del tiempo límite, el tiempo vale 10.</summary>
        public const float TimeFullUntil = 0.6f;

        public static EvaluationResult Compute(in MissionStats s, int missionId)
        {
            var r = new EvaluationResult { missionId = missionId };

            // 1) Cumplimiento del objetivo (30): llegó o no llegó.
            r.scoreObjective = s.ReachedGoal ? 30 : 0;

            // 2) Señales y semáforos (20): −5 por infracción (semáforo en rojo,
            //    límite excedido o CONTRAVÍA) y −3 por bloquear un paso cebra
            //    detenido en rojo. Originalmente 4 y 2 (se aprobaba de sobra con
            //    dos rojos); se subió a 7 y 4, y el playtest lo encontró DURO:
            //    5 y 3 castigan de verdad sin volver el juego injusto.
            r.scoreSignals = Mathf.Max(0,
                20 - (s.RedLightsRun + s.SpeedingTickets + s.WrongWays) * 5
                   - s.CrosswalkBlocks * 3);

            // 3) Técnica de manejo (20): NO se regala. Se parte de una base y
            //    los 8 puntos de arriba se GANAN con una corrida sin calar ni
            //    rechinar la caja — que es justo lo que el juego enseña.
            //    Calar cuesta 5 y rechinar 3 (antes 3 y 2).
            //    Calar cuesta 4 y rechinar 2: calar es EL error del que aprende
            //    a manejar y a 5 puntos un par de calados hundían la nota.
            r.scoreTechnique = Mathf.Max(0,
                TechniqueBase + (s.Stalls == 0 && s.Grinds == 0 ? TechniqueBonus : 0)
                - s.Stalls * 4 - s.Grinds * 2);

            // 4) Conducción defensiva (15): mismo criterio — base más un bono
            //    por manejar limpio (sin choques ni giros sin avisar). Chocar
            //    cuesta 6, no avisar 3 y salirse de la vía 3.
            r.scoreDefensive = Mathf.Max(0,
                DefensiveBase +
                (s.Collisions == 0 && s.MissedBlinkers == 0 && s.OffRoads == 0
                    ? DefensiveBonus : 0)
                - s.Collisions * 5 - s.MissedBlinkers * 2 - s.OffRoads * 2);

            // 5) Tiempo (10): deja de ser un regalo por llegar. Se puntúa la
            //    FRACCIÓN del límite consumida — llegar holgado vale 10, apurar
            //    hasta la bocina vale poco, y pasarse sigue degradando a 0.
            if (!s.ReachedGoal || s.TimeLimit <= 0f) r.scoreTime = 0;
            else
            {
                float frac = s.TimeUsed / s.TimeLimit;
                if (frac <= TimeFullUntil) r.scoreTime = 10;
                else if (frac <= 1f)
                    r.scoreTime = Mathf.RoundToInt(
                        Mathf.Lerp(10f, 2f, (frac - TimeFullUntil) / (1f - TimeFullUntil)));
                else r.scoreTime = Mathf.Max(0,
                    2 - Mathf.CeilToInt((s.TimeUsed - s.TimeLimit) / 15f));
            }

            // 6) Estado del vehículo (5): el Aveo es del papá (−3 por choque).
            r.scoreVehicle = Mathf.Max(0, 5 - s.Collisions * 3);

            r.totalScore = r.scoreObjective + r.scoreSignals + r.scoreTechnique +
                           r.scoreDefensive + r.scoreTime + r.scoreVehicle;

            r.donPanchoMessage = Verdict(r.totalScore, s);
            return r;
        }

        private static string Verdict(int total, in MissionStats s)
        {
            if (!s.ReachedGoal)
                return "No llegamos esta vez, mijo. Sin pena: otra vuelta y le sale.";
            if (total >= 90)
                return "¡Habla, camarón! Así se maneja en Quito.";
            if (total >= 70)
                return s.Stalls > 0
                    ? "Aprobado, mijo. Le falta soltura en el embrague, pero va aprendiendo."
                    : "Bien hecho, guambra. Aprobado con todas las de la ley.";
            return "Toca repetir, camarón. " +
                   (s.RedLightsRun > 0 ? "Esas luces rojas no son de adorno." :
                    s.Collisions > 0 ? "El carro del papá no es chocón, mijo." :
                    "Con calma y embrague suave.");
        }
    }
}
