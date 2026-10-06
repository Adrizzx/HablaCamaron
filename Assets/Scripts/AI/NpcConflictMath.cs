using UnityEngine;

namespace HablaCamaron.AI
{
    /// <summary>
    /// Matemática PURA de conflicto entre dos vehículos (testeada en EditMode).
    ///
    /// LA PIEZA QUE FALTABA: hasta ahora un NPC solo "veía" con un SphereCast
    /// FRONTAL (tres conos de ±18°). En un cruce o un redondel el otro auto
    /// llega DE COSTADO, fuera de ese cono, así que nadie frena; y como los NPC
    /// son Rigidbody cinemáticos movidos con MovePosition, Unity tampoco genera
    /// contacto entre ellos — no hay red de seguridad cuando el sensor falla, y
    /// se traspasan (medido: 18-34 pares superpuestos por corrida en la Zona Sur).
    ///
    /// La pregunta correcta no es "¿está en mi cono?" sino "¿nos vamos a
    /// encontrar en el mismo sitio?": se proyectan las DOS velocidades y se
    /// calcula el instante de máxima aproximación. Es el mismo tiempo-a-colisión
    /// que NpcBrain ya usa para el de adelante, generalizado a cualquier rumbo.
    /// </summary>
    public static class NpcConflictMath
    {
        /// <summary>Radio en el que un vecino es siquiera relevante (m).</summary>
        public const float RadioVecinos = 25f;

        /// <summary>
        /// Distancia de máxima aproximación por debajo de la cual eso YA es un
        /// choque (m). Un auto mide ~1.9 m de ancho y ~4.4 m de largo; al no
        /// modelar la orientación en el cruce se toma el radio que envuelve al
        /// par con algo de margen.
        /// </summary>
        public const float RadioConflicto = 3.4f;

        /// <summary>
        /// Segundos hasta el punto de máxima aproximación, si esa aproximación
        /// baja de <paramref name="radio"/>. float.MaxValue cuando no hay
        /// conflicto: se alejan, van a la par, o se cruzan lo bastante lejos.
        /// Todo en el plano XZ (la altura la resuelve el apoyo al piso).
        /// </summary>
        public static float TiempoAColision(Vector2 posRelativa, Vector2 velRelativa, float radio)
        {
            // Ya encima: conflicto inmediato (no "dentro de t segundos").
            if (posRelativa.sqrMagnitude <= radio * radio) return 0f;

            float v2 = velRelativa.sqrMagnitude;
            // Misma velocidad y rumbo: la distancia no cambia, nunca se tocan.
            if (v2 < 1e-4f) return float.MaxValue;

            float t = -Vector2.Dot(posRelativa, velRelativa) / v2;
            if (t <= 0f) return float.MaxValue; // el punto más cercano ya quedó atrás: se alejan

            Vector2 masCerca = posRelativa + velRelativa * t;
            if (masCerca.sqrMagnitude > radio * radio) return float.MaxValue; // pasan de largo

            return t;
        }

        /// <summary>
        /// ¿El otro viene por MI DERECHA? Es la mitad de la regla de prioridad
        /// del cruce sin señal (en Ecuador cede quien tiene al otro a su
        /// derecha), así que se calcula aparte del tiempo.
        /// </summary>
        public static bool PorLaDerecha(Vector3 miDerecha, Vector3 haciaElOtro)
        {
            miDerecha.y = 0f;
            haciaElOtro.y = 0f;
            return Vector3.Dot(miDerecha.normalized, haciaElOtro.normalized) > 0f;
        }

        /// <summary>Componentes XZ de un vector, para no repetir la conversión.</summary>
        public static Vector2 Plano(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>Diferencia de tiempos por debajo de la cual se considera
        /// que los dos llegan A LA VEZ y hay que desempatar (s).</summary>
        public const float EmpateSegundos = 0.35f;

        /// <summary>
        /// Segundos que tarda CADA UNO en llegar al punto donde se cruzan sus
        /// trayectorias. Devuelve false si no se cruzan (paralelos) o si alguno
        /// ya lo dejó atrás.
        ///
        /// POR QUÉ ESTO Y NO "LA REGLA DE LA DERECHA": las dos normas reales se
        /// contradicen. En un cruce sin señal cede quien tiene al otro a su
        /// derecha; pero en un redondel (que en tránsito por la derecha se
        /// recorre en sentido ANTIHORARIO) el que ya circula llega por la
        /// IZQUIERDA del que entra, así que la regla de la derecha haría ceder
        /// justo al que tiene prioridad. Comparar quién llega antes resuelve
        /// los dos casos sin que el NPC necesite saber qué es un redondel: el
        /// que ya circula está más cerca del punto de fusión, llega antes, y el
        /// que entra cede. La geometría hace el trabajo.
        /// </summary>
        public static bool TiemposAlCruce(Vector2 posA, Vector2 velA, Vector2 posB, Vector2 velB,
            out float tiempoA, out float tiempoB)
        {
            tiempoA = tiempoB = float.MaxValue;

            float cruz = velA.x * velB.y - velA.y * velB.x;
            if (Mathf.Abs(cruz) < 1e-4f) return false; // paralelos (o alguno parado)

            Vector2 d = posB - posA;
            float tA = (d.x * velB.y - d.y * velB.x) / cruz;
            float tB = (d.x * velA.y - d.y * velA.x) / cruz;
            if (tA <= 0f || tB <= 0f) return false; // alguno ya pasó el cruce

            tiempoA = tA;
            tiempoB = tB;
            return true;
        }

        /// <summary>
        /// ¿Cedo yo? Cede quien llegue DESPUÉS al cruce. Si llegan a la vez
        /// (dentro de EmpateSegundos) manda el id menor — un desempate
        /// determinista y ANTISIMÉTRICO es lo único que rompe el bloqueo: sin
        /// criterio, o ceden los dos (se quedan clavados para siempre) o no
        /// cede ninguno (se traspasan). La simetría es la que mata.
        /// </summary>
        public static bool DeboCeder(float miTiempo, float suTiempo, int miId, int suId)
        {
            if (Mathf.Abs(miTiempo - suTiempo) > EmpateSegundos) return miTiempo > suTiempo;
            return miId > suId;
        }
    }
}
