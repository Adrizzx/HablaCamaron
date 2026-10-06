using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using HablaCamaron.UI;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// Herramienta de Editor: genera todas las escenas del juego, les coloca su
    /// controlador (la UI se construye sola por código) y las agrega a Build Settings
    /// en el orden correcto. Todo desde el menú "Habla Camarón".
    /// No toca nada en tiempo de ejecución; es solo para preparar el proyecto.
    /// </summary>
    public static class SceneSetupTool
    {
        private const string ScenesDir = "Assets/Scenes";

        [MenuItem("Habla Camar\u00f3n/1 \u00b7 Crear todas las escenas y Build Settings")]
        public static void SetupAllScenes()
        {
            if (!AssetDatabase.IsValidFolder(ScenesDir))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            // (nombre de escena, acción que arma su contenido)
            var scenes = new (string name, System.Action root)[]
            {
                ("Splash",      () => AddController<SplashController>("[Splash]")),
                ("MainMenu",    BuildMainMenu),
                ("CampaignMap", () => AddController<CampaignMapController>("[CampaignMap]")),
                ("Garage",      BuildGarage),
                ("Gameplay",    BuildGameplay),
                ("Credits",     () => AddController<CreditsController>("[Credits]")),
            };

            foreach (var (name, builder) in scenes)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                AddEventSystem();
                builder();
                string path = $"{ScenesDir}/{name}.unity";
                EditorSceneManager.SaveScene(scene, path);
                Debug.Log($"[Habla Camar\u00f3n] Escena creada: {path}");
            }

            // Orden en Build Settings (Splash primero = \u00edndice 0), CONSERVANDO
            // las escenas de zona ya generadas (men\u00fas 2-5). Antes este men\u00fa las
            // borraba de Build Settings y las misiones dejaban de cargar en el
            // .exe hasta regenerar las zonas.
            string[] order = { "Splash", "MainMenu", "CampaignMap", "Garage", "Gameplay", "Credits" };
            var list = new List<EditorBuildSettingsScene>();
            foreach (var n in order)
                list.Add(new EditorBuildSettingsScene($"{ScenesDir}/{n}.unity", true));
            foreach (var old in EditorBuildSettings.scenes)
            {
                string file = System.IO.Path.GetFileNameWithoutExtension(old.path);
                if ((file.StartsWith("N0_") || file.StartsWith("N1_")) &&
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(old.path) != null)
                    list.Add(new EditorBuildSettingsScene(old.path, true));
            }
            EditorBuildSettings.scenes = list.ToArray();

            // Deja abierta la escena Splash para probar de inmediato.
            EditorSceneManager.OpenScene($"{ScenesDir}/Splash.unity");

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Habla Camar\u00f3n",
                "\u00a1Listo! Se crearon las 6 escenas y se configuraron en Build Settings.\n\n" +
                "Pulsa Play para empezar desde el Splash.", "Genial");
        }

        // Crea un GameObject con el controlador indicado.
        private static T AddController<T>(string goName) where T : Component
        {
            var go = new GameObject(goName);
            return go.AddComponent<T>();
        }

        private static void BuildGarage()
        {
            var controller = AddController<GarageController>("[Garage]");
            var aveo = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Toon Series/Toon City/Prefabs/Vehicles/Car_14C.prefab");
            // Toon City no incluye una BT-50 licenciada; Car_10C es su silueta
            // de camioneta de trabajo más cercana y conserva el estilo del juego.
            var bt50 = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Toon Series/Toon City/Prefabs/Vehicles/Car_10C.prefab");
            controller.ConfigureVehicleModels(aveo, bt50);
        }

        private static void BuildMainMenu()
        {
            var controller = AddController<MainMenuController>("[MenuController]");
            var aveo = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Toon Series/Toon City/Prefabs/Vehicles/Car_14C.prefab");
            var bt50 = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Toon Series/Toon City/Prefabs/Vehicles/Car_10C.prefab");
            controller.ConfigureVehicleModels(aveo, bt50);
        }

        // Gameplay necesita varios managers de UI sobre el mismo objeto.
        // (El GameplayUITester demo se retiró: el gameplay definitivo son las
        // zonas N1_* con el Aveo real — gotcha saldado.)
        private static void BuildGameplay()
        {
            var go = new GameObject("[GameplayUI]");
            go.AddComponent<HUDController>();
            go.AddComponent<PauseController>();
            go.AddComponent<DonPanchoDialogue>();
        }

        // EventSystem para que los botones de uGUI respondan a clics.
        private static void AddEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
