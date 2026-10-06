using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.UI;

namespace HablaCamaron.Tests.EditMode
{
    public class TextContainmentTests
    {
        [Test]
        public void RangoDeFuente_NuncaPermiteQueElMinimoSupereElMaximo()
        {
            var range = TextContainmentMath.FontRange(30, 18);

            Assert.AreEqual(18, range.min);
            Assert.AreEqual(18, range.max);
        }

        [Test]
        public void Circulo_SiempreReservaMargenInteriorSeguro()
        {
            Assert.AreEqual(3f, TextContainmentMath.CircleInset(10f));
            Assert.AreEqual(10.8f, TextContainmentMath.CircleInset(72f), 0.001f);
        }

        [Test]
        public void CirculoConTexto_ConfiguraAjusteYMargenEnLosCuatroLados()
        {
            var root = new GameObject("Raiz", typeof(RectTransform));
            try
            {
                var item = UIFactory.LabeledCircle("Acceso", root.transform, "7",
                    Color.black, Color.white, 72f, 30);

                Assert.AreEqual(new Vector2(72f, 72f), item.circle.rectTransform.sizeDelta);
                Assert.IsTrue(item.circle.preserveAspect);
                Assert.IsNotNull(item.circle.GetComponent<Mask>(),
                    "La máscara es la garantía final contra cualquier desborde del círculo");
                Assert.IsTrue(item.label.resizeTextForBestFit);
                // Con TOLERANCIA: Assert.AreEqual sobre Vector2 compara los
                // float de forma EXACTA, y 72 × 0.15 = 10.7999995 nunca es
                // idéntico al literal 10.8f. El margen es el correcto; lo
                // frágil era la comparación.
                Assert.AreEqual(10.8f, item.label.rectTransform.offsetMin.x, 1e-3f);
                Assert.AreEqual(10.8f, item.label.rectTransform.offsetMin.y, 1e-3f);
                Assert.AreEqual(-10.8f, item.label.rectTransform.offsetMax.x, 1e-3f);
                Assert.AreEqual(-10.8f, item.label.rectTransform.offsetMax.y, 1e-3f);
                Assert.AreEqual(HorizontalWrapMode.Wrap, item.label.horizontalOverflow);
                Assert.AreEqual(VerticalWrapMode.Truncate, item.label.verticalOverflow);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// La guía de arranque se salía de su placa (playtest, con foto): el
        /// texto iba anclado ESTIRADO y con sizeDelta (400, 30) tratado como
        /// si fuera el ancho. Con anclas estiradas sizeDelta se SUMA al del
        /// padre, así que el cuadro medía 830 px dentro de una placa de 430 y
        /// las frases largas ("Se caló. Tranquilo: pisa y mantén el embrague
        /// — shift") desbordaban por la derecha.
        /// Este test arma el panel de verdad y mide: ningún texto puede ser
        /// más ancho que su placa, y tiene que venir contenido (ajuste al
        /// hueco), no en modo derrame.
        /// </summary>
        [Test]
        public void GuiaDeArranque_NingunTextoEsMasAnchoQueSuPlaca()
        {
            var auto = new GameObject("Auto", typeof(Rigidbody),
                typeof(Vehicle.VehicleController));
            try
            {
                var tutor = auto.AddComponent<Vehicle.StartupTutor>();
                // Build() es privado y normalmente lo llama Start(), que en
                // EditMode no corre: se invoca a mano para poder medir la UI.
                typeof(Vehicle.StartupTutor)
                    .GetMethod("Build", System.Reflection.BindingFlags.NonPublic |
                                        System.Reflection.BindingFlags.Instance)
                    .Invoke(tutor, null);

                float anchoPlaca = Vehicle.StartupTutor.PanelSize.x;
                var textos = auto.GetComponentsInChildren<Text>(true);
                Assert.IsNotEmpty(textos, "el panel de la guía debe traer sus textos");

                // Se mide `rect.width`, NO `sizeDelta.x`: ahí está la trampa
                // que causó el bug. Con anclas estiradas sizeDelta es un
                // DELTA sobre el ancho del padre, así que el valor culpable
                // (400) parecía inofensivo frente a una placa de 430 mientras
                // el cuadro real medía 830. `rect.width` ya resuelve las
                // anclas, y es lo único que delata el desborde.
                foreach (var t in textos)
                    Assert.LessOrEqual(t.rectTransform.rect.width, anchoPlaca,
                        $"'{t.name}' mide {t.rectTransform.rect.width} px en una placa " +
                        $"de {anchoPlaca}: se sale por la derecha");

                var paso = System.Array.Find(textos, t => t.name == "Paso");
                Assert.IsNotNull(paso, "debe existir el texto de la instrucción");
                Assert.IsTrue(paso.resizeTextForBestFit,
                    "la instrucción tiene que encogerse para caber, no derramarse");
                Assert.AreEqual(HorizontalWrapMode.Wrap, paso.horizontalOverflow);
            }
            finally
            {
                Object.DestroyImmediate(auto);
            }
        }
    }
}
