using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HablaCamaron.UI
{
    /// <summary>
    /// PANTALLA 7 — HUD DE CONDUCCIÓN (escena Gameplay). Singleton.
    /// Filosofía "calma visual": solo lo esencial. Velocímetro, marcha, mini-mapa,
    /// burbujas de Don Pancho e indicadores discretos (freno, intermitente, calado).
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        private Text _speedText, _gearText, _bubbleText;
        private Image _rpmArc;
        private CanvasGroup _bubbleGroup;
        private GameObject _miniMap;
        private Text _handbrakeIcon, _blinkerIcon, _stalledIcon;
        private Coroutine _bubbleRoutine, _blinkRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Build();
        }

        private void Build()
        {
            var canvasGO = new GameObject("HUDCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            var root = canvas.transform;

            BuildMiniMap(root);
            BuildSpeedoCluster(root);
            BuildControlBar(root);
            BuildBubble(root);
        }

        // 1) Clúster velocímetro + marcha (esquina inf-der).
        private void BuildSpeedoCluster(Transform root)
        {
            // Base de vidrio del velocímetro.
            var baseCircle = UIFactory.Circle("Speedo", root, UITheme.A(UITheme.PanelDark, 0.88f), 230);
            UIFactory.SetRect(baseCircle.gameObject, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-170, 175), new Vector2(230, 230), new Vector2(0.5f, 0.5f));
            UIFactory.AddGlowEdge(baseCircle.gameObject, UITheme.A(UITheme.Gold, 0.55f), 2f);
            var gauge = baseCircle.rectTransform;

            // Marcas (ticks) alrededor del aro.
            BuildTicks(gauge, 32, 100f);

            // Arco de RPM (disco radial + disco interior = anillo).
            var arcGO = new GameObject("RpmArc", typeof(RectTransform));
            arcGO.transform.SetParent(gauge, false);
            _rpmArc = arcGO.AddComponent<Image>();
            _rpmArc.sprite = baseCircle.sprite;
            _rpmArc.color = UITheme.Gold;
            _rpmArc.type = Image.Type.Filled;
            _rpmArc.fillMethod = Image.FillMethod.Radial360;
            _rpmArc.fillOrigin = (int)Image.Origin360.Top;
            _rpmArc.fillClockwise = true;
            _rpmArc.fillAmount = 0f;
            _rpmArc.raycastTarget = false;
            var art = (RectTransform)arcGO.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(20, 20); art.offsetMax = new Vector2(-20, -20);

            // Disco interior que recorta el centro y deja solo el anillo.
            var inner = UIFactory.Circle("SpeedoInner", gauge, UITheme.A(UITheme.GroundDeep, 0.95f), 168);
            UIFactory.SetRect(inner.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(168, 168), new Vector2(0.5f, 0.5f));
            inner.raycastTarget = false;
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            _speedText = UIFactory.Label("Speed", inner.transform, "0", 72, UITheme.TextCream,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.FitTextInside(_speedText, 72, 42);
            AddShadow(_speedText);
            UIFactory.SetRect(_speedText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 14), new Vector2(170, 86), new Vector2(0.5f, 0.5f));
            var unit = UIFactory.Label("Unit", inner.transform, "km/h", 22, UITheme.TextSoft,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.FitTextInside(unit, 22, 14);
            UIFactory.SetRect(unit.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -44), new Vector2(120, 28), new Vector2(0.5f, 0.5f));

            // Caja de marcha (a la izquierda de la base).
            var box = UIFactory.GradientCard("GearBox", root, UITheme.AccentTop, UITheme.AccentBot,
                UITheme.RadiusMd);
            UIFactory.SetRect(box.gameObject, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-352, 130), new Vector2(96, 96), new Vector2(0.5f, 0.5f));
            UIFactory.AddGlowEdge(box.gameObject, UITheme.AccentEdge, 1.5f);
            var gearLbl = UIFactory.Label("GearTag", box, "MARCHA", 13, UITheme.A(UITheme.TextCream, 0.7f),
                TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.SetRect(gearLbl.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -8), new Vector2(90, 16), new Vector2(0.5f, 1));
            _gearText = UIFactory.Label("Gear", box, "N", 50, UITheme.TextCream,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.FitTextInside(_gearText, 50, 28);
            AddShadow(_gearText);
            UIFactory.SetRect(_gearText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -6), new Vector2(96, 70), new Vector2(0.5f, 0.5f));
        }

        // Marcas radiales decorativas alrededor del velocímetro.
        private void BuildTicks(RectTransform gauge, int count, float radius)
        {
            for (int i = 0; i < count; i++)
            {
                float frac = i / (float)count;
                float ang = frac * 360f * Mathf.Deg2Rad;
                bool major = i % 4 == 0;
                var tick = UIFactory.Panel("Tick", gauge, UITheme.A(UITheme.Gold, major ? 0.6f : 0.28f),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 0f);
                tick.GetComponent<Image>().raycastTarget = false;
                tick.sizeDelta = new Vector2(major ? 4f : 3f, major ? 14f : 9f);
                tick.anchoredPosition = new Vector2(Mathf.Sin(ang) * radius, Mathf.Cos(ang) * radius);
                tick.localRotation = Quaternion.Euler(0, 0, -frac * 360f);
            }
        }

        // 2) Mini-mapa REAL (esquina sup-der): calles del RoadGraph + jugador +
        //    meta en vivo (MiniMapView). Ocultable con M. En escenas sin grafo
        //    queda la cuadrícula decorativa.
        private void BuildMiniMap(Transform root)
        {
            _miniMap = UIFactory.Panel("MiniMap", root, UITheme.A(UITheme.PanelDark, 0.88f),
                new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero, UITheme.RadiusMd).gameObject;
            UIFactory.SetRect(_miniMap, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-150, -150), new Vector2(240, 240), new Vector2(0.5f, 0.5f));
            UIFactory.AddGlowEdge(_miniMap, UITheme.A(UITheme.Gold, 0.55f), 2f);
            var mapRt = (RectTransform)_miniMap.transform;

            // Cuadrícula tenue de fondo.
            for (int i = 1; i < 4; i++)
            {
                var hLine = UIFactory.Divider("H" + i, mapRt, UITheme.A(UITheme.Gold, 0.10f));
                UIFactory.SetRect(hLine.gameObject, new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                    new Vector2(0, 120 - i * 60), new Vector2(0, 1.5f), new Vector2(0.5f, 0.5f));
                var vLine = UIFactory.Panel("V" + i, mapRt, UITheme.A(UITheme.Gold, 0.10f),
                    new Vector2(0.5f, 0), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero, 0f);
                vLine.GetComponent<Image>().raycastTarget = false;
                UIFactory.SetRect(vLine.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 1),
                    new Vector2(-120 + i * 60, 0), new Vector2(1.5f, 0), new Vector2(0.5f, 0.5f));
            }

            // Contenido vivo (calles + jugador + meta) dentro del marco.
            var contentGO = new GameObject("Contenido", typeof(RectTransform));
            contentGO.transform.SetParent(mapRt, false);
            var content = (RectTransform)contentGO.transform;
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(200, 200);
            _miniMap.AddComponent<MiniMapView>().Init(content);

            // Brújula y etiqueta.
            var north = UIFactory.Pill("N", mapRt, "N", UITheme.A(UITheme.GroundDeep, 0.7f), UITheme.Gold, 16);
            UIFactory.SetRect(north.gameObject, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-22, -22), new Vector2(34, 26), new Vector2(0.5f, 0.5f));
            var lbl = UIFactory.Label("MapLbl", mapRt, "Mapa", 16, UITheme.A(UITheme.TextSoft, 0.85f),
                TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.SetRect(lbl.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(14, -10), new Vector2(80, 20), new Vector2(0, 1));
        }

        // 3) Burbuja de Don Pancho (inf-centro, sobre la barra).
        private void BuildBubble(Transform root)
        {
            var card = UIFactory.GradientCard("Bubble", root, UITheme.PanelWarm, UITheme.PanelDark,
                UITheme.RadiusLg);
            UIFactory.SetRect(card.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 170), new Vector2(640, 104));
            UIFactory.AddGlowEdge(card.gameObject, UITheme.A(UITheme.Gold, 0.35f), 1.5f);
            _bubbleGroup = card.gameObject.AddComponent<CanvasGroup>();
            _bubbleGroup.alpha = 0f;

            var avatar = UIFactory.GlowCircle("Av", card, UITheme.Gold, 76);
            UIFactory.SetRect(avatar, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(54, 0), new Vector2(76, 76), new Vector2(0.5f, 0.5f));
            // La CARA de Don Pancho dibujada por c\u00F3digo (antes era un emoji, que
            // ni se ve igual en todos los equipos ni es "el" personaje).
            var face = CharacterPortrait.Create(card, Character.DonPancho, 68f);
            UIFactory.SetRect(face.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(54, 0), new Vector2(68, 68), new Vector2(0.5f, 0.5f));

            var nm = UIFactory.Label("Name", card,
                CharacterPortrait.DisplayName(Character.DonPancho), 18, UITheme.Gold,
                TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.SetRect(nm.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(104, -14), new Vector2(300, 22), new Vector2(0, 1));

            _bubbleText = UIFactory.Label("Text", card, "", 24, UITheme.TextCream,
                TextAnchor.UpperLeft, FontStyle.Normal);
            _bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
            AddShadow(_bubbleText);
            UIFactory.SetRect(_bubbleText.gameObject, new Vector2(0, 0), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            _bubbleText.rectTransform.offsetMin = new Vector2(104, 12);
            _bubbleText.rectTransform.offsetMax = new Vector2(-22, -40);
        }

        // 4) Barra de estado inferior (inf-izq): chips de freno, intermitente, calado.
        private void BuildControlBar(Transform root)
        {
            var bar = UIFactory.GradientCard("StatusBar", root, UITheme.A(UITheme.PanelDark, 0.7f),
                UITheme.A(UITheme.GroundDeep, 0.7f), UITheme.RadiusMd);
            UIFactory.SetRect(bar.gameObject, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(180, 70), new Vector2(330, 80), new Vector2(0.5f, 0.5f));

            _handbrakeIcon = MakeChip(bar, "\U0001F590", "Freno", -106);
            _blinkerIcon   = MakeChip(bar, "\u2B05", "Direc.", 0);
            _stalledIcon   = MakeChip(bar, "\u2699", "Motor", 106);
        }

        // Crea un chip con ícono (Text cuyo color cambia) y etiqueta debajo. Devuelve el ícono.
        private Text MakeChip(RectTransform bar, string glyph, string label, float x)
        {
            var chip = UIFactory.Panel("Chip", bar, UITheme.A(UITheme.GroundDeep, 0.4f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, UITheme.RadiusMd);
            UIFactory.SetRect(chip.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, 0), new Vector2(96, 64), new Vector2(0.5f, 0.5f));

            var icon = UIFactory.Label("Icon", chip, glyph, 28, UITheme.TextMuted,
                TextAnchor.MiddleCenter, FontStyle.Normal);
            UIFactory.SetRect(icon.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -22), new Vector2(60, 34), new Vector2(0.5f, 0.5f));
            var lbl = UIFactory.Label("Lbl", chip, label, 14, UITheme.A(UITheme.TextSoft, 0.8f),
                TextAnchor.LowerCenter, FontStyle.Bold);
            UIFactory.SetRect(lbl.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 8), new Vector2(90, 18), new Vector2(0.5f, 0));
            return icon;
        }

        private void AddShadow(Text t)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, 0.55f);
            s.effectDistance = new Vector2(0, -2);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M) && _miniMap != null)
                _miniMap.SetActive(!_miniMap.activeSelf);
        }

        // ---------------- API pública ----------------

        public void SetSpeed(float kmh)
        {
            if (_speedText != null) _speedText.text = Mathf.RoundToInt(Mathf.Max(0, kmh)).ToString();
        }

        public void SetRpm(float normalized)
        {
            if (_rpmArc == null) return;
            normalized = Mathf.Clamp01(normalized);
            _rpmArc.fillAmount = normalized;
            _rpmArc.color = Color.Lerp(UITheme.Gold, UITheme.Danger, Mathf.InverseLerp(0.7f, 1f, normalized));
        }

        public void SetGear(string gear)
        {
            if (_gearText != null) _gearText.text = gear;
        }

        /// <summary>
        /// Feedback del cambio: el número de marcha "salta" y destella
        /// (dorado si el cambio fue limpio, rojo si rechinó la caja).
        /// </summary>
        public void FlashGear(bool clean)
        {
            if (_gearText == null) return;
            if (_gearFlashRoutine != null) StopCoroutine(_gearFlashRoutine);
            _gearFlashRoutine = StartCoroutine(GearFlashRoutine(clean ? UITheme.GoldLight : UITheme.Danger));
        }

        private Coroutine _gearFlashRoutine;

        private IEnumerator GearFlashRoutine(Color flash)
        {
            var rt = _gearText.rectTransform;
            Color baseColor = UITheme.TextCream;
            float t = 0f;
            const float dur = 0.28f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = 1f - t / dur; // 1 → 0
                rt.localScale = Vector3.one * (1f + 0.45f * k);
                _gearText.color = Color.Lerp(baseColor, flash, k);
                yield return null;
            }
            rt.localScale = Vector3.one;
            _gearText.color = baseColor;
        }

        public void ShowMessage(string msg, float duration = 3f)
        {
            if (_bubbleText == null) return;
            _bubbleText.text = msg;
            if (_bubbleRoutine != null) StopCoroutine(_bubbleRoutine);
            _bubbleRoutine = StartCoroutine(BubbleRoutine(duration));
        }

        // Tiempo sin escalar: la burbuja también habla con el juego congelado
        // (el veredicto de Don Pancho llega con timeScale = 0).
        private IEnumerator BubbleRoutine(float duration)
        {
            yield return Fade(_bubbleGroup, 1f, 0.2f);
            yield return new WaitForSecondsRealtime(duration);
            yield return Fade(_bubbleGroup, 0f, 0.3f);
        }

        private IEnumerator Fade(CanvasGroup g, float target, float dur)
        {
            float start = g.alpha, t = 0f;
            while (t < dur) { t += Time.unscaledDeltaTime; g.alpha = Mathf.Lerp(start, target, t / dur); yield return null; }
            g.alpha = target;
        }

        public void SetHandbrake(bool active)
        {
            if (_handbrakeIcon != null) _handbrakeIcon.color = active ? UITheme.Danger : UITheme.TextMuted;
        }

        public void SetStalled(bool active)
        {
            if (_stalledIcon != null) _stalledIcon.color = active ? UITheme.Danger : UITheme.TextMuted;
        }

        /// <summary>dir: -1 izquierda, 1 derecha, 0 = conservar la flecha actual.
        /// El ícono APUNTA hacia el lado señalizado (antes siempre miraba a la izquierda).</summary>
        public void SetBlinker(bool active, int dir = 0)
        {
            if (_blinkerIcon == null) return;
            if (dir < 0) _blinkerIcon.text = "⬅";      // ⬅ izquierda
            else if (dir > 0) _blinkerIcon.text = "➡"; // ➡ derecha
            if (_blinkRoutine != null) { StopCoroutine(_blinkRoutine); _blinkRoutine = null; }
            if (active) _blinkRoutine = StartCoroutine(BlinkRoutine());
            else _blinkerIcon.color = UITheme.TextMuted;
        }

        private IEnumerator BlinkRoutine()
        {
            while (true)
            {
                _blinkerIcon.color = UITheme.Gold;
                yield return new WaitForSeconds(0.4f);
                _blinkerIcon.color = UITheme.TextMuted;
                yield return new WaitForSeconds(0.4f);
            }
        }
    }
}
