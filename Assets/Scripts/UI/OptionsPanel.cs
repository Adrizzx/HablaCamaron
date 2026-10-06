using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HablaCamaron.UI
{
    /// <summary>
    /// PANEL DE OPCIONES (overlay modal sobre el menú).
    /// Cubre el criterio de "configuración" de la rúbrica: volumen por capa
    /// (música / efectos / voz), sensibilidad de pedales e idioma.
    /// Guarda en PlayerPrefs. Se crea oculto con Create() y se muestra con Show().
    /// </summary>
    public class OptionsPanel : MonoBehaviour
    {
        private CanvasGroup _group;
        private RectTransform _card;
        private Text _savedToast; // aviso "✓ Cambios guardados"

        public static OptionsPanel Create(Transform parent)
        {
            var go = new GameObject("OptionsPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<OptionsPanel>();
            panel.Build();
            panel.Hide();
            return panel;
        }

        private void Build()
        {
            // OJO: Build() se vuelve a llamar al Restablecer. AddComponent
            // ciego dejaba un CanvasGroup DUPLICADO (el viejo se quedaba con
            // alpha 0 y el panel se veía muerto). Se reutiliza el existente.
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // Fondo oscurecedor (clic fuera = cerrar)
            var dim = UIFactory.Panel("Dim", transform, new Color(0.08f, 0.05f, 0.035f, 0.55f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Hide);

            // Tarjeta modal central con gradiente y borde dorado.
            var card = UIFactory.GradientCard("Card", transform, UITheme.PanelWarm, UITheme.PanelDark,
                UITheme.RadiusLg);
            _card = card;
            UIFactory.SetRect(card.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720, 760));
            var cardEdge = card.gameObject.AddComponent<Outline>();
            cardEdge.effectColor = UITheme.A(UITheme.Gold, 0.4f);
            cardEdge.effectDistance = new Vector2(1.5f, -1.5f);

            // Encabezado: engranaje + título.
            var gear = UIFactory.Label("Gear", card, "\u2699", 38, UITheme.Gold,
                TextAnchor.MiddleLeft, FontStyle.Normal);
            UIFactory.SetRect(gear.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(40, -34), new Vector2(50, 50), new Vector2(0, 1));
            var title = UIFactory.Label("Title", card, Core.UIStrings.T("opt.title"), 40,
                UITheme.TextCream, TextAnchor.UpperLeft, FontStyle.BoldAndItalic);
            UIFactory.SetRect(title.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(92, -34), new Vector2(400, 50), new Vector2(0, 1));

            var close = UIFactory.GradientButton("X", card, Hide, false, true, 26);
            UIFactory.SetRect(close, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-40, -34), new Vector2(52, 52), new Vector2(1, 1));

            float y = -130f;
            SectionLabel(card, Core.UIStrings.T("opt.audio"), ref y);
            Slider(card, Core.UIStrings.T("opt.music"), "opt_vol_music", 0.30f, ref y);
            Slider(card, Core.UIStrings.T("opt.sfx"), "opt_vol_sfx", 0.75f, ref y);
            // El slider de "Voz de Don Pancho" se quitó de la INTERFAZ (pedido
            // del dueño, 2026-07-28). La voz sigue funcionando con su valor
            // por defecto: la clave opt_vol_voice se conserva, GameAudioSettings
            // la sigue leyendo y Restablecer la deja en 0.90.

            y -= 20f;
            SectionLabel(card, Core.UIStrings.T("opt.driving"), ref y);
            Slider(card, Core.UIStrings.T("opt.pedals"), "opt_pedal_sens", 0.50f, ref y);

            // Idioma (toggle ES/EN)
            LanguageToggle(card, ref y);

            // Botones inferiores
            var reset = UIFactory.GradientButton(Core.UIStrings.T("opt.reset"), card,
                ResetDefaults, false, true, 22);
            UIFactory.SetRect(reset, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(40, 40), new Vector2(220, 64), new Vector2(0, 0));
            var save = UIFactory.GradientButton(Core.UIStrings.T("opt.save"), card,
                SaveChanges, true, true, 24);
            UIFactory.SetRect(save, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-40, 40), new Vector2(380, 64), new Vector2(1, 0));

            // Aviso de guardado (invisible hasta que se pulsa Guardar): antes
            // el bot\u00f3n cerraba el panel sin decir nada y parec\u00eda que no hac\u00eda
            // nada (playtest).
            _savedToast = UIFactory.Label("SavedToast", card, Core.UIStrings.T("opt.saved"), 22,
                UITheme.SuccessLight, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetRect(_savedToast.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 118), new Vector2(420, 32), new Vector2(0.5f, 0));
            _savedToast.canvasRenderer.SetAlpha(0f);
        }

        /// <summary>
        /// Guardar de verdad: persiste PlayerPrefs, reaplica el audio al mixer
        /// (para que se OIGA sin reiniciar) y confirma en pantalla antes de
        /// cerrar \u2014 el usuario debe VER que su cambio qued\u00f3.
        /// </summary>
        private void SaveChanges()
        {
            PlayerPrefs.Save();
            Core.GameAudioSettings.Refresh();
            if (isActiveAndEnabled) StartCoroutine(ConfirmAndClose());
            else Hide();
        }

        private IEnumerator ConfirmAndClose()
        {
            _savedToast.text = Core.UIStrings.T("opt.saved");
            _savedToast.canvasRenderer.SetAlpha(1f);
            float t = 0f;
            while (t < 0.75f) { t += Time.unscaledDeltaTime; yield return null; }
            _savedToast.canvasRenderer.SetAlpha(0f);
            Hide();
        }

        // ---------- componentes ----------
        private void SectionLabel(Transform parent, string text, ref float y)
        {
            var l = UIFactory.Label("Sec_" + text, parent, text, 22, UITheme.Ocre,
                TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.SetRect(l.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(40, y), new Vector2(600, 30), new Vector2(0, 1));
            y -= 50f;
        }

        private void Slider(Transform parent, string label, string prefKey, float def, ref float y)
        {
            float value = PlayerPrefs.GetFloat(prefKey, def);

            var name = UIFactory.Label("Lbl_" + prefKey, parent, label, 24, UITheme.CreamSoft,
                TextAnchor.UpperLeft);
            UIFactory.SetRect(name.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(40, y), new Vector2(450, 30), new Vector2(0, 1));

            var valTxt = UIFactory.Label("Val_" + prefKey, parent, Mathf.RoundToInt(value * 100) + "%", 24,
                UITheme.Ocre, TextAnchor.UpperRight, FontStyle.Bold);
            UIFactory.SetRect(valTxt.gameObject, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-40, y), new Vector2(120, 30), new Vector2(1, 1));

            // Slider de Unity estilizado con la paleta
            var sgo = new GameObject("Slider_" + prefKey, typeof(RectTransform));
            sgo.transform.SetParent(parent, false);
            var srt = (RectTransform)sgo.transform;
            srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(0.5f, 1);
            srt.offsetMin = new Vector2(40, 0); srt.offsetMax = new Vector2(-40, 0);
            srt.anchoredPosition = new Vector2(0, y - 40);
            srt.sizeDelta = new Vector2(srt.sizeDelta.x, 24);

            var slider = sgo.AddComponent<Slider>();

            var bg = UIFactory.Panel("BG", sgo.transform, UITheme.Surface,
                new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -4), new Vector2(0, 4), 6);

            var fillArea = new GameObject("FillArea").AddComponent<RectTransform>();
            fillArea.SetParent(sgo.transform, false);
            fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.offsetMin = new Vector2(0, -4); fillArea.offsetMax = new Vector2(0, 4);
            var fill = UIFactory.Panel("Fill", fillArea, Color.white,
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, 6);
            var fillGrad = fill.gameObject.AddComponent<VerticalGradient>();
            fillGrad.SetColors(UITheme.Gold, UITheme.AccentTop);

            var handle = UIFactory.Panel("Handle", sgo.transform, UITheme.Cream,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 12);
            handle.sizeDelta = new Vector2(22, 22);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = value;
            slider.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetFloat(prefKey, v);
                valTxt.text = Mathf.RoundToInt(v * 100) + "%";
                // El mixer cachea los valores: refrescar para oírlo al instante.
                Core.GameAudioSettings.Refresh();
            });

            y -= 90f;
        }

        private void LanguageToggle(Transform parent, ref float y)
        {
            var name = UIFactory.Label("Lbl_lang", parent, Core.UIStrings.T("opt.lang"), 24,
                UITheme.CreamSoft, TextAnchor.UpperLeft);
            UIFactory.SetRect(name.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(40, y), new Vector2(300, 30), new Vector2(0, 1));

            int lang = PlayerPrefs.GetInt("opt_lang", 0); // 0=ES 1=EN
            string[] labels = { "Espa\u00f1ol", "English" };
            var es = UIFactory.Button(labels[0], parent, null, lang == 0, true, 20);
            var en = UIFactory.Button(labels[1], parent, null, lang == 1, true, 20);
            UIFactory.SetRect(es, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-180, y + 4), new Vector2(130, 44), new Vector2(1, 1));
            UIFactory.SetRect(en, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, y + 4), new Vector2(130, 44), new Vector2(1, 1));

            // Cambiar idioma RECONSTRUYE el panel: así el cambio se VE al
            // instante (antes solo se guardaba la clave y no pasaba nada).
            es.GetComponent<Button>().onClick.AddListener(() => SetLanguage(Core.Lang.Es));
            en.GetComponent<Button>().onClick.AddListener(() => SetLanguage(Core.Lang.En));

            y -= 80f;
        }

        /// <summary>Cambia el idioma, lo persiste y redibuja el panel traducido.</summary>
        private void SetLanguage(Core.Lang lang)
        {
            if (Core.UIStrings.Current == lang) return;
            Core.UIStrings.Current = lang; // persiste solo
            Rebuild();
        }

        private void ResetDefaults()
        {
            PlayerPrefs.SetFloat("opt_vol_music", 0.30f);
            PlayerPrefs.SetFloat("opt_vol_sfx", 0.75f);
            PlayerPrefs.SetFloat("opt_vol_voice", 0.90f);
            PlayerPrefs.SetFloat("opt_pedal_sens", 0.50f);
            PlayerPrefs.SetInt(Core.UIStrings.KEY_LANG, 0);
            PlayerPrefs.Save();
            Core.GameAudioSettings.Refresh(); // que los valores por defecto se OIGAN ya
            Rebuild();
        }

        /// <summary>
        /// Redibuja el panel con los valores/idioma actuales. Los hijos viejos
        /// se destruyen AL INSTANTE (DestroyImmediate no vale en runtime, así
        /// que se desprenden de la jerarquía antes de reconstruir): con Destroy
        /// a secas convivían un frame con los nuevos y se veía todo duplicado.
        /// </summary>
        private void Rebuild()
        {
            // De ATRÁS hacia adelante: un `foreach` sobre `transform` mientras
            // se desprenden hijos salta la mitad (el enumerador compara contra
            // `childCount`, que baja en cada vuelta) — así "Restablecer" y el
            // cambio de idioma dejaban vivo el Card viejo completo (sliders y
            // todo) debajo del nuevo, para siempre. Quitando por índice desde
            // el final, cada Destroy no afecta a los que faltan por recorrer.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
            Build();
            Show();
        }

        public void Show()
        {
            _group.alpha = 1f; _group.interactable = true; _group.blocksRaycasts = true;
            transform.SetAsLastSibling();
            if (isActiveAndEnabled && _card != null) StartCoroutine(ScaleIn());
        }

        // Animación de entrada: la tarjeta crece de 0.92 a 1 en 0.15s.
        private IEnumerator ScaleIn()
        {
            float t = 0f; const float dur = 0.15f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(0.92f, 1f, t / dur);
                _card.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            _card.localScale = Vector3.one;
        }
        public void Hide()
        {
            _group.alpha = 0f; _group.interactable = false; _group.blocksRaycasts = false;
        }
    }
}
