using NUnit.Framework;
using HablaCamaron.Vehicle;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La guía de arranque deriva su instrucción del estado del auto.
    /// Estas pruebas fijan ese "árbol de decisión" pedagógico: si alguien lo
    /// reordena y la guía empieza a dar consejos incorrectos, esto lo detecta.
    /// Firma: GetInstruction(engineOn, clutchPedal, gear, handbrakeOn, speedKmh, throttle)
    /// </summary>
    public class StartupTutorTests
    {
        [Test]
        public void MotorApagado_EnMarchaSinEmbrague_PidePisarEmbrague()
        {
            string msg = StartupTutor.GetInstruction(false, 0f, 1, true, 0f, 0f);
            StringAssert.Contains("embrague", msg.ToLower());
        }

        [Test]
        public void MotorApagado_ConEmbraguePisado_PideEncender()
        {
            string msg = StartupTutor.GetInstruction(false, 1f, 1, true, 0f, 0f);
            StringAssert.Contains("F", msg);
            StringAssert.Contains("nciende", msg);
        }

        [Test]
        public void MotorApagado_EnNeutro_PideEncenderDirecto()
        {
            // En N no hace falta embrague para encender.
            string msg = StartupTutor.GetInstruction(false, 0f, 0, true, 0f, 0f);
            StringAssert.Contains("nciende", msg);
        }

        [Test]
        public void Encendido_EnNeutroConEmbrague_PidePrimera()
        {
            string msg = StartupTutor.GetInstruction(true, 1f, 0, true, 0f, 0f);
            StringAssert.Contains("PRIMERA", msg);
        }

        [Test]
        public void Encendido_EnPrimera_ConFrenoDeMano_PideSoltarlo()
        {
            string msg = StartupTutor.GetInstruction(true, 1f, 1, true, 0f, 0f);
            StringAssert.Contains("freno de mano", msg.ToLower());
        }

        [Test]
        public void ListoParaSalir_SinAcelerador_PideAcelerar()
        {
            string msg = StartupTutor.GetInstruction(true, 1f, 1, false, 0f, 0f);
            StringAssert.Contains("W", msg);
        }

        [Test]
        public void Acelerando_ConEmbraguePisado_PideSoltarDespacio()
        {
            string msg = StartupTutor.GetInstruction(true, 0.9f, 1, false, 0f, 0.5f);
            StringAssert.Contains("DESPACIO", msg);
        }

        [Test]
        public void YaManejando_NoHayInstruccion()
        {
            string msg = StartupTutor.GetInstruction(true, 0f, 2, false, 25f, 0.6f);
            Assert.IsNull(msg, "Manejando no debe molestar con instrucciones");
        }
    }
}
