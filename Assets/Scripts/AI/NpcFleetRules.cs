namespace HablaCamaron.AI
{
    /// <summary>
    /// Reglas de la FLOTA NPC por zona (playtest 2026-07-14: los vehículos
    /// grandes se bugueaban en los redondeles — cortaban la curva del anillo
    /// y quedaban trabados). PURO y testeado en EditMode: el inyector de
    /// tráfico (menú 6) mide cada prefab de Toon City y pregunta aquí cuáles
    /// entran en cada zona.
    /// </summary>
    public static class NpcFleetRules
    {
        /// <summary>Largo máximo (m) para circular en una zona CON redondel:
        /// más largo que esto no toma la curva del anillo sin salirse.
        /// OJO con la escala TOON: los autos comunes de Toon City miden
        /// 5.6-6.0 m (medidos por el inyector) — los que se buguean son las
        /// busetas y camiones de 7.6-12.2 m. El tope va entre ambos grupos.</summary>
        public const float MaxLengthWithRoundabout = 6.5f;

        /// <summary>¿Este vehículo puede circular en esta zona?</summary>
        public static bool Fits(bool zoneHasRoundabout, float lengthMeters) =>
            !zoneHasRoundabout || lengthMeters <= MaxLengthWithRoundabout;

        /// <summary>Qué zonas exigen flota chica (dato de los builders): la
        /// Zona Sur y el Corredor por su redondel, y la Ciudad Toon por sus
        /// esquinas de grilla apretadas; la Simón es avenida abierta.</summary>
        public static bool ZoneHasRoundabout(string scenePath) =>
            scenePath.Contains("N1_ZonaSur") || scenePath.Contains("N1_CorredorExamen") ||
            scenePath.Contains("N1_CiudadToon");

        /// <summary>
        /// Modelo designado como PATRULLA. Toon City no trae ningún vehículo
        /// policial —ni prefab, ni material, ni textura: comprobado en
        /// Prefabs/Vehicles y Models/Materials—, así que no hay nada que
        /// "detectar": se DESIGNA uno y se conduce distinto. Sin esto, lo que
        /// el jugador lee como patrulla es un auto común con el cerebro
        /// genérico, y por eso "hace lo que le da la gana".
        /// </summary>
        public const string ModeloPatrulla = "Car_16A";

        /// <summary>¿Este objeto es la patrulla? El TrafficManager nombra a sus
        /// NPC "NPC_&lt;prefab&gt;", así que basta con buscar el modelo dentro.</summary>
        public static bool EsPatrulla(string nombreDelObjeto) =>
            !string.IsNullOrEmpty(nombreDelObjeto) &&
            nombreDelObjeto.Contains(ModeloPatrulla);
    }
}
