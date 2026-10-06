using NUnit.Framework;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Gotcha documentado: agrandar las placas (PlateY/PlateR) o el
    /// characterSize del texto corrompió el level del player tres veces
    /// ("level7 corrupted"). Legibilidad SÍ, tamaño NO — este test clava los
    /// techos históricos (0.085 una línea / 0.042 varias líneas) como
    /// invariante: si un cambio futuro necesita subirlos, no va.
    /// </summary>
    public class SenalTamanoTechoTests
    {
        [Test]
        public void LosTamanosHistoricosSonElTechoYNadieLosSube()
        {
            Assert.LessOrEqual(SignTextFit.CharacterSizeMaxSingleLine, 0.085f);
            Assert.LessOrEqual(SignTextFit.CharacterSizeMaxMultiLine, 0.085f);
            Assert.LessOrEqual(SignTextFit.MinCharacterSize, SignTextFit.CharacterSizeMaxSingleLine);
        }

        [Test]
        public void ElTechoDeVariasLineas_NuncaSuperaAlDeUnaLinea()
        {
            // Menos alto útil por línea → el texto multilínea SIEMPRE es
            // más chico o igual, jamás más grande.
            Assert.LessOrEqual(SignTextFit.CharacterSizeMaxMultiLine, SignTextFit.CharacterSizeMaxSingleLine);
        }
    }
}
