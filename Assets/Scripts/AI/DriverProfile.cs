using UnityEngine;

namespace HablaCamaron.AI
{
    /// <summary>
    /// Personalidad de un conductor NPC. Los TRES perfiles quiteños del GDD
    /// (taxista, buseta, particular) usan el MISMO cerebro (NpcBrain) con estos
    /// números distintos — el comportamiento emerge de los datos, no del código.
    /// </summary>
    [CreateAssetMenu(fileName = "DriverProfile", menuName = "Habla Camarón/Perfil de conductor")]
    public class DriverProfile : ScriptableObject
    {
        [Header("Identidad")]
        public string DisplayName = "Particular";

        [Header("Velocidades (m/s)")]
        public float CruiseSpeed = 9f;      // ~32 km/h urbano
        public float Acceleration = 4f;
        public float BrakeDecel = 9f;

        [Header("Convivencia vial")]
        [Tooltip("Distancia mínima al de adelante antes de frenar en seco.")]
        public float MinGap = 5f;
        // Probado y DESCARTADO (Tarea 7, ronda de revisión 2026-07-27): el
        // residuo de cruces en rojo que quedaba incluso con los perfiles
        // bien calibrados es la "zona de dilema" (el semáforo cambia cuando
        // el NPC ya no tiene distancia física para parar). Cálculo del peor
        // caso (Taxista, el perfil más rápido): frenado puro
        // d = v²/(2·a) = 12²/(2·9) = 144/18 = 8 m — con 14 m ya había margen
        // de sobra sobre eso. Se probó subir a 20 m (2,5× el frenado puro,
        // bajo el techo de detección de 25 m de NpcDriver.Sense) y medir con
        // SemaforosNpcDiagTests 3 veces: 0,80 / 1,00 (FALLA) / 1,00 (FALLA)
        // por minuto — PEOR que el promedio con 14 m (0,73/0,40/0,40/0,93,
        // todas verdes) y lejos del objetivo (≤0,5/min en la peor de 3). No
        // se confirmó una causa — la varianza de esta medición ya es alta
        // por el no-determinismo de Physics.RaycastAll/SphereCast entre
        // corridas (ver comentario en SemaforosNpcDiagTests) — pero subir el
        // valor NO mostró la mejora esperada, así que se revirtió a 14 m: no
        // vale la pena arriesgar un cambio de comportamiento de manejo (los
        // NPC frenarían antes en TODAS las misiones) sin evidencia de que
        // ayude. La ventana del test de regresión se alargó en su lugar.
        [Tooltip("A qué distancia de un semáforo en rojo empieza a frenar.")]
        public float LightBrakeDistance = 14f;
        [Tooltip("0-1: probabilidad de RESPETAR un semáforo en rojo (el taxista no siempre).")]
        [Range(0f, 1f)] public float LightObedience = 1f;
        [Tooltip("0-1: probabilidad de detenerse en doble fila un rato (la buseta sí).")]
        [Range(0f, 1f)] public float DoubleParkChance = 0f;
        public float DoubleParkDuration = 6f;

        // ---- Los tres perfiles del GDD, listos para el TrafficManager ----

        public static DriverProfile Taxista()
        {
            var p = CreateInstance<DriverProfile>();
            p.DisplayName = "Taxista";
            p.CruiseSpeed = 12f;          // apurado
            p.Acceleration = 6f;
            p.MinGap = 3f;                // se pega
            // Medido con SemaforosNpcDiagTests (Tarea 7, 2026-07-27): con
            // 0.65 el taxista era, con ventaja clara, el perfil que más
            // cruces en rojo aportaba (Taxista 8/60 vs Buseta 0/60 y
            // Particular 2/60 — los otros dos ya casi no fallaban una vez
            // corregidos los bugs reales de frenado). Sigue siendo rápido y
            // pegado, pero deja de ser el caos vial que veía el jugador.
            p.LightObedience = 0.9f;      // como la buseta: el rojo ya no es "sugerencia"
            p.DoubleParkChance = 0.10f;   // recoge pasajeros donde sea
            p.DoubleParkDuration = 4f;
            return p;
        }

        public static DriverProfile Buseta()
        {
            var p = CreateInstance<DriverProfile>();
            p.DisplayName = "Buseta";
            p.CruiseSpeed = 8f;           // pesada, arranca lento
            p.Acceleration = 2.5f;
            p.MinGap = 6f;
            p.LightObedience = 0.9f;
            p.DoubleParkChance = 0.35f;   // "¡sube, sube!" en plena vía
            p.DoubleParkDuration = 8f;
            return p;
        }

        /// <summary>
        /// PATRULLERO. El playtest se queja de que "los patrulleros hacen lo
        /// que les da la gana", y tenía razón por una causa que no estaba en el
        /// plan: **Toon City no trae ningún vehículo policial**. Ni prefab, ni
        /// material, ni textura (comprobado en Prefabs/Vehicles y en
        /// Models/Materials). Lo que el jugador lee como patrulla es un auto
        /// común con el cerebro genérico — de ahí que se comporte como
        /// cualquiera. Así que esto es una DESIGNACIÓN, no un descubrimiento:
        /// un modelo concreto pasa a conducirse como patrulla.
        /// Perfil: rápido pero IMPECABLE — respeta el rojo siempre y jamás para
        /// en doble fila. Es lo que hace que se lea como autoridad.
        /// </summary>
        public static DriverProfile Patrullero()
        {
            var p = CreateInstance<DriverProfile>();
            p.DisplayName = "Patrullero";
            p.CruiseSpeed = 11f;          // ágil, sin ser el taxista
            p.Acceleration = 5.5f;
            p.BrakeDecel = 10f;           // frena mejor que nadie
            p.MinGap = 6f;                // guarda distancia: da ejemplo
            p.LightObedience = 1f;        // el rojo NO se discute
            p.DoubleParkChance = 0f;      // nunca estorba la vía
            return p;
        }

        public static DriverProfile Particular()
        {
            var p = CreateInstance<DriverProfile>();
            p.DisplayName = "Particular";
            p.CruiseSpeed = 9f;
            p.Acceleration = 4f;
            p.MinGap = 5f;
            p.LightObedience = 1f;        // conservador
            p.DoubleParkChance = 0f;
            return p;
        }
    }
}
