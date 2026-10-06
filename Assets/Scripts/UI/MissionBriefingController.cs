using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.Core;

namespace HablaCamaron.UI
{
    /// <summary>Datos de un briefing de misión.</summary>
    public struct MissionBriefingData
    {
        public string title;
        public string zone;
        public string moment;
        public string donPanchoLine;
        public string[] objectives;
    }

    /// <summary>
    /// PANTALLA 6 — BRIEFING DE MISIÓN. Don Pancho explica el objetivo con su analogía
    /// ANTES de conducir. Overlay sobre la escena Gameplay.
    /// Se invoca con MissionBriefingController.Show(data, onStart).
    /// </summary>
    public class MissionBriefingController : MonoBehaviour
    {
        private System.Action _onStart;

        public static MissionBriefingController Show(MissionBriefingData data, System.Action onStart)
        {
            var go = new GameObject("MissionBriefing");
            var ctrl = go.AddComponent<MissionBriefingController>();
            ctrl._onStart = onStart;
            ctrl.Build(data);
            return ctrl;
        }

        private void Build(MissionBriefingData data)
        {
            var canvasGO = new GameObject("BriefingCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var root = canvas.transform;
            BackgroundBuilder.BuildSunsetBackground(root, withSun: false);
            // Oscurecedor para foco dramático.
            UIFactory.Panel("Dim", root, new Color(0.05f, 0.03f, 0.02f, 0.55f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Lado izquierdo: avatar de Don Pancho.
            var avatar = UIFactory.GlowCircle("Avatar", root, UITheme.A(UITheme.Gold, 0.9f), 220);
            UIFactory.SetRect(avatar, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(280, 60), new Vector2(220, 220), new Vector2(0.5f, 0.5f));
            // El RETRATO de Don Pancho en grande (antes un emoji de 90 pt).
            var face = CharacterPortrait.Create(root, Character.DonPancho, 205f);
            UIFactory.SetRect(face.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(280, 60), new Vector2(205, 205), new Vector2(0.5f, 0.5f));
            var name = UIFactory.Label("Name", root,
                CharacterPortrait.DisplayName(Character.DonPancho), 34, UITheme.TextCream,
                TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.SetRect(name.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(280, -120), new Vector2(360, 44), new Vector2(0.5f, 0.5f));
            var role = UIFactory.Label("Role", root, "Instructor", 22, UITheme.Gold,
                TextAnchor.UpperCenter, FontStyle.Italic);
            UIFactory.SetRect(role.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(280, -160), new Vector2(360, 30), new Vector2(0.5f, 0.5f));

            // Lado derecho: tarjeta con info.
            var card = UIFactory.GradientCard("Card", root, UITheme.PanelWarm, UITheme.PanelDark,
                UITheme.RadiusLg);
            UIFactory.SetRect(card.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(220, 0), new Vector2(940, 680));
            UIFactory.AddGlowEdge(card.gameObject, UITheme.A(UITheme.Gold, 0.25f), 1.5f);

            var t = UIFactory.Label("Title", card, data.title, 42, UITheme.TextCream,
                TextAnchor.UpperLeft, FontStyle.BoldAndItalic);
            UIFactory.SetRect(t.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(48, -36), new Vector2(840, 54), new Vector2(0, 1));
            var zone = UIFactory.Pill("Zone", card, data.zone + "  \u00b7  " + data.moment,
                UITheme.A(UITheme.GroundDeep, 0.6f), UITheme.Gold, 20);
            UIFactory.SetRect(zone.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(48, -98), new Vector2(360, 36), new Vector2(0, 1));

            // Burbuja de diálogo.
            var bubble = UIFactory.Panel("Bubble", card, UITheme.A(UITheme.GroundDeep, 0.5f),
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero, UITheme.RadiusMd);
            UIFactory.SetRect(bubble.gameObject, new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            bubble.offsetMin = new Vector2(48, 0); bubble.offsetMax = new Vector2(-48, 0);
            bubble.anchoredPosition = new Vector2(0, -240); bubble.sizeDelta = new Vector2(bubble.sizeDelta.x, 150);
            var line = UIFactory.Label("Line", bubble, "\u201C" + data.donPanchoLine + "\u201D", 24,
                UITheme.TextSand, TextAnchor.MiddleLeft, FontStyle.Italic);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetRect(line.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            line.rectTransform.offsetMin = new Vector2(24, 12); line.rectTransform.offsetMax = new Vector2(-24, -12);

            // Objetivos.
            var oh = UIFactory.Label("ObjHdr", card, "QU\u00c9 SE EVAL\u00daA", 20, UITheme.Gold,
                TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.SetRect(oh.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(48, -360), new Vector2(400, 28), new Vector2(0, 1));
            float oy = -402f;
            if (data.objectives != null)
            {
                foreach (var obj in data.objectives)
                {
                    var check = UIFactory.LabeledCircle("Chk", card, "\u2713",
                        UITheme.A(UITheme.Success, 0.9f), UITheme.TextCream, 30, 17);
                    UIFactory.SetRect(check.circle.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                        new Vector2(60, oy - 1), new Vector2(30, 30), new Vector2(0, 1));
                    var li = UIFactory.Label("Obj", card, obj, 23, UITheme.TextCream,
                        TextAnchor.UpperLeft, FontStyle.Normal);
                    UIFactory.SetRect(li.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                        new Vector2(100, oy), new Vector2(780, 32), new Vector2(0, 1));
                    oy -= 44f;
                }
            }

            // Separador sobre los botones.
            var div = UIFactory.Divider("Div", card, UITheme.A(UITheme.Gold, 0.18f));
            UIFactory.SetRect(div.gameObject, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(48, 130), new Vector2(844, 2), new Vector2(0, 0));

            // Botones (en la base, bien separados de los objetivos).
            var go = UIFactory.GradientButton("\u00a1Vamos, camar\u00f3n!  \u279C", card, OnStart, true, true, 28);
            UIFactory.SetRect(go, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-48, 36), new Vector2(380, 72), new Vector2(1, 0));
            var back = UIFactory.GradientButton("\u2190  Volver al mapa", card, OnBack, false, true, 22);
            UIFactory.SetRect(back, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(48, 36), new Vector2(280, 72), new Vector2(0, 0));
        }

        private void OnStart()
        {
            _onStart?.Invoke();
            Destroy(gameObject);
        }

        private void OnBack() => SceneLoader.Instance?.Load(SceneLoader.SCENE_CAMPAIGN);
    }
}
