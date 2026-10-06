using UnityEngine;

namespace HablaCamaron.Vehicle
{
    /// <summary>
    /// Receta PURA de la posición del ojo del conductor dentro de la cabina.
    /// El playtest pidió la vista un poco más baja: 0.72 del techo real
    /// (antes 0.80, quedaba a la altura de un bus).
    /// </summary>
    public static class CabinEyeMath
    {
        public const float AlturaOjos = 0.72f;

        public static Vector3 EyeLocalFor(Bounds cabina) => new Vector3(
            -cabina.size.x * 0.18f,
            cabina.max.y * AlturaOjos,
            cabina.center.z + cabina.size.z * 0.08f);
    }
}
