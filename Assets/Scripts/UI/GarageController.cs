using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.Core;
using HablaCamaron.Vehicle;

namespace HablaCamaron.UI
{
    /// <summary>
    /// PANTALLA 4 — GARAJE (funcional desde la Fase 4). Dos vehículos de
    /// VehicleRoster: el Chevrolet Aveo 2008 ("el carro escuela", siempre
    /// disponible) y la Mazda BT-50 (con candado hasta aprobar "La Simón de
    /// noche"). Elegir guarda PlayerPrefs hc_vehicle y las escenas de manejo
    /// re-equipan el auto al cargar (MissionSystemBootstrap).
    /// Pon el script en un GameObject de la escena "Garage".
    /// </summary>
    public class GarageController : MonoBehaviour
    {
        [Header("Modelos de la vitrina (los asigna SceneSetupTool)")]
        [SerializeField] private GameObject aveoPreviewPrefab;
        [SerializeField] private GameObject bt50PreviewPrefab;

        private Canvas _canvas;
        private int _selected;

        public void ConfigureVehicleModels(GameObject aveo, GameObject bt50)
        {
            aveoPreviewPrefab = aveo;
            bt50PreviewPrefab = bt50;
        }

        private void Start()
        {
            var data = GameManager.Instance != null ? GameManager.Instance.Data : null;
            _selected = VehicleRoster.SelectedId(data);
            BuildCanvas();
            BuildScreen();
        }

        private void BuildCanvas()
        {
            var go = new GameObject("GarageCanvas");
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
            BackgroundBuilder.BuildSunsetBackground(root, withSun: false);

            // Piso de garaje: banda oscura inferior.
            var floor = UIFactory.Panel("Floor", root, UITheme.A(UITheme.GroundDeep, 0.55f),
                new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, Vector2.zero, 0f);
            UIFactory.SetRect(floor.gameObject, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 130), new Vector2(0, 260), new Vector2(0.5f, 0));
            floor.GetComponent<Image>().raycastTarget = false;

            // ---- Header ----
            var back = UIFactory.GradientButton("←  Atrás", root, () =>
                SceneLoader.Instance?.Load(SceneLoader.SCENE_MAIN_MENU), false, true, 22);
            UIFactory.SetRect(back, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(60, -60), new Vector2(170, 58), new Vector2(0, 1));

            var title = UIFactory.Label("Title", root, "GARAJE", 58, UITheme.TextCream,
                TextAnchor.UpperCenter, FontStyle.BoldAndItalic);
            UIFactory.AddDropShadow(title.gameObject, 0.5f, -3f);
            UIFactory.SetRect(title.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -54), new Vector2(800, 70), new Vector2(0.5f, 1));
            var sub = UIFactory.Label("Sub", root, "Elige tu vehículo de práctica", 24,
                UITheme.Gold, TextAnchor.UpperCenter, FontStyle.Italic);
            UIFactory.SetRect(sub.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -122), new Vector2(800, 34), new Vector2(0.5f, 1));

            var data = GameManager.Instance != null ? GameManager.Instance.Data : null;
            bool bt50Unlocked = VehicleRoster.IsUnlocked(VehicleRoster.Bt50Id, data);

            BuildVehicleCard(root, -350f, VehicleRoster.AveoId, unlocked: true,
                aveoPreviewPrefab,
                "\U0001F697", "Chevrolet Aveo 2008", "El carro escuela",
                "Suave y perdonador. Ideal para dominar el embrague sin " +
                "frustrarte. Es con el que tu papá te enseña a manejar.",
                new[] { ("Transmisión", "Manual 5 vel."), ("Dificultad", "Principiante"),
                        ("Perdón al embrague", "Alto") });

            BuildVehicleCard(root, 350f, VehicleRoster.Bt50Id, bt50Unlocked,
                bt50PreviewPrefab,
                "\U0001F69A", "Mazda BT-50", "La camioneta del tío",
                "Pesada y con más inercia: arranca con fuerza pero exige un " +
                "embrague fino. Para cuando ya hablas el idioma del Aveo.",
                new[] { ("Transmisión", "Manual 5 vel."), ("Dificultad", "Avanzada"),
                        ("Perdón al embrague", "Bajo") });

            // Confirmar: vuelve al menú (la selección ya quedó guardada).
            var confirm = UIFactory.GradientButton("Confirmar  ✓", root, OnConfirm, true, true, 28);
            UIFactory.SetRect(confirm, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 44), new Vector2(380, 70), new Vector2(0.5f, 0));
        }

        private void BuildVehicleCard(Transform root, float x, int id, bool unlocked,
            GameObject previewPrefab, string glyph, string name, string tagline, string desc,
            (string, string)[] stats)
        {
            bool selected = id == _selected;

            var card = UIFactory.GradientCard("Vehiculo_" + id, root, UITheme.PanelWarm,
                UITheme.PanelDark, UITheme.RadiusLg);
            UIFactory.SetRect(card.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, 20), new Vector2(620, 560));
            if (selected) UIFactory.AddGlowEdge(card.gameObject, UITheme.AccentEdge, 2.5f);

            var cg = card.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = unlocked ? 1f : 0.6f;

            // Escenario con el vehículo bajo su reflector.
            var stage = UIFactory.Panel("Stage", card, UITheme.A(UITheme.GroundDeep, 0.45f),
                new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero, UITheme.RadiusMd);
            UIFactory.SetRect(stage.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -20), new Vector2(580, 220), new Vector2(0.5f, 1));
            var glow = UIFactory.GlowCircle("Glow", stage, UITheme.A(UITheme.SkyGlow, 0.25f), 300);
            UIFactory.SetRect(glow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(300, 190), new Vector2(0.5f, 0.5f));
            // Vitrina en tres escalones, de más a menos rico: el modelo 3D real
            // girando (lo mejor: es EL auto que se maneja), la foto del vehículo
            // si la escena no trae prefab, y el glifo como último recurso.
            if (previewPrefab != null)
            {
                var previewObject = new GameObject("Modelo3D", typeof(RectTransform), typeof(RawImage));
                previewObject.transform.SetParent(stage, false);
                UIFactory.SetRect(previewObject, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
                var rect = (RectTransform)previewObject.transform;
                rect.offsetMin = new Vector2(10f, 8f);
                rect.offsetMax = new Vector2(-10f, -8f);
                previewObject.AddComponent<VehiclePreview3D>()
                    .Initialize(previewPrefab, unlocked, 30 + id);

                var hint = UIFactory.Pill("Hint3D", stage, "MODELO TOON 3D  •  VISTA 360°",
                    UITheme.A(UITheme.GroundDeep, 0.62f), UITheme.TextMuted, 12);
                UIFactory.SetRect(hint.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0, 10), new Vector2(250, 25), new Vector2(0.5f, 0));
            }
            else if (VehiclePortraits.TryPhoto(id, out var foto))
            {
                // Escena sin prefab de vitrina: al menos la foto del vehículo.
                var fotoGO = new GameObject("Foto", typeof(RectTransform));
                fotoGO.transform.SetParent(stage, false);
                var img = fotoGO.AddComponent<Image>();
                img.sprite = foto;
                img.preserveAspect = true;
                img.raycastTarget = false;
                // Bloqueada = atenuada (el candado ya lo pone el CanvasGroup).
                img.color = unlocked ? Color.white : UITheme.TextLocked;
                UIFactory.SetRect(fotoGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0, 6), new Vector2(360, 190), new Vector2(0.5f, 0.5f));
            }
            else
            {
                // Fallback legible si alguien abre una escena antigua sin regenerarla.
                var car = UIFactory.Label("Car", stage, glyph, 120,
                    unlocked ? UITheme.TextCream : UITheme.TextLocked,
                    TextAnchor.MiddleCenter, FontStyle.Normal);
                UIFactory.SetRect(car.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0, 6), new Vector2(300, 160), new Vector2(0.5f, 0.5f));
            }

            if (!unlocked)
            {
                var locked = UIFactory.Pill("Bloqueado3D", stage, "🔒  BLOQUEADO",
                    UITheme.A(UITheme.GroundDeep, 0.82f), UITheme.TextLocked, 14);
                UIFactory.SetRect(locked.gameObject, new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-14, -14), new Vector2(150, 30), new Vector2(1, 1));
            }

            // Ficha.
            var nm = UIFactory.Label("Name", card, name, 32, UITheme.TextCream,
                TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.SetRect(nm.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -252), new Vector2(560, 40), new Vector2(0.5f, 1));
            var tag = UIFactory.Label("Tag", card, tagline, 20, UITheme.Gold,
                TextAnchor.UpperCenter, FontStyle.Italic);
            UIFactory.SetRect(tag.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -292), new Vector2(560, 28), new Vector2(0.5f, 1));

            var d = UIFactory.Label("Desc", card, desc, 19, UITheme.TextSoft,
                TextAnchor.UpperCenter, FontStyle.Normal);
            d.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetRect(d.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -322), new Vector2(540, 80), new Vector2(0.5f, 1));

            float sy = -412f;
            foreach (var (label, value) in stats)
            {
                var l = UIFactory.Label("S_" + label, card, label, 18, UITheme.TextMuted,
                    TextAnchor.MiddleLeft, FontStyle.Normal);
                UIFactory.SetRect(l.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(50, sy), new Vector2(280, 28), new Vector2(0, 1));
                var v = UIFactory.Pill("V_" + label, card, value,
                    UITheme.A(UITheme.GroundDeep, 0.6f), UITheme.Gold, 16);
                UIFactory.SetRect(v.gameObject, new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-50, sy - 2), new Vector2(180, 28), new Vector2(1, 1));
                sy -= 38f;
            }

            // Estado: seleccionado / elegible / bloqueado.
            if (selected)
            {
                var selectedMark = UIFactory.LabeledCircle("Seleccionado", card, "✓",
                    UITheme.Success, UITheme.TextCream, 54f, 30);
                UIFactory.SetRect(selectedMark.circle.gameObject,
                    new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0, 20), new Vector2(54, 54), new Vector2(0.5f, 0));
                UIFactory.AddGlowEdge(selectedMark.circle.gameObject,
                    UITheme.A(UITheme.SuccessLight, 0.65f), 1.5f);
            }
            else if (unlocked)
            {
                var pick = UIFactory.GradientButton("Elegir", card, () => OnSelect(id), false, true, 22);
                pick.GetComponent<VerticalGradient>().SetColors(UITheme.Gold, UITheme.SkyGlow);
                UIFactory.SetRect(pick, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0, 22), new Vector2(200, 50), new Vector2(0.5f, 0));
            }
            else
            {
                var unlockDef = Missions.MissionCatalog.Get(VehicleRoster.Bt50UnlockMissionId);
                var lk = UIFactory.Pill("Lock", card,
                    "\U0001F512  Aprueba “" + (unlockDef?.Title ?? "La Simón de noche") + "”",
                    UITheme.A(UITheme.GroundDeep, 0.7f), UITheme.TextLocked, 16);
                UIFactory.SetRect(lk.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0, 26), new Vector2(330, 38), new Vector2(0.5f, 0));
            }
        }

        private void OnSelect(int id)
        {
            _selected = id;
            PlayerPrefs.SetInt(VehicleRoster.KEY_SELECTED, id);
            PlayerPrefs.Save();
            // Reconstruir para reflejar la nueva selección.
            Destroy(_canvas.gameObject);
            BuildCanvas();
            BuildScreen();
        }

        private void OnConfirm() => SceneLoader.Instance?.Load(SceneLoader.SCENE_MAIN_MENU);
    }
}
