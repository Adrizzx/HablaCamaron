using NUnit.Framework;
using UnityEngine;
using HablaCamaron.UI;

public class PortraitLibraryTests
{
    [Test]
    public void ElRecorteDeCaraCaeEnElTercioSuperiorYEsCuadrado()
    {
        // Las 2 ilustraciones son retratos de cuerpo: la cara vive arriba.
        foreach (var who in new[] { Character.DonPancho, Character.Mishel })
        {
            var r = PortraitLibrary.FaceRectFor(who, 1024, 1024);
            Assert.AreEqual(r.width, r.height, 1e-3f, $"{who}: cuadrado");
            Assert.Greater(r.yMin, 1024 * 0.45f, $"{who}: mitad superior");
            Assert.LessOrEqual(r.yMax, 1024, $"{who}: dentro de la textura");
        }
    }

    [Test]
    public void SinSpriteImportadoNoRevienta()
    {
        // En tests no hay Resources de retratos garantizados: TryFace
        // devuelve false y la UI cae al retrato dibujado por código.
        Assert.DoesNotThrow(() => PortraitLibrary.TryFace(Character.DonPancho, out _));
    }
}
