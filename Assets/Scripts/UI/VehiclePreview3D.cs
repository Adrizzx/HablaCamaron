using UnityEngine;
using UnityEngine.UI;

namespace HablaCamaron.UI
{
    /// <summary>
    /// Vitrina 3D autocontenida para el garaje. Renderiza solamente el prefab
    /// indicado a una textura de uGUI, sin contaminar la escena con cámaras o
    /// luces visibles para el resto del juego.
    /// </summary>
    public sealed class VehiclePreview3D : MonoBehaviour
    {
        private const float TurnSpeed = 10f;

        private GameObject _pivot;
        private Transform _turntable;
        private RenderTexture _texture;
        private Camera _camera;

        public void Initialize(GameObject prefab, bool unlocked, int previewLayer)
        {
            var image = GetComponent<RawImage>();
            if (image == null || prefab == null) return;

            previewLayer = Mathf.Clamp(previewLayer, 0, 31);
            _texture = new RenderTexture(768, 320, 24, RenderTextureFormat.ARGB32)
            {
                name = "Garaje_" + prefab.name,
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear
            };
            _texture.Create();
            image.texture = _texture;
            image.color = unlocked ? Color.white : new Color(0.48f, 0.50f, 0.54f, 1f);
            image.raycastTarget = false;

            _pivot = new GameObject("Vitrina3D_" + prefab.name);
            _pivot.transform.position = new Vector3(previewLayer * 1000f, -1000f, 0f);

            _turntable = new GameObject("PlataformaGiratoria").transform;
            _turntable.SetParent(_pivot.transform, false);

            var model = Instantiate(prefab, _turntable);
            model.name = prefab.name + "_Preview";
            RemovePhysics(model);
            SetLayerRecursively(model, previewLayer);

            // Centrar el modelo sobre la plataforma para que gire sobre sí mismo.
            var initial = RendererBounds(model);
            Vector3 centerLocal = _pivot.transform.InverseTransformPoint(initial.center);
            float floorLocal = _pivot.transform.InverseTransformPoint(
                new Vector3(initial.center.x, initial.min.y, initial.center.z)).y;
            model.transform.localPosition -= new Vector3(centerLocal.x, floorLocal, centerLocal.z);
            var bounds = RendererBounds(model);

            BuildCamera(bounds, previewLayer);
            BuildLights(bounds, previewLayer);
        }

        private void Update()
        {
            if (_turntable != null)
                _turntable.Rotate(Vector3.up, TurnSpeed * Time.unscaledDeltaTime, Space.Self);
        }

        private void BuildCamera(Bounds bounds, int layer)
        {
            var go = new GameObject("CamaraVitrina");
            go.transform.SetParent(_pivot.transform, false);
            _camera = go.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            // Transparente para conservar el halo y la plataforma dibujados por uGUI.
            _camera.backgroundColor = UITheme.A(UITheme.GroundDeep, 0f);
            _camera.cullingMask = 1 << layer;
            _camera.targetTexture = _texture;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.allowHDR = false;

            Vector3 direction = new Vector3(1f, 0.48f, 1.25f).normalized;
            float distance = Mathf.Max(12f, bounds.size.magnitude * 2f);
            go.transform.position = bounds.center + direction * distance;
            go.transform.LookAt(bounds.center + Vector3.up * bounds.extents.y * 0.04f);

            var corners = BoundsCorners(bounds);
            float halfWidth = 0f;
            float halfHeight = 0f;
            foreach (var corner in corners)
            {
                Vector3 local = go.transform.InverseTransformPoint(corner);
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(local.x));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y));
            }
            _camera.orthographicSize = VehiclePreviewMath.OrthographicSize(
                halfWidth, halfHeight, (float)_texture.width / _texture.height, 1.14f);
        }

        private void BuildLights(Bounds bounds, int layer)
        {
            float reach = Mathf.Max(8f, bounds.size.magnitude * 2.5f);
            AddLight("ReflectorPrincipal", bounds.center + new Vector3(4f, 6f, 3f),
                new Color(1f, 0.80f, 0.58f), 2.2f, reach, layer);
            AddLight("RellenoDorado", bounds.center + new Vector3(-4f, 3f, -2f),
                new Color(0.58f, 0.72f, 1f), 1.35f, reach, layer);
        }

        private void AddLight(string lightName, Vector3 position, Color color,
            float intensity, float range, int layer)
        {
            var go = new GameObject(lightName);
            go.layer = layer;
            go.transform.SetParent(_pivot.transform, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            var result = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
            return result;
        }

        private static Vector3[] BoundsCorners(Bounds b)
        {
            var corners = new Vector3[8];
            int index = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        corners[index++] = b.center + Vector3.Scale(b.extents, new Vector3(x, y, z));
            return corners;
        }

        private static void RemovePhysics(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                Destroy(collider);
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                Destroy(body);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private void OnDestroy()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
            if (_pivot != null) Destroy(_pivot);
        }
    }

    /// <summary>Fórmula pura de encuadre de la vitrina, probada en EditMode.</summary>
    public static class VehiclePreviewMath
    {
        public static float OrthographicSize(float halfWidth, float halfHeight,
            float aspect, float padding = 1.1f)
        {
            aspect = Mathf.Max(0.01f, aspect);
            padding = Mathf.Max(1f, padding);
            return Mathf.Max(0.01f, Mathf.Max(halfHeight, halfWidth / aspect) * padding);
        }
    }
}
