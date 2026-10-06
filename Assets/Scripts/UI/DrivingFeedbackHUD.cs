using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.Vehicle;

namespace HablaCamaron.UI
{
    /// <summary>
    /// Feedback visual de conducción (complementa al HUDController):
    ///  - Cluster de PEDALES en vivo (izquierda-abajo): barras de embrague, freno y
    ///    acelerador. La de embrague marca el punto de fricción y se pone roja
    ///    cuando el motor está por calarse → el jugador VE lo que hacen sus pies.
    ///  - Toasts de acción (arriba-centro): confirmación breve de cada acción
    ///    (marcha, luces, freno de mano, direccional...).
    ///  - Panel de AYUDA con todos los controles (tecla H).
    /// Lo crea VehicleHUDBridge automáticamente; no hay que armar nada.
    /// </summary>
    public class DrivingFeedbackHUD : MonoBehaviour
    {
        [HideInInspector] public VehicleController Car;

        private RectTransform _clutchFill, _brakeFill, _throttleFill;
        private Image _clutchImg;
        private Text _toastText;
        private CanvasGroup _toastGroup;
        private GameObject _helpPanel;
        private Coroutine _toastRoutine;

        private void Start() => Build();

        private void Build()
        {
            var canvasGO = new GameObject("FeedbackCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11; // sobre el HUD base (10)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            var root = canvas.transform;

            BuildPedalCluster(root);
            BuildToast(root);
            BuildHelpPanel(root);
        }

        // ---------- Cluster de pedales (inferior izquierda, sobre los chips) ----------

        private void BuildPedalCluster(Transform root)
        {
            var panel = UIFactory.Panel("Pedales", root, UITheme.A(UITheme.PanelDark, 0.85f),
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, UITheme.RadiusMd);
            UIFactory.SetRect(panel.gameObject, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(26, 128), new Vector2(172, 158), new Vector2(0, 0));
            UIFactory.AddGlowEdge(panel.gameObject, UITheme.A(UITheme.Gold, 0.35f), 1.5f);

            float bitePoint = Car != null && Car.Spec != null ? Car.Spec.BitePoint : 0.45f;
            _clutchFill = BuildBar(panel, 16f, "EMB", UITheme.Gold, out _clutchImg, bitePoint);
            _brakeFill = BuildBar(panel, 66f, "FRE", UITheme.Danger, out _, -1f);
            _throttleFill = BuildBar(panel, 116f, "ACE", UITheme.SuccessLight, out _, -1f);
        }

        private RectTransform BuildBar(RectTransform parent, float x, string label,
            Color color, out Image fillImg, float marker01)
        {
            // Fondo de la barra.
            var bg = UIFactory.Panel("Bar_" + label, parent, UITheme.A(UITheme.PanelWarm, 0.45f),
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 6f);
            UIFactory.SetRect(bg.gameObject, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(x, 36), new Vector2(30, 100), new Vector2(0, 0));

            // Relleno (crece desde abajo con anclas).
            var fill = UIFactory.Panel("Fill", bg, color,
                Vector2.zero, new Vector2(1, 0), Vector2.zero, Vector2.zero, 6f);
            fillImg = fill.GetComponent<Image>();

            // Línea del punto de fricción (solo el embrague la lleva).
            if (marker01 >= 0f)
            {
                var mark = UIFactory.Panel("Bite", bg, UITheme.GoldLight,
                    new Vector2(0, marker01), new Vector2(1, marker01),
                    new Vector2(-3, -1), new Vector2(3, 1));
                mark.gameObject.name = "PuntoFriccion";
            }

            var txt = UIFactory.Label("Lbl_" + label, parent, label, 13,
                UITheme.TextMuted, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetRect(txt.gameObject, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(x + 15, 18), new Vector2(40, 18));

            return fill;
        }

        private static void SetFill(RectTransform fill, float v)
        {
            fill.anchorMax = new Vector2(1f, Mathf.Clamp01(v));
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (Car == null) return;

            SetFill(_clutchFill, Car.ClutchPedal);
            SetFill(_brakeFill, Car.BrakeInput);
            SetFill(_throttleFill, Car.ThrottleInput);

            // Barra de embrague en ROJO si el motor está a punto de calarse:
            // acoplado, mordiendo y con RPM peligrosamente bajas.
            bool stallDanger = Car.EngineOn && Car.Gear != 0 &&
                               Car.ClutchEngagement > 0.05f &&
                               Car.Rpm < Car.Spec.StallRpm * 1.5f;
            _clutchImg.color = stallDanger ? UITheme.Danger : UITheme.Gold;

            if (Input.GetKeyDown(KeyCode.H) && _helpPanel != null)
                _helpPanel.SetActive(!_helpPanel.activeSelf);
        }

        // ---------- Toast de acción (confirmación breve, arriba-centro) ----------

        private void BuildToast(Transform root)
        {
            var pill = UIFactory.Panel("Toast", root, UITheme.A(UITheme.PanelDark, 0.9f),
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, UITheme.RadiusMd);
            UIFactory.SetRect(pill.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, HUDLayout.ToastY), new Vector2(340, 44), new Vector2(0.5f, 1f));

            _toastText = UIFactory.Label("ToastText", pill, "", 20,
                UITheme.GoldLight, TextAnchor.MiddleCenter, FontStyle.Bold);
            var trt = _toastText.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            _toastGroup = pill.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
        }

        /// <summary>Confirmación breve de una acción ("Marcha: 3", "Luces: cortas"...).</summary>
        public void Toast(string msg)
        {
            if (_toastText == null) return;
            _toastText.text = msg;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            _toastGroup.alpha = 1f;
            yield return new WaitForSeconds(1.3f);
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                _toastGroup.alpha = 1f - t / 0.35f;
                yield return null;
            }
            _toastGroup.alpha = 0f;
        }

        // ---------- Panel de ayuda (tecla H) ----------

        private void BuildHelpPanel(Transform root)
        {
            var panel = UIFactory.Panel("Ayuda", root, UITheme.A(UITheme.PanelDark, 0.96f),
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, UITheme.RadiusLg);
            UIFactory.SetRect(panel.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(560, 520));
            UIFactory.AddGlowEdge(panel.gameObject, UITheme.A(UITheme.Gold, 0.5f), 2f);

            var title = UIFactory.Label("Titulo", panel, "CONTROLES DEL AVEO", 26,
                UITheme.Gold, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            UIFactory.SetRect(title.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -36), new Vector2(500, 36));

            (string key, string action)[] rows =
            {
                ("Shift",   "Embrague — mantener pisado, soltar DESPACIO"),
                ("W / S",   "Acelerador / Freno"),
                ("A / D",   "Volante"),
                ("1 - 5",   "Marchas (con embrague pisado)"),
                ("N / R",   "Neutro / Reversa (R otra vez: sale a neutro)"),
                ("F",       "Encender / apagar el motor"),
                ("Espacio", "Freno de mano (clave en las cuestas)"),
                ("Q / E",   "Direccional izquierda / derecha"),
                ("L",       "Luces: apagadas → cortas → largas"),
                ("B",       "Bocina (en Quito, el pito es lenguaje)"),
                ("C",       "Cámara: volante ↔ exterior"),
                ("Esc",     "Pausa"),
            };

            float y = -84f;
            foreach (var (key, action) in rows)
            {
                var pill = UIFactory.Pill("Key_" + key, panel, key,
                    UITheme.A(UITheme.PanelWarm, 0.9f), UITheme.GoldLight, 17);
                UIFactory.SetRect(pill.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(78, y), new Vector2(96, 30));

                var txt = UIFactory.Label("Act", panel, action, 17,
                    UITheme.TextSand, TextAnchor.MiddleLeft);
                UIFactory.SetRect(txt.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(330, y), new Vector2(380, 30));

                y -= 33f;
            }

            var hint = UIFactory.Label("Cerrar", panel, "H para cerrar", 15,
                UITheme.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic);
            UIFactory.SetRect(hint.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 24), new Vector2(200, 24));

            _helpPanel = panel.gameObject;
            _helpPanel.SetActive(false);
        }
    }
}
