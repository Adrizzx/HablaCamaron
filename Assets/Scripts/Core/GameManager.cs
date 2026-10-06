using UnityEngine;

namespace HablaCamaron.Core
{
    /// <summary>
    /// Punto central del juego. Vive una sola vez y persiste entre escenas.
    /// Guarda el estado de la partida (qué misión va, puntajes) y da acceso
    /// global a los managers. Patrón Singleton.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Estado de la partida")]
        public GameData Data;

        private void Awake()
        {
            // Si ya existe una instancia, esta es duplicada -> destruir.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject); // sobrevive al cambio de escena

            if (Data == null)
                Data = new GameData();

            Data.Load(); // carga progreso guardado (PlayerPrefs)
        }

        /// <summary>Reinicia el progreso para una partida nueva.</summary>
        public void NewGame()
        {
            Data = new GameData();
            Data.Save();

            // Limpia los residuos de la partida anterior: sin esto, el mapa
            // mostraría un chat de Mishel viejo o reabriría la misión equivocada.
            PlayerPrefs.DeleteKey("hc_pending_chat");
            PlayerPrefs.DeleteKey("hc_current_mission");
            PlayerPrefs.Save();
        }

        /// <summary>True si existe una partida guardada para "Continuar".</summary>
        public bool HasSavedGame()
        {
            return Data != null && Data.HighestUnlockedMission > 0;
        }
    }
}
