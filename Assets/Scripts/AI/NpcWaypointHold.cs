using HablaCamaron.World;

namespace HablaCamaron.AI
{
    /// <summary>
    /// PURA (testeada): ¿el NPC debe QUEDARSE apuntando al waypoint actual en
    /// vez de saltar al siguiente? Antes de esto, Move() avanzaba el índice
    /// de ruta apenas quedaban menos de 3 m al nodo del semáforo — el MISMO
    /// umbral con el que Sense() deja de "ver" ese nodo (sale de la ventana
    /// _pathIndex..+3) — así que el auto SOLTABA el freno a metros de la
    /// línea y cruzaba en rojo acelerando de nuevo a crucero, sin importar
    /// el perfil: el diagnóstico `SemaforosNpcDiagTests` medía cruces en
    /// rojo hasta con el Particular 100% obediente (LightObedience = 1),
    /// así que NO era un problema de calibración del Taxista — era este
    /// atajo. Mientras el semáforo del waypoint actual siga en rojo/amarillo
    /// Y el NPC decidió respetarlo (`_obeysThisLight`), el índice de ruta NO
    /// avanza: Sense() lo sigue viendo y el freno sigue aplicado hasta que
    /// cambie a verde o el NPC decida no respetarlo (el taxista que se
    /// lanza sigue pudiendo hacerlo — eso es diseño, no bug).
    /// </summary>
    public static class NpcWaypointHold
    {
        /// <param name="trafficLightGroup">Grupo del nodo actual del camino (-1 = sin semáforo).</param>
        /// <param name="obeysThisLight">Decisión ya tomada por el NPC para ESTE semáforo.</param>
        /// <param name="estado">Estado actual del grupo (ignorado si el nodo no tiene semáforo).</param>
        public static bool EsperaSemaforo(int trafficLightGroup, bool obeysThisLight, LightState estado) =>
            trafficLightGroup >= 0 && obeysThisLight && estado != LightState.Green;
    }
}
