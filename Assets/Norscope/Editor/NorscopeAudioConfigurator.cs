using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Norscope.Audio;

namespace Norscope.Editor
{
    /// <summary>
    /// Cross-cutting UI audio infrastructure — not tied to one phase/panel. Same non-destructive
    /// pattern as Phase 5/6: Create If Missing never overwrites an existing UIAudioTheme asset
    /// (so assigned clips are never lost), Wire Scene only ensures exactly one UIAudioManager
    /// exists in Bootstrap and points at the theme.
    /// </summary>
    public static class NorscopeAudioConfigurator
    {
        private const string BootstrapScenePath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string AudioThemePath = "Assets/Norscope/Data/UIAudioTheme.asset";
        private const string AudioDataDir = "Assets/Norscope/Data";
        private const string ManagerObjectName = "UIAudioManager";

        [MenuItem("Norscope/HoloLens/Audio/1) Create UI Audio Theme If Missing")]
        public static void CreateAudioThemeIfMissingMenu()
        {
            CreateAudioThemeIfMissing();
        }

        public static bool CreateAudioThemeIfMissing()
        {
            if (File.Exists(AudioThemePath))
            {
                Debug.Log("[Norscope Audio] UIAudioTheme.asset already exists, left untouched.");
                return false;
            }

            Directory.CreateDirectory(AudioDataDir);
            UIAudioTheme theme = ScriptableObject.CreateInstance<UIAudioTheme>();
            AssetDatabase.CreateAsset(theme, AudioThemePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Audio] Created " + AudioThemePath + " — every clip slot empty by design.");
            return true;
        }

        [MenuItem("Norscope/HoloLens/Audio/2) Wire UI Audio Scene")]
        public static void WireAudioSceneMenu()
        {
            WireAudioScene();
        }

        public static void WireAudioScene()
        {
            CreateAudioThemeIfMissing();

            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            UIAudioTheme theme = AssetDatabase.LoadAssetAtPath<UIAudioTheme>(AudioThemePath);

            UIAudioManager[] existing = Object.FindObjectsByType<UIAudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject managerGo;
            if (existing.Length == 0)
            {
                managerGo = new GameObject(ManagerObjectName);
                managerGo.AddComponent<UIAudioManager>();
            }
            else
            {
                // Keep exactly one; remove any accidental duplicates rather than leaving them
                // for the singleton's own Awake()-time destroy to sort out at runtime.
                managerGo = existing[0].gameObject;
                for (int i = 1; i < existing.Length; i++)
                {
                    Object.DestroyImmediate(existing[i].gameObject);
                }
            }

            UIAudioManager manager = managerGo.GetComponent<UIAudioManager>();
            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Norscope Audio] Wire UI Audio Scene complete — UIAudioManager present, theme assigned.");
        }

        [MenuItem("Norscope/HoloLens/Audio/3) Validate UI Audio")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log(ok
                ? "[Norscope Audio] Validate UI Audio PASSED\n" + report
                : "[Norscope Audio] Validate UI Audio FAILED\n" + report);
            EditorUtility.DisplayDialog(
                "Validate UI Audio",
                (ok ? "PASSED\n\n" : "FAILED\n\n") + report,
                "OK");
        }

        public static bool Validate(out string report)
        {
            var sb = new StringBuilder();
            int fails = 0;

            void Check(bool ok, string label)
            {
                sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
                if (!ok) fails++;
            }

            Check(File.Exists(AudioThemePath), "UIAudioTheme.asset exists");
            UIAudioTheme theme = AssetDatabase.LoadAssetAtPath<UIAudioTheme>(AudioThemePath);

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            UIAudioManager[] managers = Object.FindObjectsByType<UIAudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(managers.Length == 1, "Exactly one UIAudioManager in Bootstrap (found " + managers.Length + ")");

            if (managers.Length == 1)
            {
                SerializedObject so = new SerializedObject(managers[0]);
                Check(so.FindProperty("theme").objectReferenceValue != null, "UIAudioManager has a theme assigned");
            }

            Check(EmptyThemePlaysSafely(), "Play() with every clip slot empty produces no exception");

            // Empirically prove Create/Wire never overwrites an existing theme asset.
            string before = ComputeFileHash(AudioThemePath);
            CreateAudioThemeIfMissing();
            string after = ComputeFileHash(AudioThemePath);
            Check(before == after, "Normal Create/Wire does not overwrite an existing UIAudioTheme");

            report = sb.ToString();
            return fails == 0;
        }

        /// <summary>Instantiates a throwaway UIAudioManager against a scratch, always-empty
        /// UIAudioTheme (not the user's real asset — this must keep passing forever, even
        /// after real clips are assigned to the real theme) and calls PlayOnThisInstance() for
        /// every event. Uses the instance method directly, not the static Play(), so this is
        /// independent of whatever UIAudioManager the scene's own singleton currently is.</summary>
        private static bool EmptyThemePlaysSafely()
        {
            UIAudioTheme scratchTheme = ScriptableObject.CreateInstance<UIAudioTheme>();
            GameObject probe = new GameObject("UIAudioProbe_Temp") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                probe.AddComponent<AudioSource>();
                UIAudioManager probeManager = probe.AddComponent<UIAudioManager>();
                probeManager.SetTheme(scratchTheme);
                foreach (UIAudioEvent evt in System.Enum.GetValues(typeof(UIAudioEvent)))
                {
                    probeManager.PlayOnThisInstance(evt);
                }

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                Object.DestroyImmediate(probe);
                Object.DestroyImmediate(scratchTheme);
            }
        }

        private static string ComputeFileHash(string assetPath)
        {
            if (!File.Exists(assetPath))
            {
                return "MISSING";
            }

            byte[] bytes = File.ReadAllBytes(assetPath);
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            {
                return System.Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }
    }
}
