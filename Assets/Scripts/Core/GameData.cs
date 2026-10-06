using UnityEngine;

namespace HablaCamaron.Core
{
    /// <summary>
    /// Datos de la partida. Qué misión está desbloqueada, puntajes por misión.
    /// Se guarda con PlayerPrefs (suficiente para un proyecto académico;
    /// más adelante se puede migrar a JSON en disco sin tocar el resto del código).
    /// </summary>
    [System.Serializable]
    public class GameData
    {
        /// <summary>Capacidad de misiones guardables. Debe cubrir el MissionCatalog
        /// completo (7 del GDD + el bonus "Quito entero") — hay un test que lo
        /// garantiza.</summary>
        public const int MaxMissions = 8;

        // Índice de la misión más alta desbloqueada. 0 = solo tutorial.
        public int HighestUnlockedMission = 0;

        // Puntaje (0-100) por misión. Index = id de misión.
        public int[] MissionScores = new int[MaxMissions];

        private const string KEY_UNLOCKED = "hc_unlocked";
        private const string KEY_SCORE_PREFIX = "hc_score_";

        public void Save()
        {
            PlayerPrefs.SetInt(KEY_UNLOCKED, HighestUnlockedMission);
            for (int i = 0; i < MissionScores.Length; i++)
                PlayerPrefs.SetInt(KEY_SCORE_PREFIX + i, MissionScores[i]);
            PlayerPrefs.Save();
        }

        public void Load()
        {
            HighestUnlockedMission = PlayerPrefs.GetInt(KEY_UNLOCKED, 0);
            for (int i = 0; i < MissionScores.Length; i++)
                MissionScores[i] = PlayerPrefs.GetInt(KEY_SCORE_PREFIX + i, 0);
        }

        /// <summary>Registra el resultado de una misión y desbloquea la siguiente si aprobó (>=70).</summary>
        public void RecordMissionResult(int missionId, int score)
        {
            if (missionId < 0 || missionId >= MissionScores.Length) return;

            // Guarda el mejor puntaje obtenido.
            if (score > MissionScores[missionId])
                MissionScores[missionId] = score;

            // Umbral de aprobación = 70.
            if (score >= 70 && missionId >= HighestUnlockedMission)
                HighestUnlockedMission = missionId + 1;

            Save();
        }
    }
}
