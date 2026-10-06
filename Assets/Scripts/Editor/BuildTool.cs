using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// FASE 4 — Build Windows con un clic (o en batch con -executeMethod).
    /// Toma las escenas habilitadas de Build Settings (Splash primero) y
    /// produce Builds/Windows/HablaCamaron.exe. La carpeta Builds/ está
    /// ignorada en git: el build se genera, no se versiona.
    /// Menú: Habla Camarón > 7 · Build Windows (Fase 4)
    /// </summary>
    public static class BuildTool
    {
        private const string OutputDir = "Builds/Windows";
        private const string OutputExe = OutputDir + "/HablaCamaron.exe";

        [MenuItem("Habla Camarón/7 · Build Windows (Fase 4)")]
        public static void BuildWindows()
        {
            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) scenes.Add(s.path);

            if (scenes.Count == 0)
            {
                EditorUtility.DisplayDialog("Habla Camarón",
                    "No hay escenas en Build Settings. Corre primero los menús 1-5.", "OK");
                return;
            }

            // CAUSA REAL del crash "level7 is corrupted" (2026-07-20): la
            // carpeta de salida se reciclaba entre builds. CleanBuildCache
            // limpia la caché de Unity, NO el directorio destino, así que
            // UnityPlayer.dll, MonoBleedingEdge y sobre todo los metadatos de
            // scripting (ScriptingAssemblies.json / RuntimeInitializeOnLoads
            // .json) quedaban de un build viejo. Con clases nuevas en el
            // proyecto, los levels serializan tipos que ese mapa de
            // ensamblados no describe: al deserializar, el lector se sale de
            // rango ("Position out of bounds!") y el player declara el level
            // corrupto. Borrar el destino ANTES de construir lo elimina.
            if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);
            Directory.CreateDirectory(OutputDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = OutputExe,
                target = BuildTarget.StandaloneWindows64,
                // OJO: se probó CompressWithLz4 contra el mismo crash
                // (2026-07-14) y NO cambia nada: el problema era el destino
                // reciclado, no el camino de lectura ni la compresión.
                options = BuildOptions.CleanBuildCache,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Habla Camarón] Build OK: {OutputExe} " +
                          $"({summary.totalSize / (1024 * 1024)} MB, " +
                          $"{scenes.Count} escenas, {summary.totalTime.TotalMinutes:0.0} min).");
                EditorUtility.DisplayDialog("Habla Camarón — Fase 4",
                    $"Build Windows listo:\n{OutputExe}\n\n" +
                    $"{scenes.Count} escenas · {summary.totalSize / (1024 * 1024)} MB.\n" +
                    "Pruébalo con el mando de carreras del laboratorio.", "¡A jugar!");
            }
            else
            {
                Debug.LogError($"[Habla Camarón] Build FALLÓ: {summary.result} " +
                               $"({summary.totalErrors} errores). Revisa la consola.");
            }
        }
    }
}
