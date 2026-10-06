using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.Core;
using HablaCamaron.Missions;

namespace HablaCamaron.UI
{
    /// <summary>
    /// PANTALLA 5 — MAPA DE CAMPAÑA. Las misiones del MissionCatalog (7 del GDD)
    /// encadenadas sur → norte, en dos filas. Estado leído de GameManager.Data:
    /// aprobada / actual / bloqueada. "Jugar" guarda hc_current_mission (varias
    /// misiones comparten zona) y carga la escena de la misión.
    /// Pon el script en un GameObject de la escena "CampaignMap".
    /// </summary>
    public class CampaignMapController : MonoBehaviour
    {
        private const int PerRow = 4;                       // tarjetas por fila
        private static readonly Vector2 CardSize = new Vector2(400, 330);
        private const float CardGapX = 40f, RowGapY = 44f;

        private Canvas _canvas;

        private void Start()
        {
            BuildCanvas();
            BuildScreen();
            ShowPendingChat();
        }

        // Chat de Mishel pendiente tras aprobar una misión (el motor emocional
        // del GDD: los chats llegan ENTRE misiones, aquí en el mapa).
        private void ShowPendingChat()
        {
            int id = PlayerPrefs.GetInt(MissionCatalog.KEY_PENDING_CHAT, -1);
            if (id < 0) return;
            PlayerPrefs.DeleteKey(MissionCatalog.KEY_PENDING_CHAT);
            PlayerPrefs.Save();

            var def = MissionCatalog.Get(id);
            if (def?.ChatAfter != null)
                MishelChatController.Show(def.ChatAfter);
        }

        private void BuildCanvas()
        {
            var go = new GameObject("CampaignCanvas");
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

            // Header.
            var back = UIFactory.GradientButton("←  Atrás", root, () =>
                SceneLoader.Instance?.Load(SceneLoader.SCENE_MAIN_MENU), false, true, 22);
            UIFactory.SetRect(back, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(60, -60), new Vector2(180, 60), new Vector2(0, 1));

            var title = UIFactory.Label("Title", root, "Campaña", 56, UITheme.TextCream,
                TextAnchor.UpperCenter, FontStyle.BoldAndItalic);
            UIFactory.SetRect(title.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -46), new Vector2(600, 70), new Vector2(0.5f, 1));
            var dir = UIFactory.Label("Dir", root, "SUR → NORTE", 22, UITheme.Gold,
                TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.SetRect(dir.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -112), new Vector2(600, 32), new Vector2(0.5f, 1));

            int unlocked = GameManager.Instance != null ? GameManager.Instance.Data.HighestUnlockedMission : 0;
            int count = MissionCatalog.All.Length;

            // Tarjetas en filas de PerRow, cada fila centrada.
            int rows = Mathf.CeilToInt(count / (float)PerRow);
            float firstRowY = (rows - 1) * (CardSize.y + RowGapY) * 0.5f - 30f;
            for (int i = 0; i < count; i++)
            {
                int row = i / PerRow;
                int inRow = Mathf.Min(count - row * PerRow, PerRow);
                int col = i % PerRow;
                float rowWidth = inRow * CardSize.x + (inRow - 1) * CardGapX;
                float x = -rowWidth * 0.5f + CardSize.x * 0.5f + col * (CardSize.x + CardGapX);
                float y = firstRowY - row * (CardSize.y + RowGapY);
                BuildMissionCard(root, new Vector2(x, y), i, unlocked);
            }

            BuildProgressBar(root, unlocked, count);
        }

        private void BuildMissionCard(Transform root, Vector2 pos, int id, int unlocked)
        {
            var def = MissionCatalog.Get(id);
            bool approved = id < unlocked;
            bool current = id == unlocked;
            bool locked = id > unlocked;

            var card = UIFactory.GradientCard("Mission_" + id, root, UITheme.PanelWarm, UITheme.PanelDark,
                UITheme.RadiusLg);
            UIFactory.SetRect(card.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, CardSize);

            var cg = card.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = locked ? 0.55f : 1f;

            // Borde según estado.
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(3, -3);
            if (approved) outline.effectColor = UITheme.Success;
            else if (current) outline.effectColor = UITheme.AccentEdge;
            else outline.effectColor = new Color(0, 0, 0, 0);

            // Círculo con número.
            var number = UIFactory.LabeledCircle("Num", card, (id + 1).ToString(),
                UITheme.PanelDark, locked ? UITheme.TextLocked : UITheme.TextCream, 72, 30);
            UIFactory.SetRect(number.circle.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -46), new Vector2(72, 72), new Vector2(0.5f, 1));
            var cgrad = number.circle.gameObject.AddComponent<VerticalGradient>();
            if (approved) cgrad.SetColors(UITheme.Gold, UITheme.GoldDeep);
            else if (current) cgrad.SetColors(UITheme.AccentTop, UITheme.AccentBot);
            else cgrad.SetColors(UITheme.PanelDark, UITheme.GroundDeep);

            // Título y zona (del catálogo: una sola fuente de verdad).
            var t = UIFactory.Label("Tit", card, def.Title, 23, UITheme.TextCream,
                TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.SetRect(t.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -122), new Vector2(370, 34), new Vector2(0.5f, 1));
            var z = UIFactory.Label("Zone", card, def.Briefing.zone, 17, UITheme.Gold,
                TextAnchor.UpperCenter, FontStyle.Italic);
            UIFactory.SetRect(z.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -158), new Vector2(370, 26), new Vector2(0.5f, 1));

            if (approved)
            {
                int score = GameManager.Instance.Data.MissionScores[id];
                var badge = UIFactory.Panel("Badge", card, UITheme.Success,
                    new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero, UITheme.RadiusMd);
                UIFactory.SetRect(badge.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(-88, 34), new Vector2(180, 48), new Vector2(0.5f, 0));
                var bl = UIFactory.Label("BadgeLbl", badge, "✓  ★ " + score + "/100", 21,
                    UITheme.TextCream, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.SetRect(bl.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                UIFactory.FitTextInside(bl, 21, 14);

                // Repetir una misión aprobada para subir el puntaje (se guarda
                // el MEJOR: repetirla jamás retrocede el progreso — testeado).
                var replay = UIFactory.GradientButton("Repetir", card, () => OnPlay(id), false, true, 19);
                UIFactory.SetRect(replay, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(102, 34), new Vector2(180, 48), new Vector2(0.5f, 0));
            }
            else if (current)
            {
                var play = UIFactory.GradientButton("Jugar", card, () => OnPlay(id), false, true, 24);
                play.GetComponent<VerticalGradient>().SetColors(UITheme.Gold, UITheme.SkyGlow);
                UIFactory.SetRect(play, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0, 34), new Vector2(190, 54), new Vector2(0.5f, 0));
            }
            else
            {
                var lk = UIFactory.Pill("Lock", card, "\U0001F512  Aprueba la misión " + id,
                    UITheme.A(UITheme.GroundDeep, 0.7f), UITheme.TextLocked, 16);
                UIFactory.SetRect(lk.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0, 38), new Vector2(300, 38), new Vector2(0.5f, 0));
            }
        }

        private void BuildProgressBar(Transform root, int unlocked, int total)
        {
            float pct = Mathf.Clamp01(unlocked / (float)total);

            var barBg = UIFactory.Panel("ProgBG", root, UITheme.PanelDark,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero, 6);
            UIFactory.SetRect(barBg.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 88), new Vector2(900, 14), new Vector2(0.5f, 0));

            var fill = UIFactory.Panel("ProgFill", barBg, Color.white,
                new Vector2(0, 0), new Vector2(pct, 1), Vector2.zero, Vector2.zero, 6);
            fill.GetComponent<Image>().raycastTarget = false;
            fill.gameObject.AddComponent<VerticalGradient>().SetColors(UITheme.Gold, UITheme.AccentTop);

            var lbl = UIFactory.Label("ProgLbl", root,
                "Progreso de campaña: " + unlocked + " de " + total + " aprobadas", 22, UITheme.TextSand,
                TextAnchor.LowerCenter, FontStyle.Italic);
            UIFactory.SetRect(lbl.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 40), new Vector2(800, 30), new Vector2(0.5f, 0));
        }

        private void OnPlay(int id)
        {
            // hc_current_mission le dice al MissionRunner CUÁL misión de la zona
            // se juega (T1, T2, C1 y C2 comparten la Zona Sur).
            PlayerPrefs.SetInt(MissionCatalog.KEY_CURRENT, id);
            PlayerPrefs.Save();
            string scene = MissionCatalog.SceneFor(id) ?? SceneLoader.SCENE_GAMEPLAY;
            SceneLoader.Instance?.Load(scene);
        }
    }
}
