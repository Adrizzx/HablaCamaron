namespace HablaCamaron.AI
{
    /// <summary>
    /// PURA (testeada): ¿este NPC cuenta como "esperando el semáforo" para que
    /// NpcDriver NO lo trate como atasco? Bug de la revisión final: Sense()
    /// solo "ve" el semáforo si el nodo cae en la ventana _pathIndex..+3 y a
    /// ≤25 m — en una cola larga (Hora pico, Quito entero) el 3º o 4º auto ya
    /// no lo ve, así que la exclusión original (`!(RedLightAhead &&
    /// obeysThisLight)`) no lo cubría: a los 3 s el desatasco lo hacía
    /// retroceder y esquivar (hasta 2.5 m de deriva) y a los 6 s abandonaba la
    /// fila replanificando, mientras el líder esperaba correctamente en rojo.
    /// La espera se PROPAGA hacia atrás por toda la cola: si el auto de
    /// adelante ya espera Y yo voy casi parado, yo también espero — auto por
    /// auto. Un frame de retraso en la propagación (el de adelante calculó SU
    /// EsperandoSemaforo el frame anterior) es aceptable: el umbral del
    /// desatasco son 3 s completos.
    /// </summary>
    public static class NpcQueueWait
    {
        /// <summary>m/s por debajo del cual el NPC se considera "casi parado"
        /// — mismo umbral que ya usa Sense() para AheadIsStatic.</summary>
        public const float VelocidadCasiParado = 0.4f;

        /// <param name="veSemaforoYObedece">El propio NPC ve el rojo/amarillo
        /// dentro de su ventana de sensado y decidió respetarlo.</param>
        /// <param name="speed">Velocidad actual del NPC (m/s).</param>
        /// <param name="adelanteEspera">El auto inmediatamente de adelante
        /// (si hay) ya cuenta como esperando semáforo.</param>
        public static bool Espera(bool veSemaforoYObedece, float speed, bool adelanteEspera) =>
            veSemaforoYObedece || (speed < VelocidadCasiParado && adelanteEspera);
    }
}
