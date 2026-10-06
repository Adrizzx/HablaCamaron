using UnityEngine;

namespace HablaCamaron.AI
{
    /// <summary>
    /// Geometría PURA del sensor frontal del NPC. En pendiente, un cast
    /// horizontal desde el morro muerde el propio asfalto y la FSM ve un
    /// "obstáculo" eterno (playtest: NPCs parados en la cuesta sin razón).
    /// La receta: origen elevado sobre la NORMAL de la vía y dirección
    /// proyectada al plano de la vía.
    /// </summary>
    public static class NpcSensorMath
    {
        public const float AlturaSensor = 1.0f; // > radio 0.8 del SphereCast

        public static (Vector3 origen, Vector3 dir) ForwardRay(
            Vector3 origen, Vector3 forward, Vector3 normalPiso)
        {
            var n = normalPiso.sqrMagnitude < 1e-6f ? Vector3.up : normalPiso.normalized;
            var dir = Vector3.ProjectOnPlane(forward, n);
            if (dir.sqrMagnitude < 1e-6f) dir = forward;
            return (origen + n * AlturaSensor, dir.normalized);
        }
    }
}
