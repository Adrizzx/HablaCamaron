using System.Collections.Generic;

namespace HablaCamaron.AI
{
    /// <summary>
    /// Matemática PURA del apoyo al piso de los NPCs (testeada en EditMode).
    /// El rayo hacia abajo devuelve TODAS las superficies bajo el auto (la vía,
    /// el relleno de la meseta... y también el techo del arco de la meta o una
    /// valla). Elegir siempre "la más alta" teletransportaba los autos ENCIMA
    /// de arcos y puentes ("se van para arriba"). La regla correcta: la más
    /// alta que el auto podría PISAR desde donde está (un escalón razonable),
    /// nunca un techo por encima de su capó.
    /// </summary>
    public static class NpcGroundMath
    {
        /// <summary>Máximo escalón que un auto "sube" entre frames (cuestas
        /// incluidas: la pendiente por frame es de centímetros).</summary>
        public const float MaxStepUp = 1.6f;

        /// <summary>
        /// Elige la Y de apoyo entre las superficies detectadas: la más alta
        /// que no supere currentY + MaxStepUp. Si todas son techos inalcanzables
        /// (auto bajo un puente sin piso detectado) devuelve false: mejor
        /// conservar la altura actual que saltar arriba.
        /// </summary>
        public static bool TryPick(IReadOnlyList<float> surfaceYs, float currentY, out float pickedY)
        {
            pickedY = currentY;
            bool found = false;
            float limit = currentY + MaxStepUp;
            for (int i = 0; i < surfaceYs.Count; i++)
            {
                float y = surfaceYs[i];
                if (y > limit) continue; // techo de arco/valla/puente: no es piso
                if (!found || y > pickedY) { pickedY = y; found = true; }
            }
            return found;
        }

        /// <summary>
        /// Igual que TryPick, pero la ACERA no es suelo mientras haya calzada
        /// pisable. Playtest: "los NPC se suben a las veredas". La causa es
        /// geométrica: en una esquina la vereda se apoya ENCIMA de la pieza de
        /// calle, así que "la superficie pisable más alta" es el bordillo y el
        /// auto se sube solo. Con la calzada disponible se prefiere siempre;
        /// si de verdad solo hay acera bajo el auto (un tramo mal escaneado)
        /// se acepta, porque dejarlo sin suelo sería peor: se hundiría.
        /// </summary>
        public static bool TryPickCalzada(IReadOnlyList<float> surfaceYs, IReadOnlyList<bool> esAcera,
            float currentY, out float pickedY)
        {
            pickedY = currentY;
            if (esAcera == null || esAcera.Count != surfaceYs.Count)
                return TryPick(surfaceYs, currentY, out pickedY);

            bool found = false;
            float limit = currentY + MaxStepUp;
            for (int i = 0; i < surfaceYs.Count; i++)
            {
                float y = surfaceYs[i];
                if (y > limit || esAcera[i]) continue;
                if (!found || y > pickedY) { pickedY = y; found = true; }
            }
            if (found) return true;

            return TryPick(surfaceYs, currentY, out pickedY); // solo había acera
        }
    }
}
