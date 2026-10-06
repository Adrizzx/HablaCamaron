using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HablaCamaron.Vehicle;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El garaje de la Fase 4 re-equipa el auto de la escena EN CALIENTE
    /// (las zonas se generan con el Aveo; si el jugador eligió la BT-50,
    /// ApplySpec le cambia masa, centro de masa y suspensión de verdad).
    /// </summary>
    public class VehicleSwapTests
    {
        private GameObject _carGO;
        private VehicleController _car;
        private VehicleSpec _aveo;

        [SetUp]
        public void BuildTestCar()
        {
            _aveo = ScriptableObject.CreateInstance<VehicleSpec>();

            _carGO = new GameObject("AutoDePrueba");
            _car = _carGO.AddComponent<VehicleController>(); // crea el Rigidbody
            _car.Spec = _aveo;
            _carGO.GetComponent<Rigidbody>().useGravity = false;

            _car.WheelFL = MakeWheel(new Vector3(-0.8f, 0.35f, 1.4f));
            _car.WheelFR = MakeWheel(new Vector3(0.8f, 0.35f, 1.4f));
            _car.WheelRL = MakeWheel(new Vector3(-0.8f, 0.35f, -1.4f));
            _car.WheelRR = MakeWheel(new Vector3(0.8f, 0.35f, -1.4f));
        }

        [TearDown]
        public void Cleanup()
        {
            Object.Destroy(_carGO);
            Object.Destroy(_aveo);
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

        [UnityTest]
        public IEnumerator ApplySpec_ConLaBt50_CambiaMasaYSuspensionDeVerdad()
        {
            yield return null; // que corra el Awake del controlador

            var bt50 = VehicleRoster.CreateSpec(VehicleRoster.Bt50Id);
            _car.ApplySpec(bt50);

            var rb = _carGO.GetComponent<Rigidbody>();
            Assert.AreEqual(bt50.Mass, rb.mass, 0.01f, "la masa de camioneta debe aplicarse");
            Assert.AreEqual(bt50.CenterOfMass, rb.centerOfMass);
            Assert.AreEqual(bt50.SpringForce, _car.WheelFL.suspensionSpring.spring, 0.01f,
                "la suspensión debe re-templarse para el peso");
            Assert.AreSame(bt50, _car.Spec);

            Object.Destroy(bt50);
        }

        [UnityTest]
        public IEnumerator ApplySpec_Nulo_NoRompeNada()
        {
            yield return null;
            float massBefore = _carGO.GetComponent<Rigidbody>().mass;
            Assert.DoesNotThrow(() => _car.ApplySpec(null));
            Assert.AreSame(_aveo, _car.Spec, "un spec nulo se ignora");
            Assert.AreEqual(massBefore, _carGO.GetComponent<Rigidbody>().mass);
        }
    }
}
