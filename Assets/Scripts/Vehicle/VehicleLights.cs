using UnityEngine;

namespace HablaCamaron.Vehicle
{
    /// <summary>
    /// Faros REALES del auto: dos luces spot que alumbran la vía según el estado
    /// de luces del VehicleController (L: apagadas → cortas → largas).
    /// Indispensables para la misión nocturna de la Av. Simón Bolívar: las cortas
    /// alcanzan poco y apuntan al piso; las largas alumbran lejos y planas.
    /// La receta de cada haz está en BeamFor() (pura, testeada).
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleLights : MonoBehaviour
    {
        public struct Beam
        {
            public float Range;      // metros de alcance
            public float Intensity;
            public float PitchDown;  // grados de inclinación hacia el piso
        }

        /// <summary>Receta del haz por estado de luces (0 off, 1 cortas, 2 largas).</summary>
        public static Beam BeamFor(int lightsState) => lightsState switch
        {
            1 => new Beam { Range = 28f, Intensity = 2.6f, PitchDown = 10f },
            2 => new Beam { Range = 62f, Intensity = 4.2f, PitchDown = 2f },
            _ => new Beam { Range = 0f, Intensity = 0f, PitchDown = 0f },
        };

        private VehicleController _car;
        private Light _left, _right;

        private void Awake()
        {
            _car = GetComponent<VehicleController>();

            // Posición de los faros medida del propio modelo (frente del auto).
            var rends = GetComponentsInChildren<Renderer>();
            var b = new Bounds(transform.position, Vector3.one);
            if (rends.Length > 0)
            {
                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
            }
            Vector3 frontCenter = transform.InverseTransformPoint(
                new Vector3(b.center.x, b.min.y + b.size.y * 0.38f, b.max.z - 0.15f));
            float sideOff = b.size.x * 0.30f;

            _left = MakeHeadlight("Faro_Izq", frontCenter + Vector3.left * sideOff);
            _right = MakeHeadlight("Faro_Der", frontCenter + Vector3.right * sideOff);
        }

        private Light MakeHeadlight(string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.95f, 0.82f); // halógeno cálido (Aveo 2008)
            light.spotAngle = 70f;
            light.innerSpotAngle = 30f;
            light.shadows = LightShadows.None; // rendimiento: 2 luces sin sombras
            light.enabled = false;
            return light;
        }

        private void Update()
        {
            var beam = BeamFor(_car.LightsState);
            bool on = beam.Range > 0f;

            foreach (var l in new[] { _left, _right })
            {
                if (l == null) continue;
                l.enabled = on;
                if (!on) continue;
                l.range = beam.Range;
                l.intensity = beam.Intensity;
                l.transform.localRotation = Quaternion.Euler(beam.PitchDown, 0f, 0f);
            }
        }
    }
}
