using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.World
{
    /// <summary>
    /// Matemática PURA de los límites del mundo jugable: de los puntos del
    /// grafo vial sale el rectángulo XZ (con margen) donde se levantan los
    /// muros invisibles. Testeada en EditMode.
    /// </summary>
    public static class WorldLimitsMath
    {
        /// <summary>Rectángulo XZ que encierra los puntos + margen. Sin puntos → ceros.</summary>
        public static bool Bounds(IReadOnlyList<Vector3> points, float margin,
                                  out Vector3 min, out Vector3 max)
        {
            min = Vector3.zero; max = Vector3.zero;
            if (points == null || points.Count == 0) return false;

            min = max = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                var p = points[i];
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            min -= new Vector3(margin, 0f, margin);
            max += new Vector3(margin, 0f, margin);
            return true;
        }
    }

    /// <summary>
    /// Va montado en cada muro límite invisible: cuando el jugador lo topa,
    /// Don Pancho le avisa que por ahí no es (con tregua para no repetirse).
    /// </summary>
    public class WorldBoundary : MonoBehaviour
    {
        public const float Cooldown = 8f;
        private static float _nextWarn; // compartida: los 4 muros no se turnan para regañar

        private void OnCollisionEnter(Collision c)
        {
            if (Time.time < _nextWarn) return;
            if (c.rigidbody == null ||
                c.rigidbody.GetComponent<Vehicle.VehicleController>() == null) return;

            _nextWarn = Time.time + Cooldown;
            UI.DonPanchoDialogue.Instance?.Trigger(UI.DialogueEvent.OutOfBounds);
        }
    }
}
