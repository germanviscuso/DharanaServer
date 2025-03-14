using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics; // <- for Process
using Debug = UnityEngine.Debug;

namespace CignvsLab.Editor
{
    public static class AddDependencies
    {
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
                Debug.Log("✅ Dependencies added to manifest. Unity will now restart to apply changes.");
            }
            else
            {
                Debug.Log("⚠️ All dependencies already present. No changes made.");
            }

            TrySelfDestruct(manifestPath);
            ForceUnityRestart();
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

        private static void ForceUnityRestart()
        {
            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string unityPath = GetUnityEditorExecutablePath();

            if (string.IsNullOrEmpty(unityPath))
            {
                Debug.LogWarning("⚠️ Unity executable path not found. Please restart manually.");
                EditorUtility.DisplayDialog("Restart Required", "Dependencies installed. Please restart Unity manually.", "OK");
                return;
            }

            EditorUtility.DisplayDialog("Restarting Unity", "Dependencies were added successfully. Unity will now restart automatically.", "OK");

            var startInfo = new ProcessStartInfo
            {
                FileName = unityPath,
                Arguments = $"-projectPath \"{projectPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                KillAutoQuitter(); 
                Process.Start(startInfo);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to restart Unity: {e.Message}");
            }
        }

        private static void KillAutoQuitter()
        {
            #if UNITY_EDITOR_OSX
                    var processList = Process.GetProcessesByName("UnityAutoQuitter");
                    foreach (var proc in processList)
                    {
                        try
                        {
                            proc.Kill();
                            Debug.Log($"🛑 Killed lingering UnityAutoQuitter process (PID: {proc.Id})");
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"⚠️ Could not kill UnityAutoQuitter (PID: {proc.Id}): {ex.Message}");
                        }
                    }
            #endif
        }

        private static string GetUnityEditorExecutablePath()
        {
#if UNITY_EDITOR_OSX
            return $"/Applications/Unity/Hub/Editor/{Application.unityVersion}/Unity.app/Contents/MacOS/Unity";
#elif UNITY_EDITOR_WIN
            string path = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), $"Unity\\Hub\\Editor\\{Application.unityVersion}\\Editor\\Unity.exe");
            if (File.Exists(path)) return path;

            string fallback = $"C:\\Program Files\\Unity\\Hub\\Editor\\{Application.unityVersion}\\Editor\\Unity.exe";
            return File.Exists(fallback) ? fallback : null;
#else
            return null;
#endif
        }
    }
}
