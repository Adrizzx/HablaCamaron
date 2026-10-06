using UnityEngine;

namespace HablaCamaron.World
{
    /// <summary>
    /// Un PASO CEBRA como dato (los pinta CrosswalkKit en el editor, junto a
    /// cada semáforo): rectángulo local + grupo de semáforo que lo gobierna.
    /// La regla de tránsito vive en Missions.CrosswalkJudge: detenerse ENCIMA
    /// de la cebra con el semáforo en rojo bloquea el paso de la gente (−2 en
    /// señales). El paquete Toon City no trae peatones (decisión documentada):
    /// la funcionalidad es la regla + el respeto al ciclo del semáforo.
    /// </summary>
    public class Crosswalk : MonoBehaviour
    {
        /// <summary>Grupo de TrafficLightCycle que la gobierna.</summary>
        public int LightGroup;

        /// <summary>Media caja LOCAL de la zona pintada (x = ancho de la vía,
        /// z = profundidad de las franjas).</summary>
        public Vector3 HalfExtents = new Vector3(4f, 2f, 1.6f);

        /// <summary>¿Este punto del mundo está sobre la cebra? (en XZ local).</summary>
        public bool Contains(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            return Mathf.Abs(local.x) <= HalfExtents.x &&
                   Mathf.Abs(local.z) <= HalfExtents.z;
        }
    }
}
