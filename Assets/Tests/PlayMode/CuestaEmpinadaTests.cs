using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HablaCamaron.Vehicle;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Las garantías de DISEÑO de la cuesta desafiante (playtest, nivel 3;
    /// ver CuestaDesafiante y AddMidHillLight en ZonaSurBuilder — estos
    /// ángulos y aquellos multiplicadores van de la mano):
    ///  1) En la rampa del SEMÁFORO (~14.5°) el arranque en pendiente parado,
    ///     con la técnica de Don Pancho, SÍ sale en 1ª.
    ///  2) En la "pared" final (~16°) la 3ª NO puede — eso obliga a cambiar.
    /// OJO: el techo real es la TRACCIÓN, no el torque (tracción delantera y
    /// el peso se va atrás cuesta arriba; a fondo las ruedas patinan — medido
    /// con la telemetría de estos tests). El banco replica las ruedas del
    /// Aveo REAL de ToonCityKit.BuildPlayerCar: radio 0.38, masa 22 y
    /// suspensión con targetPosition 0.5. Rampa real + gravedad encendida.
    /// </summary>
    public class CuestaEmpinadaTests
    {
        /// <summary>La rampa donde vive el semáforo de media cuesta.</summary>
        private const float RampaSemaforoDeg = 14.5f;
        /// <summary>La pared final (asin(2·1.12/8) ≈ 16°).</summary>
        private const float ParedDeg = 16f;

        private GameObject _rampa, _carGO;
        private VehicleController _car;
        private VehicleSpec _spec;

        private void BuildRampAndCar(float pendienteDeg)
        {
            // La rampa: un cubo enorme inclinado; la cara superior es la vía.
            // OJO: el banco se monta SIEMPRE en el origen. Separar cada test en
            // su propia zona del mundo parece más limpio, pero deja el collider
            // de la rampa sin sincronizar el primer paso de física y el auto
            // nace en el aire: cae, y la telemetría sale con z=0 y la velocidad
            // subiendo sin techo. La contaminación entre tests se resuelve en
            // Cleanup con DestroyImmediate, que es donde toca.
            _rampa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _rampa.name = "RampaDePrueba";
            _rampa.transform.rotation = Quaternion.Euler(-pendienteDeg, 0f, 0f);
            _rampa.transform.localScale = new Vector3(12f, 1f, 60f);

            _spec = ScriptableObject.CreateInstance<VehicleSpec>();

            _carGO = new GameObject("AveoDePrueba");
            var box = _carGO.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(1.8f, 1.0f, 4.2f);

            _car = _carGO.AddComponent<VehicleController>(); // crea el Rigidbody
            _car.Spec = _spec;

            _car.WheelFL = MakeWheel(new Vector3(-0.8f, 0.38f, 1.4f));
            _car.WheelFR = MakeWheel(new Vector3(0.8f, 0.38f, 1.4f));
            _car.WheelRL = MakeWheel(new Vector3(-0.8f, 0.38f, -1.4f));
            _car.WheelRR = MakeWheel(new Vector3(0.8f, 0.38f, -1.4f));

            // CLAVE con gravedad: Awake corrió ANTES de asignar el Spec, así
            // que el Rigidbody quedó con masa 1 kg. ApplySpec (la API del
            // garaje) re-equipa masa, centro de masa y suspensión reales.
            _car.ApplySpec(_spec);

            // El auto nace apoyado en la superficie, mirando cuesta arriba,
            // con freno de mano mientras asienta. Altura: las ruedas alcanzan
            // superficie hasta radio + suspensión (~0.6 m bajo su centro).
            Vector3 up = _rampa.transform.up;
            _carGO.transform.rotation = _rampa.transform.rotation;
            _carGO.transform.position = up * (0.5f + 0.12f);
            _car.HandbrakeOn = true;
            Physics.SyncTransforms(); // que el primer paso de física vea la rampa
        }

        [TearDown]
        public void Cleanup()
        {
            // DestroyImmediate, no Destroy: en PlayMode `Destroy` difiere la
            // baja al final del frame y el TearDown no cede ninguno, así que
            // el banco del test anterior seguía existiendo durante el siguiente.
            if (_rampa != null) Object.DestroyImmediate(_rampa);
            if (_carGO != null) Object.DestroyImmediate(_carGO);
            if (_spec != null) Object.DestroyImmediate(_spec);
        }

        /// <summary>Rueda como las del Aveo real (ToonCityKit.BuildPlayerCar).</summary>
        private WheelCollider MakeWheel(Vector3 localPos)
        {
            var go = new GameObject("WC");
            go.transform.SetParent(_carGO.transform, false);
            go.transform.localPosition = localPos;
            var wc = go.AddComponent<WheelCollider>();
            wc.radius = 0.38f;
            wc.mass = 22f;
            var spring = wc.suspensionSpring;
            spring.targetPosition = 0.5f;
            wc.suspensionSpring = spring;
            return wc;
        }

        /// <summary>
        /// Arranque en pendiente CON LA TÉCNICA que enseña Don Pancho:
        /// gas DOSIFICADO (a fondo la tracción delantera patina), embrague al
        /// punto de fricción con freno de mano puesto, y recién ahí soltar.
        /// </summary>
        private IEnumerator ArrancarEnPendiente(int gear, float gasArranque, float gasSubida)
        {
            for (int i = 0; i < 50; i++) yield return new WaitForFixedUpdate(); // asienta

            _car.ClutchPedal = 1f;
            _car.ToggleIgnition();
            _car.ShiftTo(gear);
            Assert.AreEqual(gear, _car.Gear, $"Precondición: la {gear}ª debía entrar");

            _car.ThrottleInput = gasArranque;

            float pedal = 1f;
            while (pedal > _spec.BitePoint)
            {
                pedal -= Time.fixedDeltaTime * 1.1f;
                _car.ClutchPedal = Mathf.Max(pedal, _spec.BitePoint);
                yield return new WaitForFixedUpdate();
            }
            for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate(); // muerde

            _car.HandbrakeOn = false;
            while (pedal > 0f)
            {
                pedal -= Time.fixedDeltaTime * 1.1f;
                _car.ClutchPedal = Mathf.Max(0f, pedal);
                yield return new WaitForFixedUpdate();
            }

            _car.ThrottleInput = gasSubida;
        }

        [UnityTest]
        public IEnumerator EnLaRampaDelSemaforo_ArranqueParado_EnPrimeraSube()
        {
            BuildRampAndCar(RampaSemaforoDeg);
            yield return ArrancarEnPendiente(1, gasArranque: 0.6f, gasSubida: 0.75f);
            float z0 = _carGO.transform.position.z;

            // Ventana de 600 pasos (12 s), no 300. La maniobra REAL incluye un
            // retroceso: al soltar el freno de mano el auto rueda ~0.7 m hacia
            // atrás antes de que el embrague muerda, así que en 300 pasos el
            // avance neto quedaba rozando los 2 m exigidos y el test cruzaba el
            // umbral en un sentido u otro según el ruido de la física (medido:
            // fallaba ~1 de cada 2 corridas con valores de 1.1 a 2.2 m).
            // El umbral NO se toca: se le da a la maniobra el tiempo que dura.
            for (int i = 0; i < 600; i++)
            {
                if (i % 60 == 0) // diagnóstico al log (se lee en tests-pm.log)
                    Debug.Log($"[CuestaDiag] f={i} z={_carGO.transform.position.z - z0:0.00} " +
                              $"rpm={_car.Rpm:0} vel={_car.SpeedKmh:0.0} gear={_car.Gear} " +
                              $"FLrpm={_car.WheelFL.rpm:0} FLtorque={_car.WheelFL.motorTorque:0}");
                yield return new WaitForFixedUpdate();
            }

            float avance = _carGO.transform.position.z - z0;
            Assert.IsFalse(_car.IsStalled, "Con la técnica bien hecha no debe calarse");
            Assert.Greater(avance, 2f,
                "El arranque en pendiente de la rampa del semáforo DEBE salir en 1ª");
        }

        [UnityTest]
        public IEnumerator EnLaParedFinal_LaTercera_NoPuede()
        {
            BuildRampAndCar(ParedDeg);
            yield return ArrancarEnPendiente(3, gasArranque: 0.6f, gasSubida: 1f);
            float z0 = _carGO.transform.position.z;

            for (int i = 0; i < 300; i++) yield return new WaitForFixedUpdate();

            float avance = _carGO.transform.position.z - z0;
            // En 3ª el motor no tiene fuerza en la pared: se cala o gatea —
            // eso es lo que OBLIGA a bajar de marcha (la pedagogía del nivel 3).
            Assert.Less(avance, 2f, "En 3ª la pared NO debería poder subirse");
        }
    }
}
