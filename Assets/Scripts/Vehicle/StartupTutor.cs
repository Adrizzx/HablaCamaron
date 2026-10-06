using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.UI;

namespace HablaCamaron.Vehicle
{
    /// <summary>
    /// Guía paso a paso del ritual de arranque (panel superior izquierdo).
    /// Deriva la instrucción actual del ESTADO del auto (no de un guion rígido),
    /// así que si el jugador se salta pasos o se cala, la guía siempre dice lo
    /// correcto. Desaparece cuando ya está manejando y reaparece si se cala.
    /// La lógica está en GetInstruction() (estática y pura) para poder testearla.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class StartupTutor : MonoBehaviour
    {
        /// <summary>Tamaño de la placa (px de referencia 1920×1080). Lo usan
        /// también los textos de dentro para calcular su ancho útil: si la
        /// placa cambia de ancho, el texto sigue cabiendo solo.</summary>
        public static readonly Vector2 PanelSize = new Vector2(430f, 84f);

        private VehicleController _car;
        private GameObject _panel;
        private Text _stepText;
        private float _doneTimer = -1f;

        private void Awake() => _car = GetComponent<VehicleController>();

        private void Start() => Build();

        /// <summary>
        /// Qué debe hacer el jugador AHORA, según el estado del auto.
        /// Devuelve null cuando ya está manejando (no hay nada que enseñar).
        /// </summary>
        public static string GetInstruction(bool engineOn, float clutchPedal, int gear,
            bool handbrakeOn, float speedKmh, float throttle)
        {
            if (!engineOn)
            {
                if (gear != 0 && clutchPedal < 0.7f)
                    return "Pisa y MANTÉN el embrague — Shift";
                return "Enciende el motor — F";
            }
            if (gear == 0)
            {
                if (clutchPedal < 0.7f)
                    return "Pisa el embrague (Shift) para meter la marcha";
                return "Mete PRIMERA — tecla 1";
            }
            if (handbrakeOn)
                return "Suelta el freno de mano — Espacio";
            if (speedKmh < 3f)
            {
                if (throttle < 0.15f)
                    return "Acelera un poco — W";
                if (clutchPedal > 0.10f)
                    return "Suelta el embrague DESPACIO...";
            }
            return null; // ya está manejando
        }

        private void Build()
        {
            var canvasGO = new GameObject("TutorCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = UIFactory.Panel("Tutor", canvas.transform, UITheme.A(UITheme.PanelDark, 0.88f),
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, UITheme.RadiusMd);
            UIFactory.SetRect(panel.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(26, -26), PanelSize, new Vector2(0, 1));
            UIFactory.AddGlowEdge(panel.gameObject, UITheme.A(UITheme.Gold, 0.45f), 1.5f);

            // OJO con las anclas: la versión anterior anclaba este texto
            // ESTIRADO (anchorMin.x=0, anchorMax.x=1) y le pasaba sizeDelta
            // (400, 30) como si fuera el ancho. Con anclas estiradas sizeDelta
            // NO es el tamaño, es cuánto se SUMA al del padre: el cuadro de
            // texto medía 430+400 = 830 px dentro de un panel de 430, y como
            // UIFactory.Label deja `HorizontalWrapMode.Overflow`, las frases
            // largas ("Se caló. Tranquilo: pisa y mantén el embrague — shift")
            // se salían de la placa por la derecha (reportado con foto).
            // Ahora todo va anclado al borde SUPERIOR IZQUIERDO del panel, con
            // tamaño absoluto y su margen — y el texto se contiene de verdad.
            const float margen = 14f;
            float anchoUtil = PanelSize.x - margen * 2f;

            var title = UIFactory.Label("Titulo", panel, "DON PANCHO TE GUÍA", 14,
                UITheme.Gold, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            UIFactory.SetRect(title.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(margen, -8f), new Vector2(anchoUtil, 18f), new Vector2(0, 1));

            _stepText = UIFactory.Label("Paso", panel, "", 20,
                UITheme.TextCream, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.SetRect(_stepText.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(margen, -30f), new Vector2(anchoUtil, 46f), new Vector2(0, 1));
            // Que la frase se ajuste al hueco (envuelve a 2 líneas y encoge
            // hasta 13 si hace falta) en vez de derramarse fuera de la placa.
            UIFactory.FitTextInside(_stepText, 20, 13);

            _panel = panel.gameObject;
        }

        private void Update()
        {
            if (_panel == null || _car == null) return;

            string instruction = GetInstruction(_car.EngineOn, _car.ClutchPedal, _car.Gear,
                _car.HandbrakeOn, _car.SpeedKmh, _car.ThrottleInput);

            if (instruction != null)
            {
                _panel.SetActive(true);
                _stepText.text = _car.IsStalled && !_car.EngineOn
                    ? "Se caló. Tranquilo: " + instruction.ToLower()
                    : instruction;
                _doneTimer = -1f;
            }
            else
            {
                // Ya maneja: mensaje de cierre 4 segundos y el panel se va.
                if (_doneTimer < 0f)
                {
                    _doneTimer = 4f;
                    _stepText.text = "¡Eso es, camarón!  (H = ver controles)";
                }
                _doneTimer -= Time.deltaTime;
                if (_doneTimer <= 0f) _panel.SetActive(false);
            }
        }
    }
}
