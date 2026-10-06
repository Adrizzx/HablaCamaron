using UnityEngine;

namespace HablaCamaron.AI
{
    /// <summary>
    /// Reglas PURAS de aparición y retirada de NPCs (testeadas en EditMode).
    /// El playtest reportaba "hay NPCs que aparecen de la nada": medido con
    /// TraficoBoletinTests, en la Zona Sur el 100% de los nacimientos caía
    /// DENTRO del frustum de la cámara del jugador (6/6, 7/7, 8/8, 9/9 en
    /// cuatro corridas) — el spawn solo miraba DISTANCIA, nunca si el jugador
    /// estaba mirando hacia allá. Un auto materializándose a 60 m de frente es
    /// exactamente lo que rompe la ilusión de ciudad.
    /// La retirada tiene el problema simétrico: destruir un NPC que el jugador
    /// está viendo es una desaparición igual de fea.
    /// </summary>
    public static class NpcSpawnRules
    {
        /// <summary>
        /// Margen (m) que se le suma a la caja del auto al probar el frustum:
        /// nacer JUSTO en el borde de la vista es casi tan malo como nacer en
        /// el centro, porque un giro mínimo de cabeza lo descubre apareciendo.
        /// </summary>
        public const float MargenFrustum = 6f;

        /// <summary>Caja aproximada de un auto, para probar visibilidad ANTES
        /// de instanciar nada (no hay Renderer todavía que medir).</summary>
        public static readonly Vector3 CajaAuto = new Vector3(2.6f, 2f, 5.2f);

        /// <summary>
        /// Un NPC lejísimos se retira aunque se vea: sin este tope, un jugador
        /// parado mirando una avenida recta acumularía NPCs sin límite (fuga de
        /// memoria y de CPU disfrazada de "no desaparecer a la vista").
        /// </summary>
        public const float FactorDespawnDuro = 1.6f;

        /// <summary>¿La distancia al jugador cae en el anillo de aparición?</summary>
        public static bool DistanciaValida(float dist, float radioMin, float radioMax)
            => dist >= radioMin && dist <= radioMax;

        /// <summary>
        /// ¿Una caja en <paramref name="centro"/> entra en el frustum? Sin
        /// planos (escena sin cámara todavía) se responde NO VISIBLE: es lo
        /// seguro — si no se sabe qué ve el jugador, no se bloquea el tráfico
        /// entero, que dejaría la ciudad vacía.
        /// </summary>
        public static bool EnFrustum(Plane[] planos, Vector3 centro)
        {
            if (planos == null || planos.Length == 0) return false;
            var caja = new Bounds(centro, CajaAuto + Vector3.one * (MargenFrustum * 2f));
            return GeometryUtility.TestPlanesAABB(planos, caja);
        }

        /// <summary>
        /// Piso de distancia cuando el punto NO se ve. El radio mínimo de
        /// siempre (45 m) era en realidad un PROXY de "que no lo vean nacer":
        /// se elegía grande porque no había forma de saber hacia dónde miraba
        /// el jugador. Comprobada la visibilidad DE VERDAD, el proxy sobra y
        /// estorba: medido con TraficoBoletinTests, en la Zona Sur el 100% de
        /// los nodos del anillo 45-110 m caía dentro del frustum, así que
        /// exigir las dos cosas a la vez dejó la zona con CERO tráfico
        /// ambiental (0 spawns en 4 corridas). Un auto que nace 25 m detrás del
        /// jugador es invisible y legítimo; el piso solo evita que aparezca
        /// literalmente encima.
        /// </summary>
        public const float RadioMinFueraDeVista = 25f;

        /// <summary>
        /// ¿El jugador VERÍA aparecer algo ahí? No basta con el frustum: un
        /// auto que nace detrás de un edificio no "aparece de la nada" aunque
        /// geométricamente caiga dentro del cono de la cámara.
        /// La distinción no es cosmética, es lo que hace jugable la Zona Sur:
        /// medido con SpawnVisibilidadDiagTests, de sus 15 nodos en rango
        /// **ninguno** queda fuera del frustum (el jugador arranca en el garaje
        /// mirando la única calle y el grafo entero le queda de frente), pero
        /// 4 sí están tapados. Con el criterio de solo-frustum la zona se
        /// quedaba con CERO tráfico ambiental.
        /// </summary>
        public static bool SeLeVeria(bool enFrustum, bool hayLineaDeVista) => enFrustum && hayLineaDeVista;

        /// <summary>
        /// Alturas (m sobre la calzada) a las que hay que probar la línea de
        /// vista. Con un solo rayo a 0.9 m la regla se equivocaba de forma
        /// sistemática: un auto de Toon City mide ~3 m, así que un muro bajo
        /// tapa la línea al nivel del capó y deja el TECHO a la vista. Medido:
        /// dos corridas distintas colaron un spawn visible en el MISMO punto
        /// (657.8, 1.65, 130.0) a 92 m — la regla lo daba por escondido y el
        /// jugador le veía la parte de arriba. Si CUALQUIERA de estas alturas
        /// tiene línea libre, se le ve.
        /// </summary>
        public static readonly float[] AlturasDeSilueta = { 0.5f, 1.7f, 2.9f };

        /// <summary>
        /// ¿Puede nacer aquí? Que no se le vea aparecer, y a una distancia
        /// razonable. Al no vérsele, el mínimo se relaja hasta
        /// RadioMinFueraDeVista (ver arriba: el mínimo grande era un sustituto
        /// de esta misma comprobación).
        /// </summary>
        public static bool PuedeNacer(float dist, float radioMin, float radioMax, bool seLeVeria)
        {
            if (seLeVeria) return false;
            return DistanciaValida(dist, Mathf.Min(radioMin, RadioMinFueraDeVista), radioMax);
        }

        /// <summary>
        /// ¿Toca retirarlo? Pasado el radio, salvo que el jugador lo esté
        /// viendo; y sin salvedad que valga pasado el tope duro.
        /// </summary>
        public static bool DebeRetirarse(float dist, float radioDespawn, bool seLeVeria)
        {
            if (dist > radioDespawn * FactorDespawnDuro) return true;
            return dist > radioDespawn && !seLeVeria;
        }
    }
}
