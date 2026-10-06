using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.Missions
{
    /// <summary>Dónde está el jugador respecto del camino de la misión.</summary>
    public enum CorridorVerdict
    {
        /// <summary>Dentro del corredor: manejando por donde toca.</summary>
        OnRoute,
        /// <summary>Se está desviando: aviso en pantalla, todavía sin castigo.</summary>
        Straying,
        /// <summary>Lejos y sin volver: se acabó la misión.</summary>
        Lost,
    }

    /// <summary>
    /// El juez del CORREDOR DE RUTA (playtest 2026-07-25: "en el nivel 4 debe
    /// dejar ir solo por donde puede, no ser libre"). La misión traza su camino
    /// con A* al empezar y el jugador debe mantenerse cerca de ÉL: la ciudad es
    /// enorme y sin esto el nivel se convertía en pasear por donde uno quisiera.
    ///
    /// La ruta NO se recalcula sobre la marcha a propósito: si se recalculara
    /// desde donde está el jugador, cualquier desvío pasaría a ser "la ruta" y
    /// el corredor no querría decir nada.
    ///
    /// PURO (sin Unity más allá de Vector3) para probarlo en EditMode. Acumula
    /// tiempo fuera y perdona los cruces breves — cortar una esquina ancha o
    /// esquivar un NPC no puede costar la misión.
    /// </summary>
    public sealed class RouteCorridorJudge
    {
        /// <summary>Media anchura del corredor (m). Generosa: la ruta va por el
        /// eje de los carriles y hay que caber con manzana de por medio.</summary>
        public const float CorridorHalfWidth = 30f;

        /// <summary>Segundos fuera del corredor antes de avisar.</summary>
        public const float StrayingSeconds = 2.5f;

        /// <summary>Segundos fuera del corredor para dar la misión por perdida.</summary>
        public const float LostSeconds = 14f;

        private float _outFor;

        /// <summary>Segundos que lleva fuera (para la cuenta atrás del HUD).</summary>
        public float OutFor => _outFor;

        /// <summary>Un latido del juez con la distancia a la ruta de la misión.</summary>
        public CorridorVerdict Tick(float distanceToRoute, float dt)
        {
            if (distanceToRoute <= CorridorHalfWidth)
            {
                _outFor = 0f;
                return CorridorVerdict.OnRoute;
            }

            _outFor += dt;
            if (_outFor >= LostSeconds) return CorridorVerdict.Lost;
            if (_outFor >= StrayingSeconds) return CorridorVerdict.Straying;
            return CorridorVerdict.OnRoute; // colchón: un roce no es desviarse
        }

        /// <summary>
        /// Distancia XZ de un punto a la polilínea de la ruta (el camino como
        /// tramos rectos entre nodos). float.MaxValue si no hay ruta.
        /// </summary>
        public static float DistanceToRoute(IReadOnlyList<Vector3> route, Vector3 position)
        {
            if (route == null || route.Count == 0) return float.MaxValue;
            if (route.Count == 1) return PlanarDistance(route[0], position);

            float best = float.MaxValue;
            for (int i = 1; i < route.Count; i++)
            {
                float d = DistanceToSegmentXZ(position, route[i - 1], route[i]);
                if (d < best) best = d;
            }
            return best;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b) =>
            new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private static float DistanceToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 p2 = new Vector2(p.x, p.z);
            Vector2 a2 = new Vector2(a.x, a.z);
            Vector2 ab = new Vector2(b.x, b.z) - a2;
            float len = ab.sqrMagnitude;
            float t = len < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / len);
            return (p2 - (a2 + ab * t)).magnitude;
        }
    }
}
