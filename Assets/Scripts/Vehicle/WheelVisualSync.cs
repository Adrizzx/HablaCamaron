using UnityEngine;

namespace HablaCamaron.Vehicle
{
    /// <summary>
    /// Copia la posición y rotación de cada WheelCollider (física invisible)
    /// a la malla de rueda del modelo de Toon City (lo que se ve).
    /// Los pares los asigna automáticamente el armador de escena.
    /// </summary>
    public class WheelVisualSync : MonoBehaviour
    {
        [System.Serializable]
        public struct Pair
        {
            public WheelCollider Collider;
            public Transform Visual;
        }

        public Pair[] Wheels;

        private void LateUpdate()
        {
            if (Wheels == null) return;
            foreach (var w in Wheels)
            {
                if (w.Collider == null || w.Visual == null) continue;
                w.Collider.GetWorldPose(out var pos, out var rot);
                w.Visual.SetPositionAndRotation(pos, rot);
            }
        }
    }
}
