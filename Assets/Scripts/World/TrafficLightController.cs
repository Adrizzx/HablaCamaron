using System;
using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.World
{
    /// <summary>
    /// Controlador de los semáforos de la zona (uno por escena). Registra
    /// semáforos construidos por código (hijos Light_Green/Light_Yellow/
    /// Light_Red, ver TrafficLightKit) o postes Streetlight de Toon City
    /// (solo verde y rojo) y los prende/apaga según el ciclo. Los NPCs y el
    /// sistema de puntaje preguntan GetGroupState() para saber si pueden pasar.
    /// EN SU PROPIO ARCHIVO A PROPÓSITO (Tarea 9, 2026-07-24): compartía
    /// TrafficLights.cs con TrafficLightCycle y TrafficLampFaces, y Unity
    /// resolvía la CLASE PRINCIPAL de ese archivo como TrafficLightCycle (la
    /// primera declarada) — el MonoScript de TrafficLightController quedaba
    /// sin identidad de asset propia, así que NINGUNA escena podía guardar
    /// una referencia por GUID a este componente (siempre caía al respaldo
    /// por nombre de clase, que resuelve mal al cargar según la escena — se
    /// diagnosticó con `CityTrafficLightWiringTests`: N1_CiudadToon a veces
    /// resolvía, N2_QuitoCiudad nunca). Con el archivo separado (nombre =
    /// clase, la convención de Unity) el importador por fin identifica el
    /// asset correcto y las escenas regeneradas guardan la referencia real.
    /// </summary>
    public class TrafficLightController : MonoBehaviour
    {
        public static TrafficLightController Instance { get; private set; }

        [Header("Duraciones del ciclo (segundos)")]
        public float GreenDuration = 8f;
        public float YellowDuration = 2f;
        public float AllRedGap = 1f;

        // OJO: NO agregar campos a Lamp — cambiar el layout serializado de un
        // componente que vive en las escenas de zona reprodujo el crash
        // "level7 is corrupted" del player (2026-07-14). El foco amarillo del
        // semáforo de código se descubre por NOMBRE en runtime (abajo).
        [Serializable]
        public class Lamp
        {
            public int Group;
            public GameObject GreenLight;
            public GameObject RedLight;
        }

        private readonly Dictionary<Lamp, GameObject> _yellow = new Dictionary<Lamp, GameObject>();

        public List<Lamp> Lamps = new List<Lamp>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // EN RUNTIME (nada serializado): cada lámpara del poste gana un
            // FOCO esférico brillante (Unlit) — el estado se lee de lejos a
            // plena luz del día. Cuelga de la lámpara: SetActive lo apaga solo.
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            foreach (var lamp in Lamps)
            {
                // El foco amarillo (si el semáforo lo trae) se busca por nombre
                // entre los hermanos: así Lamp no cambia su layout serializado.
                var yellow = FindYellowSibling(lamp);
                _yellow[lamp] = yellow;
                float size = yellow != null ? 0.45f : 0.7f; // semáforo de código = focos finos

                AddBulb(lamp.GreenLight, new Color(0.15f, 0.95f, 0.25f), shader, size);
                AddBulb(yellow, new Color(1f, 0.78f, 0.12f), shader, size);
                AddBulb(lamp.RedLight, new Color(0.98f, 0.15f, 0.1f), shader, size);
            }
        }

        /// <summary>El "Light_Yellow" hermano de las lámparas registradas, si existe.</summary>
        private static GameObject FindYellowSibling(Lamp lamp)
        {
            var anchor = lamp.GreenLight != null ? lamp.GreenLight : lamp.RedLight;
            if (anchor == null || anchor.transform.parent == null) return null;
            foreach (Transform t in anchor.transform.parent)
                if (t.name.StartsWith("Light_Yellow")) return t.gameObject;
            return null;
        }

        private static void AddBulb(GameObject lampObj, Color c, Shader shader, float size)
        {
            if (lampObj == null) return;
            var ls = lampObj.transform.lossyScale;

            // Foco sólido (tamaño de MUNDO, sin importar la escala del poste).
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(bulb.GetComponent<Collider>());
            bulb.name = "Foco";
            bulb.transform.SetParent(lampObj.transform, false);
            bulb.transform.localPosition = Vector3.zero;
            bulb.transform.localScale = WorldScale(size, ls);
            if (shader != null)
                bulb.GetComponent<Renderer>().material = new Material(shader) { color = c };

            // Halo translúcido alrededor: el color "irradia" y se lee de lejos.
            var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(halo.GetComponent<Collider>());
            halo.name = "Halo";
            halo.transform.SetParent(lampObj.transform, false);
            halo.transform.localPosition = Vector3.zero;
            halo.transform.localScale = WorldScale(size * 1.8f, ls);
            if (shader != null)
            {
                var mat = new Material(shader) { color = new Color(c.r, c.g, c.b, 0.30f) };
                mat.SetFloat("_Surface", 1f); // transparente
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                halo.GetComponent<Renderer>().material = mat;
            }

            // Luz REAL de color: baña el cruce (de noche es imposible no verla).
            // Cuelga de la lámpara → SetActive la prende y apaga sola.
            var lightGO = new GameObject("Luz_Foco");
            lightGO.transform.SetParent(lampObj.transform, false);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = c;
            light.range = 9f;
            light.intensity = 2.6f;
            light.shadows = LightShadows.None;
        }

        /// <summary>Escala local que produce un tamaño MUNDIAL dado (el poste padre escala).</summary>
        private static Vector3 WorldScale(float worldSize, Vector3 parentLossy) => new Vector3(
            worldSize / Mathf.Max(parentLossy.x, 0.01f),
            worldSize / Mathf.Max(parentLossy.y, 0.01f),
            worldSize / Mathf.Max(parentLossy.z, 0.01f));

        /// <summary>Estado actual de un grupo (lo consultan NPCs y puntaje).</summary>
        public LightState GetGroupState(int group) =>
            TrafficLightCycle.GetState(group, Time.time, GreenDuration, YellowDuration, AllRedGap);

        /// <summary>
        /// Registra un poste semáforo de Toon City: busca sus lámparas hijas por
        /// nombre ("Light_Green...", "Light_Red...") y las asocia a un grupo.
        /// </summary>
        public void RegisterStreetlight(Transform streetlight, int group, float bulbSize = 0.7f)
        {
            var lamp = new Lamp { Group = group };
            foreach (var t in streetlight.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Light_Green")) lamp.GreenLight = t.gameObject;
                else if (t.name.StartsWith("Light_Red")) lamp.RedLight = t.gameObject;
            }
            Lamps.Add(lamp);
        }

        private void Update()
        {
            foreach (var lamp in Lamps)
            {
                var state = GetGroupState(lamp.Group);
                _yellow.TryGetValue(lamp, out var yellow);
                TrafficLampFaces.For(state, yellow != null,
                    out bool g, out bool y, out bool r);
                if (lamp.GreenLight != null) lamp.GreenLight.SetActive(g);
                if (yellow != null) yellow.SetActive(y);
                if (lamp.RedLight != null) lamp.RedLight.SetActive(r);
            }
        }
    }
}
