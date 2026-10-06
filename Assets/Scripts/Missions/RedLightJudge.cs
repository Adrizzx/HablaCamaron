using UnityEngine;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// El juez del semáforo en rojo, PURO (testeado en EditMode). Frenar
    /// llegando a la línea es LEGAL aunque se entre al radio del nodo: la
    /// infracción existe recién cuando el auto CRUZA el nodo (le queda atrás)
    /// habiendo pasado POR su carril — no rozándolo de costado, como al
    /// circular el anillo del redondel junto a la entrada de otro brazo.
    /// PlayerInfractions lo consulta mientras el semáforo está en rojo.
    /// </summary>
    public static class RedLightJudge
    {
        /// <summary>Tolerancia lateral (m): pasó POR el nodo si la trayectoria
        /// quedó a menos de esto (los nodos del grafo son por carril).</summary>
        public const float LateralTolerance = 3f;

        /// <summary>true si el auto ya dejó el nodo ATRÁS habiendo pasado por
        /// encima de su carril (cruzó la línea). Con el nodo todavía adelante
        /// devuelve false: el conductor aún puede detenerse a tiempo.</summary>
        public static bool CrossedNode(Vector3 carPos, Vector3 carForward, Vector3 nodePos)
        {
            Vector3 to = nodePos - carPos; to.y = 0f;
            Vector3 f = carForward; f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) return false;
            f.Normalize();

            float along = Vector3.Dot(to, f);
            if (along >= 0f) return false; // el nodo sigue adelante: puede parar

            Vector3 lateral = to - f * along;
            return lateral.magnitude <= LateralTolerance;
        }
    }
}
