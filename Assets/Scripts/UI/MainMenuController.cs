using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.Core;
using HablaCamaron.Vehicle;

namespace HablaCamaron.UI
{
    /// <summary>
    /// PANTALLA 2 — MENÚ PRINCIPAL (estilo videojuego "atardecer quiteño").
    /// Fondo degradado + sol + viñeta, silueta de ciudad y auto héroe a la derecha.
    /// "Continuar" es la acción primaria; "Salir" queda desjerarquizada.
    /// Pon este script en un GameObject vacío de la escena MainMenu. Se arma solo.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Modelos de portada (los asigna SceneSetupTool)")]
        [SerializeField] private GameObject aveoHeroPrefab;
        [SerializeField] private GameObject bt50HeroPrefab;

        private Canvas _canvas;
        private OptionsPanel _options;

        public void ConfigureVehicleModels(GameObject aveo, GameObject bt50)
        {
            aveoHeroPrefab = aveo;
            bt50HeroPrefab = bt50;
        }

        private void Start()
        {
            BuildCanvas();
            BuildMenu();
            _options = OptionsPanel.Create(_canvas.transform); // oculto al inicio
        }

        private void BuildCanvas()
        {
            var go = new GameObject("MenuCanvas");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
        }

        private void BuildMenu()
        {
            var root = _canvas.transform;

            // Fondo atardecer (degradado + sol + viñeta).
            BackgroundBuilder.BuildSunsetBackground(root);

            // Silueta de ciudad + auto héroe en la parte baja/derecha.
            BuildCitySkyline(root);
            BuildHeroCar(root);

            // --- Kicker con línea dorada ---
            var line = UIFactory.Panel("KickerLine", root, UITheme.Gold,
                new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero, 2);
            UIFactory.SetRect(line.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(120, -70), new Vector2(40, 6), new Vector2(0, 1));
            var kicker = UIFactory.Label("Kicker", root, "APRENDE A MANEJAR EN QUITO", 24,
                UITheme.GoldLight, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.SetRect(kicker.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(176, -82), new Vector2(900, 36), new Vector2(0, 1));

            // --- Título dos líneas con sombra ---
            var t1 = UIFactory.Label("Title1", root, "\u00a1Habla,", 96, UITheme.TextCream,
                TextAnchor.UpperLeft, FontStyle.BoldAndItalic);
            AddTextShadow(t1);
            UIFactory.SetRect(t1.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(116, -120), new Vector2(1000, 120), new Vector2(0, 1));
            var t2 = UIFactory.Label("Title2", root, "Camar\u00f3n!", 96, UITheme.Gold,
                TextAnchor.UpperLeft, FontStyle.BoldAndItalic);
            AddTextShadow(t2);
            UIFactory.SetRect(t2.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(116, -220), new Vector2(1000, 120), new Vector2(0, 1));

            var promise = UIFactory.Label("Promise", root,
                "DOMINA EL EMBRAGUE  •  CAMBIA MARCHAS  •  CONQUISTA LAS CUESTAS", 18,
                UITheme.TextSand, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.SetRect(promise.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(120, -342), new Vector2(720, 32), new Vector2(0, 1));
            UIFactory.FitTextInside(promise, 18, 13);

            float colX = 120f;
            float colW = 620f;

            // --- PRIMARIO: Continuar ---
            bool hasSave = GameManager.Instance != null && GameManager.Instance.HasSavedGame();
            var cont = UIFactory.GradientButton("\u25B6  " + Core.UIStrings.T("menu.continue"),
                root, OnContinue, true, hasSave, 32);
            UIFactory.SetRect(cont, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(colX, -400), new Vector2(colW, 84), new Vector2(0, 1));

            // --- Nueva partida (ancho completo) ---
            var nueva = UIFactory.GradientButton(Core.UIStrings.T("menu.new"),
                root, OnNewGame, false, true, 28);
            UIFactory.SetRect(nueva, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(colX, -496), new Vector2(colW, 74), new Vector2(0, 1));

            // --- Grid 2x2 ---
            float half = (colW - 16) / 2f;
            var misiones = UIFactory.GradientButton("Misiones", root, OnFreePlay, false, true, 24);
            UIFactory.SetRect(misiones, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(colX, -582), new Vector2(half, 70), new Vector2(0, 1));
            var garaje = UIFactory.GradientButton("Garaje", root, OnGarage, false, true, 24);
            UIFactory.SetRect(garaje, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(colX + half + 16, -582), new Vector2(half, 70), new Vector2(0, 1));

            var opciones = UIFactory.GradientButton(Core.UIStrings.T("menu.options"),
                root, OnOptions, false, true, 24);
            UIFactory.SetRect(opciones, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(colX, -660), new Vector2(half, 70), new Vector2(0, 1));

            // --- Salir: desjerarquizado ---
            var salir = UIFactory.GradientButton(Core.UIStrings.T("menu.quit"),
                root, OnQuit, false, true, 24);
            UIFactory.SetRect(salir, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(colX + half + 16, -660), new Vector2(half, 70), new Vector2(0, 1));
            var salirGrad = salir.GetComponent<VerticalGradient>();
            salirGrad.SetColors(UITheme.A(UITheme.PanelDark, 0.5f), UITheme.A(UITheme.GroundDeep, 0.5f));
            salir.GetComponentInChildren<Text>().color = UITheme.TextMuted;

            // Crédito de autores.
            var footer = UIFactory.Label("Footer", root, "Garc\u00eda \u00b7 Iza \u00b7 Padilla   \u00b7   ESPE", 22,
                UITheme.TextSand, TextAnchor.LowerRight, FontStyle.Italic);
            AddTextShadow(footer);
            UIFactory.SetRect(footer.gameObject, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-60, 50), new Vector2(800, 40), new Vector2(1, 0));
        }

        // Silueta de ciudad: edificios rectangulares de distintas alturas + poste de luz.
        private void BuildCitySkyline(Transform root)
        {
            var skyline = new GameObject("Skyline", typeof(RectTransform));
            skyline.transform.SetParent(root, false);
            var srt = (RectTransform)skyline.transform;
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0);
            srt.pivot = new Vector2(0.5f, 0);
            srt.anchoredPosition = new Vector2(0, 0);
            srt.sizeDelta = new Vector2(0, 320);

            float[] heights = { 120, 200, 90, 260, 150, 110, 220, 80, 180, 130, 240, 100 };
            float x = 40f;
            for (int i = 0; i < heights.Length; i++)
            {
                float w = Random.Range(90f, 150f);
                var b = UIFactory.Panel("B" + i, srt, UITheme.A(UITheme.GroundDeep, 0.92f),
                    new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, Vector2.zero);
                UIFactory.SetRect(b.gameObject, new Vector2(0, 0), new Vector2(0, 0),
                    new Vector2(x, 0), new Vector2(w, heights[i]), new Vector2(0, 0));
                b.GetComponent<Image>().raycastTarget = false;
                x += w + 18f;
            }

            // Poste de luz con glow dorado.
            var pole = UIFactory.Panel("Pole", srt, UITheme.GroundDeep,
                new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, Vector2.zero);
            UIFactory.SetRect(pole.gameObject, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(x + 60, 0), new Vector2(8, 280), new Vector2(0, 0));
            pole.GetComponent<Image>().raycastTarget = false;
            var lamp = UIFactory.GlowCircle("Lamp", srt, UITheme.A(UITheme.GoldLight, 0.85f), 70);
            UIFactory.SetRect(lamp, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(x + 64, 270), new Vector2(70, 70), new Vector2(0.5f, 0.5f));
        }

        // Portada jugable: el vehículo actualmente elegido, en una vitrina 3D
        // sobre una calle estilizada. Comunica el tema antes de leer el menú.
        private void BuildHeroCar(Transform root)
        {
            var data = GameManager.Instance != null ? GameManager.Instance.Data : null;
            int selectedId = VehicleRoster.SelectedId(data);
            GameObject prefab = selectedId == VehicleRoster.Bt50Id ? bt50HeroPrefab : aveoHeroPrefab;

            var card = UIFactory.GradientCard("HeroGarage", root,
                UITheme.A(UITheme.PanelWarm, 0.76f), UITheme.A(UITheme.GroundDeep, 0.90f),
                UITheme.RadiusLg);
            UIFactory.SetRect(card.gameObject, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-490, 10), new Vector2(820, 700), new Vector2(0.5f, 0.5f));
            UIFactory.AddGlowEdge(card.gameObject, UITheme.A(UITheme.Gold, 0.28f), 1.5f);
            var heroButton = card.gameObject.AddComponent<Button>();
            heroButton.targetGraphic = card.GetComponent<Image>();
            heroButton.transition = Selectable.Transition.ColorTint;
            heroButton.onClick.AddListener(OnGarage);

            var kicker = UIFactory.Pill("HeroKicker", card, "TU VEHÍCULO  •  CLIC PARA CAMBIAR",
                UITheme.A(UITheme.GroundDeep, 0.72f), UITheme.GoldLight, 15);
            UIFactory.SetRect(kicker.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(28, -22), new Vector2(310, 32), new Vector2(0, 1));

            var selectedMark = UIFactory.LabeledCircle("Activo", card, "✓",
                UITheme.Success, UITheme.TextCream, 46f, 25);
            UIFactory.SetRect(selectedMark.circle.gameObject,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -20),
                new Vector2(46, 46), new Vector2(1, 1));

            var stage = UIFactory.Panel("RoadStage", card, UITheme.A(UITheme.GroundDeep, 0.55f),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero,
                UITheme.RadiusMd);
            UIFactory.SetRect(stage.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -70), new Vector2(770, 410), new Vector2(0.5f, 1));
            BuildRoadMarks(stage);

            if (prefab != null)
            {
                var preview = new GameObject("Vehiculo3D", typeof(RectTransform), typeof(RawImage));
                preview.transform.SetParent(stage, false);
                var rt = (RectTransform)preview.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(10, 12);
                rt.offsetMax = new Vector2(-10, -4);
                preview.AddComponent<VehiclePreview3D>().Initialize(prefab, true, 30 + selectedId);
            }
            else
            {
                var fallback = UIFactory.Label("VehiculoFallback", stage, "\U0001F697", 150,
                    UITheme.TextCream, TextAnchor.MiddleCenter);
                UIFactory.SetRect(fallback.gameObject, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero);
                UIFactory.FitTextInside(fallback, 150, 70);
            }

            var vehicleName = UIFactory.Label("VehicleName", card,
                MainMenuHeroContent.VehicleName(selectedId), 34, UITheme.TextCream,
                TextAnchor.UpperCenter, FontStyle.BoldAndItalic);
            UIFactory.SetRect(vehicleName.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -500), new Vector2(720, 44), new Vector2(0.5f, 1));
            UIFactory.FitTextInside(vehicleName, 34, 22);

            var role = UIFactory.Label("VehicleRole", card,
                MainMenuHeroContent.VehicleRole(selectedId), 19, UITheme.Gold,
                TextAnchor.UpperCenter, FontStyle.Italic);
            UIFactory.SetRect(role.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -548), new Vector2(700, 30), new Vector2(0.5f, 1));

            string[] skills = { "EMBRAGUE", "5 MARCHAS", "QUITO" };
            float[] xs = { -220f, 0f, 220f };
            for (int i = 0; i < skills.Length; i++)
            {
                var chip = UIFactory.Pill("Skill_" + i, card, skills[i],
                    UITheme.A(UITheme.PanelWarm, 0.72f), UITheme.TextSand, 15);
                UIFactory.SetRect(chip.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(xs[i], 42), new Vector2(180, 38), new Vector2(0.5f, 0));
            }
        }

        private static void BuildRoadMarks(RectTransform stage)
        {
            var road = UIFactory.Panel("Asfalto", stage, UITheme.A(UITheme.PanelDark, 0.72f),
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero,
                UITheme.RadiusMd);
            UIFactory.SetRect(road.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 64), new Vector2(700, 105), new Vector2(0.5f, 0));
            road.GetComponent<Image>().raycastTarget = false;

            for (int i = -2; i <= 2; i++)
            {
                var dash = UIFactory.Panel("LineaVia", road, UITheme.A(UITheme.GoldLight, 0.72f),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 2f);
                UIFactory.SetRect(dash.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(i * 120f, 0), new Vector2(64, 7), new Vector2(0.5f, 0.5f));
                dash.GetComponent<Image>().raycastTarget = false;
            }
        }

        private void AddTextShadow(Text t)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, 0.5f);
            s.effectDistance = new Vector2(0, -3);
        }

        // ---------- Navegación ----------
        private void OnContinue() => SceneLoader.Instance?.Load(SceneLoader.SCENE_CAMPAIGN);
        private void OnNewGame()
        {
            GameManager.Instance?.NewGame();
            SceneLoader.Instance?.Load(SceneLoader.SCENE_CAMPAIGN);
        }
        private void OnFreePlay() => SceneLoader.Instance?.Load(SceneLoader.SCENE_CAMPAIGN);
        private void OnGarage() => SceneLoader.Instance?.Load(SceneLoader.SCENE_GARAGE);
        private void OnOptions() => _options?.Show();
        private void OnQuit() => SceneLoader.Instance?.QuitGame();
    }

    /// <summary>Contenido puro de la portada según el vehículo elegido.</summary>
    public static class MainMenuHeroContent
    {
        public static string VehicleName(int vehicleId) =>
            vehicleId == VehicleRoster.Bt50Id ? "Mazda BT-50" : "Chevrolet Aveo 2008";

        public static string VehicleRole(int vehicleId) =>
            vehicleId == VehicleRoster.Bt50Id
                ? "La camioneta avanzada · pesada y exigente"
                : "El carro escuela · suave y perdonador";
    }
}
