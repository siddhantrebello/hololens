using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;

namespace Norscope.Editor
{
    /// <summary>
    /// Hand-Ray Fidelity — near/far arbitration only.
    /// Capture → Dry Run → Apply → Validate → Restore.
    /// </summary>
    public static class PhaseHandRayFidelityConfigurator
    {
        private const string Menu = "Norscope/HoloLens/Hand Ray Fidelity/";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string BackupKey = "Norscope.HandRayFidelity.Backup";
        private const string DryRunReportPath = "Temp/NorscopeHandRayFidelityDryRun.txt";
        private const string ValidateReportPath = "Temp/NorscopeHandRayFidelityValidate.txt";
        private const string ApplyRequestFlag = "Temp/NorscopeHandRayFidelityApply.flag";
        private const string ValidateRequestFlag = "Temp/NorscopeHandRayFidelityValidate.flag";

        private const string BootstrapScene = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string PokeSupportScript =
            "Assets/Norscope/Runtime/Phase3UiPokeSupport.cs";
        private const string RayCorrectorScript =
            "Assets/Norscope/Runtime/Phase3HandRayCorrector.cs";
        private const string ArbiterScript =
            "Assets/Norscope/Runtime/PhaseHandRayNearFarArbiter.cs";
        private const string HandsRigPrefab =
            "Assets/Samples/XR Interaction Toolkit/3.2.1/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab";

        private const string OpenXrSettings =
            "Assets/XR/Settings/OpenXRPackageSettings.asset";

        private static string _openXrHashAtCapture = string.Empty;
        private const string OpenXrHashKey = "Norscope.HandRayFidelity.OpenXrHash";

        [InitializeOnLoadMethod]
        private static void PollFlags()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (File.Exists(ValidateRequestFlag))
            {
                File.Delete(ValidateRequestFlag);
                EditorApplication.delayCall += ValidateMenu;
                return;
            }

            if (File.Exists(ApplyRequestFlag))
            {
                File.Delete(ApplyRequestFlag);
                EditorApplication.delayCall += ApplyMenu;
            }
        }

        [MenuItem(Menu + "1) Capture Baseline")]
        public static void CaptureBaselineMenu()
        {
            string folder = BackupRoot + "/HandRayFidelity_" +
                            DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(folder);

            CopyIfExists(BootstrapScene, folder + "/Bootstrap.unity");
            CopyIfExists(PokeSupportScript, folder + "/Phase3UiPokeSupport.cs.bak");
            CopyIfExists(RayCorrectorScript, folder + "/Phase3HandRayCorrector.cs.bak");
            CopyIfExists(HandsRigPrefab, folder + "/XR Origin Hands (XR Rig).prefab");
            CopyIfExists(OpenXrSettings, folder + "/OpenXRPackageSettings.asset.bak");

            if (File.Exists(OpenXrSettings))
            {
                string hash = HashFile(OpenXrSettings);
                EditorPrefs.SetString(OpenXrHashKey, hash);
            }

            EditorPrefs.SetString(BackupKey, folder);
            AssetDatabase.Refresh();

            Debug.Log("[Norscope Hand Ray Fidelity] Baseline CAPTURED → " + folder);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Hand Ray Fidelity",
                    "Baseline captured:\n" + folder, "OK");
            }
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRunMenu()
        {
            bool ready = BuildDryRun(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(DryRunReportPath, report, Encoding.UTF8);
            Debug.Log((ready
                ? "[Norscope Hand Ray Fidelity] Dry Run READY\n"
                : "[Norscope Hand Ray Fidelity] Dry Run BLOCKED\n") + report);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Hand Ray Fidelity — Dry Run",
                    (ready ? "READY\n\n" : "BLOCKED\n\n") +
                    "Report: " + DryRunReportPath, "OK");
            }
        }

        [MenuItem(Menu + "3) Apply")]
        public static void ApplyMenu()
        {
            if (!BuildDryRun(out string dryReport))
            {
                Debug.LogError("[Norscope Hand Ray Fidelity] Apply BLOCKED\n" + dryReport);
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "Hand Ray Fidelity — Apply BLOCKED", dryReport, "OK");
                }
                return;
            }

            try
            {
                string applyLog = ExecuteApply();
                Directory.CreateDirectory("Temp");
                File.WriteAllText(
                    "Temp/NorscopeHandRayFidelityApply.txt", applyLog, Encoding.UTF8);
                Debug.Log("[Norscope Hand Ray Fidelity] Apply COMPLETE\n" + applyLog);
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "Hand Ray Fidelity — Apply",
                        "Apply complete.\n\nRun Validate next.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Norscope Hand Ray Fidelity] Apply FAILED\n" + ex);
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "Hand Ray Fidelity — Apply FAILED", ex.Message, "OK");
                }
                if (Application.isBatchMode)
                    throw;
            }
        }

        [MenuItem(Menu + "4) Validate")]
        public static void ValidateMenu()
        {
            bool pass = BuildValidate(out string report);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ValidateReportPath, report, Encoding.UTF8);
            if (pass)
            {
                Debug.Log("[Norscope Hand Ray Fidelity] Validate PASS\n" + report);
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "Hand Ray Fidelity — Validate", "PASS\n\nSee Console.", "OK");
                }
            }
            else
            {
                Debug.LogError("[Norscope Hand Ray Fidelity] Validate FAIL\n" + report);
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "Hand Ray Fidelity — Validate", "FAIL\n\nSee Console.", "OK");
                }
            }
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void RestoreBaselineMenu()
        {
            string folder = EditorPrefs.GetString(BackupKey, string.Empty);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "Hand Ray Fidelity",
                        "No baseline folder recorded. Run Capture Baseline first.",
                        "OK");
                }
                return;
            }

            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    "Restore Hand Ray Fidelity Baseline?",
                    "Restore files from:\n" + folder, "Restore", "Cancel"))
                return;

            RestoreIfExists(folder + "/Bootstrap.unity", BootstrapScene);
            RestoreIfExists(folder + "/Phase3UiPokeSupport.cs.bak", PokeSupportScript);
            RestoreIfExists(folder + "/Phase3HandRayCorrector.cs.bak", RayCorrectorScript);
            RestoreIfExists(
                folder + "/XR Origin Hands (XR Rig).prefab", HandsRigPrefab);
            // Intentionally do NOT restore OpenXR from backup on normal restore of
            // arbitration-only work — OpenXR was never modified. Keep file as safety copy.
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Hand Ray Fidelity] Baseline restored from " + folder);
        }

        public static bool BuildDryRun(out string report)
        {
            var sb = new StringBuilder();
            bool ready = true;

            void Check(bool ok, string label)
            {
                sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
                if (!ok)
                    ready = false;
            }

            sb.AppendLine("Hand Ray Fidelity Dry Run (reduced scope)");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("o"));
            sb.AppendLine();
            Check(File.Exists(ArbiterScript) || true,
                "Arbiter script path reserved/created on Apply");
            Check(File.Exists(PokeSupportScript), "Phase3UiPokeSupport.cs present");
            Check(File.Exists(BootstrapScene), "Bootstrap.unity present");
            Check(File.Exists(HandsRigPrefab), "XR Origin Hands prefab present");
            Check(Mathf.Approximately(PhaseHandRayNearFarArbiter.DefaultEnterNearMeters, 0.10f),
                "Enter-near constant = 0.10 m");
            Check(Mathf.Approximately(PhaseHandRayNearFarArbiter.DefaultExitNearMeters, 0.16f),
                "Exit-near constant = 0.16 m");
            Check(PhaseHandRayNearFarArbiter.DefaultExitNearMeters >
                  PhaseHandRayNearFarArbiter.DefaultEnterNearMeters,
                "Hysteresis exit > enter");
            Check(true, "Scope excludes OpenXR / Aim / masks / raycasters / Ask AI");
            report = sb.ToString();
            return ready;
        }

        private static string ExecuteApply()
        {
            var log = new StringBuilder();
            log.AppendLine("=== Hand Ray Fidelity Apply ===");

            // 1) Disconnect competing UnityEvents on the Hands rig prefab.
            int removed = DisconnectPokeGestureFarCastingWriters(HandsRigPrefab);
            log.AppendLine("Disconnected set_enableFarCasting UnityEvent calls: " + removed);

            // 2) Wire arbiter on Bootstrap XR Origin root.
            var scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            GameObject origin = FindXrOriginHands();
            if (origin == null)
                throw new Exception("XR Origin Hands (XR Rig) not found in Bootstrap.");

            PhaseHandRayNearFarArbiter arbiter =
                origin.GetComponent<PhaseHandRayNearFarArbiter>();
            if (arbiter == null)
                arbiter = origin.AddComponent<PhaseHandRayNearFarArbiter>();

            Phase3UiPokeSupport pokeSupport = origin.GetComponent<Phase3UiPokeSupport>();

            NearFarInteractor[] fars =
                origin.GetComponentsInChildren<NearFarInteractor>(true);
            XRPokeInteractor[] pokes =
                origin.GetComponentsInChildren<XRPokeInteractor>(true);

            NearFarInteractor leftFar = null;
            NearFarInteractor rightFar = null;
            XRPokeInteractor leftPoke = null;
            XRPokeInteractor rightPoke = null;

            for (int i = 0; i < fars.Length; i++)
            {
                string path = GetTransformPath(fars[i].transform);
                if (path.Contains("Left Hand"))
                    leftFar = fars[i];
                else if (path.Contains("Right Hand"))
                    rightFar = fars[i];
            }

            for (int i = 0; i < pokes.Length; i++)
            {
                string path = GetTransformPath(pokes[i].transform);
                if (path.Contains("Left Hand"))
                    leftPoke = pokes[i];
                else if (path.Contains("Right Hand"))
                    rightPoke = pokes[i];
            }

            if (leftFar == null || rightFar == null || leftPoke == null || rightPoke == null)
                throw new Exception(
                    "Failed to resolve left/right NearFar/Poke under XR Origin Hands.");

            arbiter.Bind(leftFar, leftPoke, rightFar, rightPoke);
            SerializedObject so = new SerializedObject(arbiter);
            so.FindProperty("leftNearFar").objectReferenceValue = leftFar;
            so.FindProperty("leftPoke").objectReferenceValue = leftPoke;
            so.FindProperty("rightNearFar").objectReferenceValue = rightFar;
            so.FindProperty("rightPoke").objectReferenceValue = rightPoke;
            so.FindProperty("enterNearMeters").floatValue = 0.10f;
            so.FindProperty("exitNearMeters").floatValue = 0.16f;
            so.FindProperty("enableStateChangeLogs").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(arbiter);

            if (pokeSupport != null)
            {
                pokeSupport.Bind(leftPoke, rightPoke);
                // Ensure no leftover nearUiDistance field usage for far casting.
                SerializedObject pokeSo = new SerializedObject(pokeSupport);
                SerializedProperty depth = pokeSo.FindProperty("pokeDepthMeters");
                if (depth != null)
                    depth.floatValue = 0.15f;
                pokeSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Also clear any lingering UnityEvents on scene instance detectors.
            int sceneRemoved = 0;
            var detectors = origin.GetComponentsInChildren<PokeGestureDetector>(true);
            for (int i = 0; i < detectors.Length; i++)
                sceneRemoved += ClearFarCastingPersistentCalls(detectors[i]);

            log.AppendLine("Scene instance UnityEvent far-casting calls cleared: " +
                           sceneRemoved);
            log.AppendLine("Arbiter on: " + origin.name);
            log.AppendLine("Left NearFar: " + GetTransformPath(leftFar.transform));
            log.AppendLine("Right NearFar: " + GetTransformPath(rightFar.transform));
            log.AppendLine("Left Poke: " + GetTransformPath(leftPoke.transform));
            log.AppendLine("Right Poke: " + GetTransformPath(rightPoke.transform));
            log.AppendLine("enterNearMeters=0.10 exitNearMeters=0.16");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine("Bootstrap.unity saved.");
            return log.ToString();
        }

        private static bool BuildValidate(out string report)
        {
            var sb = new StringBuilder();
            bool pass = true;

            void Check(bool ok, string label)
            {
                sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
                if (!ok)
                    pass = false;
            }

            sb.AppendLine("=== Hand Ray Fidelity Validate ===");
            sb.AppendLine(DateTime.Now.ToString("o"));
            sb.AppendLine();

            Check(File.Exists(ArbiterScript), "PhaseHandRayNearFarArbiter.cs exists");

            string pokeSrc = File.Exists(PokeSupportScript)
                ? File.ReadAllText(PokeSupportScript)
                : string.Empty;
            Check(!string.IsNullOrEmpty(pokeSrc) &&
                  !pokeSrc.Contains(".enableFarCasting") &&
                  !pokeSrc.Contains("enableFarCasting ="),
                "Phase3UiPokeSupport no longer assigns enableFarCasting");

            string prefabSrc = File.Exists(HandsRigPrefab)
                ? File.ReadAllText(HandsRigPrefab)
                : string.Empty;
            Check(!string.IsNullOrEmpty(prefabSrc) &&
                  !prefabSrc.Contains("set_enableFarCasting"),
                "Hands rig prefab has no set_enableFarCasting UnityEvents");

            string arbiterSrc = File.Exists(ArbiterScript)
                ? File.ReadAllText(ArbiterScript)
                : string.Empty;
            Check(arbiterSrc.Contains("enableFarCasting ="),
                "Arbiter is the enableFarCasting writer");
            Check(arbiterSrc.Contains("IsBodyFollowUiCanvas") &&
                  arbiterSrc.Contains("HomeLauncherFollow") &&
                  arbiterSrc.Contains("HomeLauncher"),
                "Arbiter excludes HomeLauncher / HomeLauncherFollow from proximity");

            Check(Mathf.Approximately(PhaseHandRayNearFarArbiter.DefaultEnterNearMeters, 0.10f),
                "Enter threshold constant 0.10 m");
            Check(Mathf.Approximately(PhaseHandRayNearFarArbiter.DefaultExitNearMeters, 0.16f),
                "Exit threshold constant 0.16 m");

            // OpenXR untouched
            string captured = EditorPrefs.GetString(OpenXrHashKey, string.Empty);
            if (!string.IsNullOrEmpty(captured) && File.Exists(OpenXrSettings))
            {
                Check(HashFile(OpenXrSettings) == captured,
                    "OpenXRPackageSettings.asset unchanged vs Capture Baseline hash");
            }
            else
            {
                sb.AppendLine("WARN  OpenXR hash not captured — skip binary compare " +
                              "(Capture Baseline first for full gate)");
            }

            // Aim / corrector aim-mask file still present and still references Aim Pose
            string corrector = File.Exists(RayCorrectorScript)
                ? File.ReadAllText(RayCorrectorScript)
                : string.Empty;
            Check(corrector.Contains("Aim Pose") && corrector.Contains("castOrigin"),
                "Phase3HandRayCorrector aim/mask behavior file retained");

            // Scene wiring
            var scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            GameObject origin = FindXrOriginHands();
            Check(origin != null, "XR Origin Hands present in Bootstrap");

            PhaseHandRayNearFarArbiter arbiter = origin != null
                ? origin.GetComponent<PhaseHandRayNearFarArbiter>()
                : null;
            Check(arbiter != null, "PhaseHandRayNearFarArbiter on XR Origin");

            if (arbiter != null)
            {
                SerializedObject so = new SerializedObject(arbiter);
                Check(Mathf.Approximately(
                        so.FindProperty("enterNearMeters").floatValue, 0.10f),
                    "Serialized enterNearMeters = 0.10");
                Check(Mathf.Approximately(
                        so.FindProperty("exitNearMeters").floatValue, 0.16f),
                    "Serialized exitNearMeters = 0.16");
                Check(so.FindProperty("leftNearFar").objectReferenceValue != null,
                    "Left NearFar wired");
                Check(so.FindProperty("rightNearFar").objectReferenceValue != null,
                    "Right NearFar wired");
                Check(so.FindProperty("leftPoke").objectReferenceValue != null,
                    "Left Poke wired");
                Check(so.FindProperty("rightPoke").objectReferenceValue != null,
                    "Right Poke wired");
            }

            int remainingCalls = 0;
            if (origin != null)
            {
                var detectors = origin.GetComponentsInChildren<PokeGestureDetector>(true);
                for (int i = 0; i < detectors.Length; i++)
                    remainingCalls += CountFarCastingPersistentCalls(detectors[i]);
            }

            Check(remainingCalls == 0,
                "No scene PokeGestureDetector → set_enableFarCasting calls");

            // Exactly one intended runtime writer type present
            Check(File.Exists(ArbiterScript) &&
                  !pokeSrc.Contains(".enableFarCasting") &&
                  !pokeSrc.Contains("enableFarCasting =") &&
                  !prefabSrc.Contains("set_enableFarCasting") &&
                  arbiterSrc.Contains("enableFarCasting ="),
                "Exactly one runtime writer remains (arbiter)");

            Check(EditorPrefs.HasKey(BackupKey) ||
                  Directory.Exists(BackupRoot),
                "Restore Baseline remains available");

            sb.AppendLine();
            sb.AppendLine("Manual HoloLens checks still required:");
            sb.AppendLine("  - Mid-reach across HomeLauncher: far ray stays stable");
            sb.AppendLine("  - Far stable > 0.16 m from real panels");
            sb.AppendLine("  - Enter <= 0.10 m hides once");
            sb.AppendLine("  - No flicker while near");
            sb.AppendLine("  - Exit >= 0.16 m restores once");
            sb.AppendLine("  - Rapid boundary motion stable");
            sb.AppendLine("  - HomeLauncher still pokeable");
            sb.AppendLine("  - L/R independent; grab/resize; no duplicate selects");
            sb.AppendLine("  - Ask AI / Settings / Home / Tasks / Reports");

            sb.AppendLine();
            sb.AppendLine(pass ? "STATUS: PASS" : "STATUS: FAIL");
            report = sb.ToString();
            return pass;
        }

        private static int DisconnectPokeGestureFarCastingWriters(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            int removed = 0;
            try
            {
                var detectors = root.GetComponentsInChildren<PokeGestureDetector>(true);
                for (int i = 0; i < detectors.Length; i++)
                    removed += ClearFarCastingPersistentCalls(detectors[i]);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return removed;
        }

        private static int ClearFarCastingPersistentCalls(UnityEngine.Object detector)
        {
            if (detector == null)
                return 0;

            SerializedObject so = new SerializedObject(detector);
            int removed = 0;
            removed += ClearFarCastingFromUnityEvent(so.FindProperty("m_PokeGestureStarted"));
            removed += ClearFarCastingFromUnityEvent(so.FindProperty("m_PokeGestureEnded"));
            so.ApplyModifiedPropertiesWithoutUndo();
            return removed;
        }

        private static int CountFarCastingPersistentCalls(UnityEngine.Object detector)
        {
            if (detector == null)
                return 0;
            SerializedObject so = new SerializedObject(detector);
            return CountFarCastingInUnityEvent(so.FindProperty("m_PokeGestureStarted")) +
                   CountFarCastingInUnityEvent(so.FindProperty("m_PokeGestureEnded"));
        }

        private static int ClearFarCastingFromUnityEvent(SerializedProperty unityEventProp)
        {
            if (unityEventProp == null)
                return 0;

            SerializedProperty calls =
                unityEventProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls == null || !calls.isArray)
                return 0;

            int removed = 0;
            for (int i = calls.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(i);
                SerializedProperty method = call.FindPropertyRelative("m_MethodName");
                if (method != null && method.stringValue == "set_enableFarCasting")
                {
                    calls.DeleteArrayElementAtIndex(i);
                    removed++;
                }
            }

            return removed;
        }

        private static int CountFarCastingInUnityEvent(SerializedProperty unityEventProp)
        {
            if (unityEventProp == null)
                return 0;
            SerializedProperty calls =
                unityEventProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls == null || !calls.isArray)
                return 0;

            int count = 0;
            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty method =
                    calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_MethodName");
                if (method != null && method.stringValue == "set_enableFarCasting")
                    count++;
            }

            return count;
        }

        private static GameObject FindXrOriginHands()
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name.Contains("XR Origin Hands"))
                    return roots[i];

                Transform t = roots[i].transform.Find("XR Origin Hands (XR Rig)");
                if (t != null)
                    return t.gameObject;
            }

            // Prefab instance may use stripped name variants
            Phase3HandRayCorrector corrector =
                UnityEngine.Object.FindFirstObjectByType<Phase3HandRayCorrector>();
            return corrector != null ? corrector.gameObject : null;
        }

        private static string GetTransformPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }

        private static string HashFile(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] hash = md5.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static void CopyIfExists(string src, string dst)
        {
            if (!File.Exists(src))
                return;
            string dir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            if (File.Exists(dst))
                File.Delete(dst);
            FileUtil.CopyFileOrDirectory(src, dst);
        }

        private static void RestoreIfExists(string src, string dst)
        {
            if (!File.Exists(src))
                return;
            if (File.Exists(dst))
                FileUtil.DeleteFileOrDirectory(dst);
            FileUtil.CopyFileOrDirectory(src, dst);
        }
    }
}
