using UnityEngine;
using UnityEngine.UI;

namespace HablaCamaron.Vehicle
{
    /// <summary>
    /// Cámaras del auto. Se agrega al GameObject del auto y construye todo por código
    /// (misma filosofía que la UI del proyecto):
    ///  - Vista 1ª persona desde el asiento del conductor (la vista del documento
    ///    de diseño), con leve cabeceo al acelerar/frenar y sacudida al calar.
    ///  - Espejo retrovisor central: cámara trasera → RenderTexture → panel arriba.
    ///  - Tecla C: alterna con una cámara exterior de seguimiento (útil para depurar
    ///    y para apreciar el modelo del Aveo).
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class DriverCamera : MonoBehaviour
    {
        [Header("Posición del ojo del conductor (local al auto)")]
        public Vector3 EyeLocalPos = new Vector3(-0.45f, 1.55f, 0.35f);

        [Header("Cámara exterior")]
        public Vector3 ExteriorOffset = new Vector3(0f, 2.8f, -7f);
        public float FollowLerp = 5f;

        private VehicleController _car;
        private Camera _cam;
        private Transform _camT;
        private bool _exterior;

        // Efectos de cabina.
        private float _pitchVel, _lastForwardSpeed, _shakeTime;

        // Retrovisor.
        private Camera _mirrorCam;
        private RenderTexture _mirrorRT;
        private GameObject _mirrorUI;

        private void Awake()
        {
            _car = GetComponent<VehicleController>();
            BuildMainCamera();
            BuildMirror();
        }

        private void BuildMainCamera()
        {
            // Si la escena trae una Main Camera suelta, se elimina: manda esta.
            var existing = Camera.main;
            if (existing != null && existing.transform.root != transform.root)
                Destroy(existing.gameObject);

            var go = new GameObject("DriverCamera");
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = EyeLocalPos;
            _cam = go.AddComponent<Camera>();
            _cam.fieldOfView = 58f;
            _cam.nearClipPlane = 0.15f;
            go.AddComponent<AudioListener>();
            _camT = go.transform;
        }

        private void BuildMirror()
        {
            // Cámara trasera montada detrás del techo, mirando hacia atrás.
            var go = new GameObject("MirrorCamera");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, EyeLocalPos.y + 0.25f, -1.8f);
            go.transform.localRotation = Quaternion.Euler(4f, 180f, 0f);

            _mirrorRT = new RenderTexture(512, 144, 16);
            _mirrorCam = go.AddComponent<Camera>();
            _mirrorCam.targetTexture = _mirrorRT;
            _mirrorCam.fieldOfView = 26f;
            _mirrorCam.nearClipPlane = 0.3f;

            // Panel del espejo: arriba al centro, como retrovisor real.
            var canvasGO = new GameObject("MirrorCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // debajo del HUD (10)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Marco oscuro + imagen de la cámara.
            var frame = new GameObject("MirrorFrame", typeof(RectTransform));
            frame.transform.SetParent(canvasGO.transform, false);
            var frameImg = frame.AddComponent<Image>();
            frameImg.color = new Color(0.10f, 0.07f, 0.05f, 0.95f);
            frameImg.raycastTarget = false;
            var frt = (RectTransform)frame.transform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 1f);
            frt.anchoredPosition = new Vector2(0f, -14f);
            frt.sizeDelta = new Vector2(424, 132);
            frt.pivot = new Vector2(0.5f, 1f);

            var img = new GameObject("MirrorImage", typeof(RectTransform));
            img.transform.SetParent(frame.transform, false);
            var raw = img.AddComponent<RawImage>();
            raw.texture = _mirrorRT;
            // Volteado horizontal: un espejo invierte izquierda/derecha.
            raw.uvRect = new Rect(1f, 0f, -1f, 1f);
            raw.raycastTarget = false;
            var irt = (RectTransform)img.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(6, 6); irt.offsetMax = new Vector2(-6, -6);

            _mirrorUI = canvasGO;
        }

        private void OnEnable()
        {
            var car = GetComponent<VehicleController>();
            car.OnStalled += StartShake;
        }

        private void OnDisable()
        {
            var car = GetComponent<VehicleController>();
            car.OnStalled -= StartShake;
        }

        private void OnDestroy()
        {
            // El auto se reconstruye en cada carga/reintento de misión: sin
            // liberar esto, la textura nativa del espejo se acumula durante
            // una sesión larga.
            if (_mirrorRT != null)
            {
                _mirrorRT.Release();
                Destroy(_mirrorRT);
            }
        }

        private void StartShake() => _shakeTime = 0.45f;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C)) SetExterior(!_exterior);
        }

        private void SetExterior(bool exterior)
        {
            _exterior = exterior;
            _mirrorUI.SetActive(!exterior);
            _mirrorCam.enabled = !exterior;
            if (!exterior)
            {
                _camT.SetParent(transform, false);
                _camT.localPosition = EyeLocalPos;
                _camT.localRotation = Quaternion.identity;
            }
            else
            {
                _camT.SetParent(null);
            }
        }

        private void LateUpdate()
        {
            if (_exterior) UpdateExterior();
            else UpdateFirstPerson();
        }

        private void UpdateFirstPerson()
        {
            // Cabeceo: la cabeza se va hacia atrás al acelerar y adelante al frenar.
            float accel = (_car.ForwardSpeed - _lastForwardSpeed) / Mathf.Max(Time.deltaTime, 0.001f);
            _lastForwardSpeed = _car.ForwardSpeed;
            float targetPitch = Mathf.Clamp(-accel * 0.35f, -3.5f, 3.5f);
            _pitchVel = Mathf.Lerp(_pitchVel, targetPitch, 4f * Time.deltaTime);

            // Mirada leve hacia donde se gira el volante.
            float yaw = _car.SteerInput * 4f;

            var rot = Quaternion.Euler(_pitchVel, yaw, 0f);

            // Sacudida al calar el motor (el "cabezazo" clásico del principiante).
            if (_shakeTime > 0f)
            {
                _shakeTime -= Time.deltaTime;
                float k = _shakeTime / 0.45f;
                rot *= Quaternion.Euler(
                    (Mathf.PerlinNoise(Time.time * 22f, 0f) - 0.5f) * 6f * k,
                    0f,
                    (Mathf.PerlinNoise(0f, Time.time * 22f) - 0.5f) * 4f * k);
            }

            _camT.localPosition = EyeLocalPos;
            _camT.localRotation = rot;
        }

        private void UpdateExterior()
        {
            Vector3 target = transform.TransformPoint(ExteriorOffset);
            _camT.position = Vector3.Lerp(_camT.position, target, FollowLerp * Time.deltaTime);
            _camT.LookAt(transform.position + Vector3.up * 1.4f);
        }
    }
}
