using UnityEditor;
using UnityEngine;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// Ajusta la importación de las ilustraciones de personajes
    /// (`Assets/Resources/Portraits/*.png`) para que Unity las trate como
    /// Sprite listas para uGUI, no como textura genérica de 5 MB cada una.
    /// Corre en batch (`-executeMethod HablaCamaron.EditorTools.PortraitImportTool.Apply`).
    /// </summary>
    public static class PortraitImportTool
    {
        private const string PortraitsDir = "Assets/Resources/Portraits";

        [MenuItem("Habla Camarón/Retratos · Importar (sprites)")]
        public static void Apply()
        {
            if (!AssetDatabase.IsValidFolder(PortraitsDir))
            {
                Debug.LogWarning($"PortraitImportTool: no existe {PortraitsDir}; nada que importar.");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitsDir });
            int ajustados = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.ToLowerInvariant().EndsWith(".png")) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = 1024;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
                ajustados++;
            }

            Debug.Log($"PortraitImportTool: {ajustados} retrato(s) ajustado(s) a Sprite ≤ 1024 px.");
            AssetDatabase.Refresh();
        }
    }
}
