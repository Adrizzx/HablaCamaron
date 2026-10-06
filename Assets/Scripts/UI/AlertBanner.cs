using UnityEngine;
using UnityEngine.UI;

namespace HablaCamaron.UI
{
    /// <summary>Urgencia de un aviso: manda el número más alto.</summary>
    public enum AlertPriority
    {
        /// <summary>Informativo (vas fuera de la ruta prevista).</summary>
        Info = 0,
        /// <summary>Cuidado (te saliste de la calzada).</summary>
        Warning = 1,
        /// <summary>Peligro real (contravía).</summary>
        Danger = 2,
    }

    /// <summary>
    /// EL CANAL ÚNICO DE AVISOS del gameplay (playtest 2026-07-25: "las
    /// indicaciones no están muy buenas... buena interfaz de usuario").
    ///
    /// Antes cada sistema pintaba su propio texto en su propia franja: el
    /// aviso de contravía (y=-210), el de fuera de ruta (y=-260), la guía
    /// (y=-196) y los toasts (y=-180) se SOLAPABAN — medido — así que en una
    /// curva mal tomada se veían tres mensajes encimados y no se entendía
    /// ninguno. Ahora todos publican aquí y solo se muestra EL MÁS URGENTE,
    /// en un sitio fijo que el jugador aprende a mirar.
    ///
    /// Cada aviso se re-publica mientras siga vigente; si deja de publicarse
    /// desaparece solo (no hace falta que nadie lo apague).
    /// </summary>
    public class AlertBanner : MonoBehaviour
    {
        public static AlertBanner Instance { get; private set; }

        /// <summary>Margen de gracia antes de borrar un aviso que ya no se
        /// publica (s): evita el parpadeo entre frames.</summary>
        private const float HoldSeconds = 0.15f;

        private Text _label;
        private Image _plate;
        private string _text;
        private AlertPriority _priority;
        private float _expiresAt;

        /// <summary>Lo que se está mostrando (vacío si nada). Para tests.</summary>
        public string CurrentText => _expiresAt > Time.unscaledTime ? _text : "";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Crea el banner si aún no existe (lo llama quien avisa).</summary>
        public static AlertBanner Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[AlertBanner]");
            return go.AddComponent<AlertBanner>();
        }

        /// <summary>Publica un aviso. Gana el de mayor prioridad del frame.</summary>
        public void Show(string message, AlertPriority priority)
        {
            if (string.IsNullOrEmpty(message)) return;
            bool vigente = _expiresAt > Time.unscaledTime;
            if (vigente && priority < _priority) return; // ya hay algo más urgente
            _text = message;
            _priority = priority;
            _expiresAt = Time.unscaledTime + HoldSeconds;
        }

        private void Build()
        {
            var canvasGO = new GameObject("AlertCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12; // encima del HUD, debajo de la pausa
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Placa de fondo: el texto suelto sobre la ciudad no se leía.
            var panel = UIFactory.Panel("AlertPlate", canvas.transform,
                UITheme.A(UITheme.PanelDark, 0.82f), Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, UITheme.RadiusMd);
            UIFactory.SetRect(panel.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, BannerY), new Vector2(880, 52), new Vector2(0.5f, 1f));
            _plate = panel.GetComponent<Image>();

            _label = UIFactory.Label("AlertText", panel, "", 30,
                UITheme.Danger, TextAnchor.MiddleCenter, FontStyle.Bold);
            var rt = _label.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12f, 0f); rt.offsetMax = new Vector2(-12f, 0f);

            panel.gameObject.SetActive(false);
        }

        /// <summary>Carril vertical del banner: bajo el timer y la guía, sin
        /// pisar a ninguno (ver la tabla de carriles en HUDLayout).</summary>
        public const float BannerY = HUDLayout.BannerY;

        private void LateUpdate()
        {
            bool on = _expiresAt > Time.unscaledTime;
            var plate = _plate != null ? _plate.gameObject : null;
            if (plate == null) return;
            if (plate.activeSelf != on) plate.SetActive(on);
            if (!on) return;

            _label.text = _text;
            var color = _priority switch
            {
                AlertPriority.Danger => UITheme.Danger,
                AlertPriority.Warning => UITheme.Gold,
                _ => UITheme.TextCream,
            };
            // Parpadeo solo en lo grave: el aviso informativo no debe agobiar.
            if (_priority == AlertPriority.Danger)
                color.a = 0.7f + 0.3f * Mathf.PingPong(Time.unscaledTime * 2.4f, 1f);
            _label.color = color;
        }
    }

    /// <summary>
    /// Los CARRILES de la franja superior del HUD, en un solo sitio. Estaban
    /// repartidos por cinco archivos y se solapaban entre ellos (medido:
    /// timer/toast/guía/avisos compartían píxeles). Cualquier elemento nuevo
    /// de esa franja debe declararse aquí para que el solape se vea a simple
    /// vista en vez de descubrirlo jugando.
    /// </summary>
    public static class HUDLayout
    {
        /// <summary>Tiempo restante y distancia a la meta.</summary>
        public const float TimerY = -152f;
        /// <summary>Copiloto: "EN 40 m GIRA A LA DERECHA".</summary>
        public const float GuideY = -208f;
        /// <summary>Canal único de avisos (contravía, fuera de ruta...).</summary>
        public const float BannerY = -266f;
        /// <summary>Toasts de acción ("Marcha: 2ª", "Luces: largas").</summary>
        public const float ToastY = -326f;
    }
}
