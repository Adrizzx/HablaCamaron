using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.Core;

namespace HablaCamaron.UI
{
    /// <summary>
    /// PANTALLA 13 — CRÉDITOS. Scroll vertical lento, estilo cinematográfico de cierre.
    /// Acelera manteniendo una tecla. Pon el script en un GameObject de la escena "Credits"
    /// o instáncialo como panel desde MainMenu.
    /// </summary>
    public class CreditsController : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _scroll;
        private float _contentHeight;
        private bool _finished;

        private void Start()
        {
            BuildCanvas();
            BuildScreen();
            StartCoroutine(ScrollRoutine());
        }

        private void BuildCanvas()
        {
            var go = new GameObject("CreditsCanvas");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
        }

        private void BuildScreen()
        {
            var root = _canvas.transform;
            BackgroundBuilder.BuildSunsetBackground(root);

            // Botón Volver siempre visible arriba.
            var back = UIFactory.GradientButton("\u2190  Volver", root, ToMenu, false, true, 22);
            UIFactory.SetRect(back, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(60, -50), new Vector2(180, 56), new Vector2(0, 1));

            // Contenedor scroll.
            _scroll = UIFactory.Panel("Scroll", root, new Color(0, 0, 0, 0),
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero, 0);
            _scroll.sizeDelta = new Vector2(900, 4000);
            _scroll.pivot = new Vector2(0.5f, 0);
            _scroll.anchoredPosition = new Vector2(0, -1080); // empieza abajo, fuera de pantalla

            float y = -40f;
            BigTitle("\u00a1Habla, Camar\u00f3n!", ref y);
            Sub("Un entorno que ense\u00f1a a manejar en Quito", ref y);
            Gap(ref y, 60);

            Section("EQUIPO DE DESARROLLO", ref y);
            Person("Mateo Iza", "Programaci\u00f3n y arquitectura", ref y);
            Person("Eduardo Garc\u00eda", "Dise\u00f1o UI/UX y direcci\u00f3n de arte", ref y);
            Person("Adri\u00e1n Padilla", "Entorno 3D, assets e identidad visual", ref y);
            Gap(ref y, 40);

            Section("ACAD\u00c9MICO", ref y);
            Person("Ing. H\u00e9ctor Pa\u00fal Pinto Pachacama", "Docente \u2014 Desarrollo de Videojuegos", ref y);
            Sub("Universidad de las Fuerzas Armadas ESPE", ref y);
            Sub("Departamento de Ciencias de la Computaci\u00f3n", ref y);
            Sub("Carrera de Ingenier\u00eda de Software \u00b7 NRC 27857", ref y);
            Gap(ref y, 40);

            Section("RECURSOS", ref y);
            Sub("Toon City \u2014 Asset Store (entorno urbano low-poly)", ref y);
            Sub("Kenney / Quaternius \u2014 modelos CC0 de apoyo", ref y);
            Sub("Modelado propio \u2014 identidad quite\u00f1a (disco PARE, sem\u00e1foro LED, taxi amarillo)", ref y);
            Sub("FMOD Studio \u2014 audio espacial", ref y);
            Sub("Motor: Unity 6.3 LTS", ref y);
            Gap(ref y, 60);

            Sub("Sangolqu\u00ed, 2026", ref y);
            var quote = UIFactory.Label("Quote", _scroll, "\u201CDesign is how it works.\u201D", 26,
                UITheme.Gold, TextAnchor.UpperCenter, FontStyle.Italic);
            UIFactory.SetRect(quote.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, y), new Vector2(800, 40), new Vector2(0.5f, 1));
            y -= 60f;

            _contentHeight = -y;
            _scroll.sizeDelta = new Vector2(900, _contentHeight + 100);
        }

        // ---------- helpers de contenido ----------
        private void BigTitle(string text, ref float y)
        {
            var t = UIFactory.Label("BigTitle", _scroll, text, 64, UITheme.Gold,
                TextAnchor.UpperCenter, FontStyle.BoldAndItalic);
            UIFactory.SetRect(t.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, y), new Vector2(800, 80), new Vector2(0.5f, 1));
            y -= 90f;
        }

        private void Section(string text, ref float y)
        {
            var t = UIFactory.Label("Sec", _scroll, "\u2014  " + text + "  \u2014", 26, UITheme.Gold,
                TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.SetRect(t.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, y), new Vector2(800, 36), new Vector2(0.5f, 1));
            y -= 60f;
        }

        private void Person(string name, string role, ref float y)
        {
            var n = UIFactory.Label("Name", _scroll, name, 34, UITheme.TextCream,
                TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.SetRect(n.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, y), new Vector2(800, 44), new Vector2(0.5f, 1));
            y -= 44f;
            var r = UIFactory.Label("Role", _scroll, role, 22, UITheme.TextMuted,
                TextAnchor.UpperCenter, FontStyle.Normal);
            UIFactory.SetRect(r.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, y), new Vector2(800, 30), new Vector2(0.5f, 1));
            y -= 50f;
        }

        private void Sub(string text, ref float y)
        {
            var t = UIFactory.Label("Sub", _scroll, text, 22, UITheme.TextSoft,
                TextAnchor.UpperCenter, FontStyle.Normal);
            UIFactory.SetRect(t.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, y), new Vector2(900, 30), new Vector2(0.5f, 1));
            y -= 40f;
        }

        private void Gap(ref float y, float amount) => y -= amount;

        private IEnumerator ScrollRoutine()
        {
            float speed = 60f; // px/s
            float distance = _contentHeight + 1080f;
            float traveled = 0f;
            while (traveled < distance)
            {
                float mult = (Input.anyKey) ? 4f : 1f;
                float delta = speed * mult * Time.deltaTime;
                traveled += delta;
                _scroll.anchoredPosition += new Vector2(0, delta);
                yield return null;
            }
            _finished = true;
            ShowEndButton();
        }

        private void ShowEndButton()
        {
            if (_finished == false) return;
            var menu = UIFactory.GradientButton("Volver al men\u00fa", _canvas.transform, ToMenu, true, true, 28);
            UIFactory.SetRect(menu, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(360, 72), new Vector2(0.5f, 0.5f));
        }

        private void ToMenu() => SceneLoader.Instance?.Load(SceneLoader.SCENE_MAIN_MENU);
    }
}
