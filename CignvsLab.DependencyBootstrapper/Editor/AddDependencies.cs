using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics; // <- for Process
using Debug = UnityEngine.Debug;

namespace CignvsLab.Editor
{
    [InitializeOnLoad]
    public static class AddDependencies
    {
        static AddDependencies()
        {
            // This runs automatically on Editor domain reload (package import, Unity startup, etc)
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ShowInstallReminderOnce();
            }
        }

        private static void ShowInstallReminderOnce()
        {
            const string key = "CignvsLab.ShowedInstallReminder";

            if (!SessionState.GetBool(key, false))
            {
                EditorApplication.update += () =>
                {
                    // Delay to ensure Unity finishes importing other assets
                    if (!SessionState.GetBool(key, false))
                    {
                        SessionState.SetBool(key, true);
                        EditorUtility.DisplayDialog(
                            "Install Dependencies",
                            "The CignvsLab package was imported.\nGo to CignvsLab > Install to set up required dependencies.",
                            "OK"
                        );
                    }
                };
            }
        }

        private static readonly Dictionary<string, string> dependenciesToAdd = new Dictionary<string, string>
        {
            { "com.endel.nativewebsocket", "https://github.com/endel/NativeWebSocket.git#upm" },
            { "jillejr.newtonsoft.json-for-unity", "https://github.com/jilleJr/Newtonsoft.Json-for-Unity.git#upm" },
            { "org.cignvslab", "https://github.com/germanviscuso/DharanaServer.git" }
        };

        [MenuItem("CignvsLab/Install")]
        public static void InstallDependencies()
        {
            string manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");

            if (!File.Exists(manifestPath))
            {
                Debug.LogError("❌ manifest.json not found!");
                return;
            }

            string json = File.ReadAllText(manifestPath);
            bool changed = false;

            var match = Regex.Match(json, "\"dependencies\"\\s*:\\s*{([\\s\\S]*?)}");

            if (match.Success)
            {
                string dependenciesBlock = match.Groups[1].Value;
                foreach (var dep in dependenciesToAdd)
                {
                    if (!dependenciesBlock.Contains($"\"{dep.Key}\""))
                    {
                        dependenciesBlock = $"    \"{dep.Key}\": \"{dep.Value}\",\n{dependenciesBlock}";
                        Debug.Log($"📦 Added dependency: {dep.Key} → {dep.Value}");
                        changed = true;
                    }
                    else
                    {
                        Debug.Log($"✅ Dependency already exists: {dep.Key}");
                    }
                }

                json = Regex.Replace(json, "\"dependencies\"\\s*:\\s*{([\\s\\S]*?)}", $"\"dependencies\": {{\n{dependenciesBlock}\n}}");
            }
            else
            {
                Debug.LogError("❌ Could not locate 'dependencies' block in manifest.json!");
                return;
            }

            if (changed)
            {
                File.WriteAllText(manifestPath, json);
                AssetDatabase.Refresh();
                Debug.Log("✅ Dependencies added to manifest. Please restart Unity.");
            }
            else
            {
                Debug.Log("⚠️ All dependencies already present. No changes made.");
            }

            TrySelfDestruct(manifestPath);
            RestartWarning();
            // ForceUnityRestart();
        }

        private static void TrySelfDestruct(string manifestPath)
        {
            string scriptPath = GetSelfPath();

            if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
            {
                try
                {
                    File.Delete(scriptPath);
                    string metaPath = scriptPath + ".meta";
                    if (File.Exists(metaPath)) File.Delete(metaPath);
                    Debug.Log("🧹 Self-cleanup complete: Removed AddDependencies.cs after successful run.");
                }
                catch (IOException e)
                {
                    Debug.LogWarning($"⚠️ Could not delete AddDependencies.cs: {e.Message}");
                }
            }

            string manifestJson = File.ReadAllText(manifestPath);
            string pattern = "\\s*\"org\\.cignvslab\\.dependency-bootstrapper\"\\s*:\\s*\"[^\"]+\",?";
            string cleanedJson = Regex.Replace(manifestJson, pattern, "", RegexOptions.Multiline);

            if (!manifestJson.Equals(cleanedJson))
            {
                File.WriteAllText(manifestPath, cleanedJson);
                Debug.Log("🧹 Removed bootstrapper entry from manifest.json.");
                AssetDatabase.Refresh();
            }
        }

        private static string GetSelfPath()
        {
            string[] files = Directory.GetFiles(Application.dataPath, "AddDependencies.cs", SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : null;
        }

        private static void RestartWarning()
        {
            EditorUtility.DisplayDialog("Restart Unity", "Dependencies were added successfully. Restart Unity if Packages were not refreshed.", "OK");
        }
    }
}
