using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HablaCamaron.Vehicle;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Pruebas de la MECÁNICA CENTRAL del juego: el embrague.
    /// Arman un auto de prueba en el aire (sin gravedad, sin escena) y verifican:
    ///  - soltar el embrague de golpe sin acelerar → se cala,
    ///  - soltarlo con acelerador → arranque limpio,
    ///  - cambiar sin embrague → rechinido y la marcha no entra,
    ///  - encender en marcha sin embrague → bloqueado.
    /// Si alguien "mejora" la física y rompe la pedagogía, esto lo delata.
    /// </summary>
    public class VehicleControllerTests
    {
        private GameObject _carGO;
        private VehicleController _car;
        private VehicleSpec _spec;

        [SetUp]
        public void BuildTestCar()
        {
            _spec = ScriptableObject.CreateInstance<VehicleSpec>();

            _carGO = new GameObject("AutoDePrueba");
            var box = _carGO.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(1.8f, 1.0f, 4.2f);

            _car = _carGO.AddComponent<VehicleController>(); // crea el Rigidbody
            _car.Spec = _spec;

            var rb = _carGO.GetComponent<Rigidbody>();
            rb.useGravity = false; // el banco de pruebas flota: física determinista

            _car.WheelFL = MakeWheel(new Vector3(-0.8f, 0.35f, 1.4f));
            _car.WheelFR = MakeWheel(new Vector3(0.8f, 0.35f, 1.4f));
            _car.WheelRL = MakeWheel(new Vector3(-0.8f, 0.35f, -1.4f));
            _car.WheelRR = MakeWheel(new Vector3(0.8f, 0.35f, -1.4f));

            _car.HandbrakeOn = false;
        }

        [TearDown]
        public void Cleanup()
        {
            Object.Destroy(_carGO);
            Object.Destroy(_spec);
        }

        private WheelCollider MakeWheel(Vector3 localPos)
        {
            var go = new GameObject("WC");
            go.transform.SetParent(_carGO.transform, false);
            go.transform.localPosition = localPos;
            var wc = go.AddComponent<WheelCollider>();
            wc.radius = 0.35f;
            return wc;
        }

        // Ritual correcto hasta tener 1ª puesta y motor encendido.
        private void EngineOnInFirstGear()
        {
            _car.ClutchPedal = 1f;   // embrague pisado a fondo
            _car.ToggleIgnition();
            _car.ShiftTo(1);
            Assert.IsTrue(_car.EngineOn, "Precondición: el motor debía encender");
            Assert.AreEqual(1, _car.Gear, "Precondición: la 1ª debía entrar");
        }

        [UnityTest]
        public IEnumerator SoltarEmbragueDeGolpe_SinAcelerador_CalaElMotor()
        {
            EngineOnInFirstGear();
            bool stalledEvent = false;
            _car.OnStalled += () => stalledEvent = true;

            _car.ClutchPedal = 0f; // soltón brusco, el clásico del principiante
            _car.ThrottleInput = 0f;

            for (int i = 0; i < 120 && !_car.IsStalled; i++)
                yield return new WaitForFixedUpdate();

            Assert.IsTrue(_car.IsStalled, "Debía calarse al soltar sin acelerar");
            Assert.IsFalse(_car.EngineOn);
            Assert.IsTrue(stalledEvent, "El evento OnStalled debía dispararse (HUD/Don Pancho)");
        }

        [UnityTest]
        public IEnumerator SoltarEmbragueGradual_ConAcelerador_ArrancaLimpio()
        {
            EngineOnInFirstGear();
            _car.ThrottleInput = 0.7f;

            // Soltada progresiva (~1 segundo), como enseña Don Pancho.
            float pedal = 1f;
            while (pedal > 0f)
            {
                pedal -= Time.fixedDeltaTime * 1.1f;
                _car.ClutchPedal = Mathf.Max(0f, pedal);
                yield return new WaitForFixedUpdate();
            }
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();

            Assert.IsFalse(_car.IsStalled, "Con acelerador y soltada gradual NO debe calarse");
            Assert.IsTrue(_car.EngineOn);
            Assert.Greater(_car.Rpm, _spec.StallRpm);
        }

        [UnityTest]
        public IEnumerator CambiarSinEmbrague_Rechinia_YLaMarchaNoEntra()
        {
            _car.ClutchPedal = 0f; // pedal arriba
            bool grind = false;
            _car.OnGearGrind += () => grind = true;

            _car.ShiftTo(2);
            yield return null;

            Assert.IsTrue(grind, "Debía sonar el rechinido de caja");
            Assert.AreEqual(0, _car.Gear, "La marcha no debe entrar sin embrague");
        }

        [UnityTest]
        public IEnumerator EncenderEnMarcha_SinEmbrague_EstaBloqueado()
        {
            EngineOnInFirstGear();
            _car.ToggleIgnition(); // apagar dejando la 1ª puesta
            _car.ClutchPedal = 0f;

            bool blocked = false;
            _car.OnIgnitionBlocked += () => blocked = true;
            _car.ToggleIgnition();
            yield return null;

            Assert.IsTrue(blocked, "Debía avisar que pise el embrague");
            Assert.IsFalse(_car.EngineOn);
        }

        [UnityTest]
        public IEnumerator ReversaEnMovimiento_NoEntra()
        {
            EngineOnInFirstGear();
            // Simular que va a 20 km/h empujando el rigidbody.
            _carGO.GetComponent<Rigidbody>().linearVelocity = _carGO.transform.forward * 5.6f;
            yield return new WaitForFixedUpdate();

            _car.ClutchPedal = 1f;
            _car.ShiftTo(-1);

            Assert.AreNotEqual(-1, _car.Gear, "La reversa no debe entrar en movimiento");
        }

        [UnityTest]
        public IEnumerator Direccional_TogglearDosVeces_LaApaga()
        {
            _car.ToggleBlinker(-1);
            Assert.AreEqual(-1, _car.BlinkerState);
            _car.ToggleBlinker(-1);
            Assert.AreEqual(0, _car.BlinkerState);
            yield return null;
        }
    }
}
