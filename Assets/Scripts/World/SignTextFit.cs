using UnityEngine;

namespace HablaCamaron.World
{
    /// <summary>
    /// El sastre de las placas (arreglo de playtest: las letras se salían del
    /// cartel): dado cuánto mide el texto con characterSize = 1 y el espacio
    /// útil que deja la FORMA de la placa, devuelve el characterSize máximo
    /// que cabe. PURO (testeado en EditMode); EcuadorSignKit lo usa al
    /// construir cada señal midiendo el texto con la fuente real.
    /// </summary>
    public static class SignTextFit
    {
        /// <summary>Piso de legibilidad: por debajo de esto la placa necesita
        /// menos texto, no letras más chicas.</summary>
        public const float MinCharacterSize = 0.008f;

        /// <summary>Techo histórico para placas de una sola línea (arreglo de
        /// playtest 2026-07-14): agrandar este número corrompió el level del
        /// player tres veces ("level7 corrupted"). Legibilidad se gana con
        /// contraste (EcuadorSignKit), NUNCA subiendo esto.</summary>
        public const float CharacterSizeMaxSingleLine = 0.085f;

        /// <summary>Igual que arriba pero para placas de dos o más líneas
        /// ("ZONA\nESCOLAR", "CEDA\nEL PASO"...): menos alto útil por línea,
        /// por eso el techo es más chico.</summary>
        public const float CharacterSizeMaxMultiLine = 0.042f;

        /// <summary>
        /// Ancho útil (m) que la forma deja para el texto, dado el "radio" de
        /// la placa. Cada fracción sale del ancho REAL de la malla de
        /// EcuadorSignKit.ShapeMesh (círculo 2r, rombo 2.3r, rect 2.2r...)
        /// con margen para el borde.
        /// </summary>
        public static float UsableWidth(SignShape shape, float plateR)
        {
            switch (shape)
            {
                case SignShape.Octagono: return 2f * plateR * 0.72f;
                case SignShape.Triangulo: return 1.9f * plateR * 0.52f;
                case SignShape.Circulo: return 2f * plateR * 0.68f;
                case SignShape.Rombo: return 2.3f * plateR * 0.56f;
                default: return 2.2f * plateR * 0.88f; // rectángulo
            }
        }

        /// <summary>Alto útil (m) para el bloque completo de texto.</summary>
        public static float UsableHeight(SignShape shape, float plateR)
        {
            switch (shape)
            {
                case SignShape.Octagono: return 2f * plateR * 0.60f;
                case SignShape.Triangulo: return 1.6f * plateR * 0.30f; // la punta abajo recorta
                case SignShape.Circulo: return 2f * plateR * 0.56f;
                case SignShape.Rombo: return 2.3f * plateR * 0.50f;
                default: return 1.3f * plateR * 0.62f; // rectángulo
            }
        }

        /// <summary>
        /// El characterSize que hace CABER un texto medido (unidades de mundo
        /// por characterSize = 1): nunca crece sobre baseSize y manda el
        /// límite más restrictivo entre ancho y alto.
        /// </summary>
        public static float Fit(float baseSize, float widestLineUnits, float totalHeightUnits,
            float maxWidth, float maxHeight)
        {
            float size = baseSize;
            if (widestLineUnits > 1e-5f) size = Mathf.Min(size, maxWidth / widestLineUnits);
            if (totalHeightUnits > 1e-5f) size = Mathf.Min(size, maxHeight / totalHeightUnits);
            return Mathf.Max(size, MinCharacterSize);
        }
    }
}
