using UnityEngine;

namespace HablaCamaron.World
{
    /// <summary>
    /// Señal de tránsito como DATO sobre el objeto visual (poste de Toon City,
    /// textura ecuatoriana en Fase 4). Referencia los nodos del grafo a los que
    /// aplica: los NPCs la obedecen según su perfil y el puntaje penaliza al
    /// jugador que la ignora (Fase 3).
    /// </summary>
    public class RoadSign : MonoBehaviour
    {
        public SignType Type = SignType.Stop;

        [Tooltip("Ids de los nodos del RoadGraph a los que esta señal aplica.")]
        public int[] AffectedNodeIds = new int[0];

        /// <summary>Límite en km/h de una señal de velocidad (0 = no es de velocidad).
        /// PURA: la usan el puntaje (Fase 3) y sus tests.</summary>
        public static int LimitKmh(SignType type) => type switch
        {
            SignType.SpeedLimit30 => 30,
            SignType.SpeedLimit50 => 50,
            SignType.SpeedLimit90 => 90,
            _ => 0,
        };
    }
}
