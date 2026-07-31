using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Norscope.Audio;
using Norscope.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Norscope.Editor
{
    // Controlled Settings Audio editor workflow; validation is read-only.
    public static class PhaseSettingsAudioConfigurator
    {
        private const string Menu = "Norscope/HoloLens/Settings/Audio/";
        private const string PanelPath = "Assets/Norscope/UI/Panels/SettingsPanel.prefab";
        private const string BootstrapPath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string MixerPath = "Assets/Norscope/Audio/NorscopeAudioMixer.mixer";
        private const string BackupRoot = "Assets/Norscope/UI/Panels/_Backups";
        private const string ApplyFlag = "Temp/NorscopeSettingsAudioApply.flag";
        private const string ValidateFlag = "Temp/NorscopeSettingsAudioValidate.flag";
        private const string InspectMixerFlag = "Temp/NorscopeSettingsAudioInspectMixer.flag";

        [InitializeOnLoadMethod]
        private static void RunApprovedQueuedCommand()
        {
            if (File.Exists(ApplyFlag))
            {
                File.Delete(ApplyFlag);
                EditorApplication.delayCall += Apply;
            }
            else if (File.Exists(ValidateFlag))
            {
                File.Delete(ValidateFlag);
                EditorApplication.delayCall += Validate;
            }
            else if (File.Exists(InspectMixerFlag))
            {
                File.Delete(InspectMixerFlag);
                EditorApplication.delayCall += LogMixerApi;
            }
        }

        private static void LogMixerApi()
        {
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
            string methods = string.Join("\n", type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name.IndexOf("expos", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.Name.IndexOf("param", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.Name.IndexOf("group", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.Name.IndexOf("child", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.Name.IndexOf("create", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(m => m.ToString()));
            Debug.Log("[Norscope Settings Audio API]\n" + methods);
        }

        [MenuItem(Menu + "1) Capture Baseline")]
        public static void CaptureBaseline()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folder = BackupRoot + "/SettingsAudio_" + stamp;
            Directory.CreateDirectory(folder);
            FileUtil.CopyFileOrDirectory(PanelPath, folder + "/SettingsPanel.prefab");
            FileUtil.CopyFileOrDirectory(BootstrapPath, folder + "/Bootstrap.unity");
            File.WriteAllText(folder + "/manifest.txt",
                "Settings Audio baseline " + DateTime.Now.ToString("O") + Environment.NewLine +
                PanelPath + Environment.NewLine + BootstrapPath + Environment.NewLine);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Settings Audio] Baseline captured: " + folder);
        }

        [MenuItem(Menu + "2) Dry Run")]
        public static void DryRun()
        {
            string report;
            bool ready = Inspect(out report);
            Debug.Log("[Norscope Settings Audio] Dry run " + (ready ? "READY\n" : "BLOCKED\n") + report);
        }

        [MenuItem(Menu + "3) Apply %#a")]
        public static void Apply()
        {
            string report;
            if (!Inspect(out report))
            {
                Debug.LogError("[Norscope Settings Audio] Apply blocked\n" + report);
                return;
            }

            EnsureBackupExists();
            AudioMixer mixer = CreateMixer();
            if (mixer == null)
            {
                Debug.LogError("[Norscope Settings Audio] Apply stopped: mixer creation failed. Prefab and scene were not modified.");
                return;
            }

            WirePanel();
            WireBootstrap(mixer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Settings Audio] Apply complete. Run 4) Validate.");
        }

        [MenuItem(Menu + "4) Validate %#v")]
        public static void Validate()
        {
            var lines = new List<string>();
            bool ok = true;
            Action<bool, string> check = (pass, label) =>
            {
                ok &= pass;
                lines.Add((pass ? "PASS  " : "FAIL  ") + label);
            };

            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            check(mixer != null, "NorscopeAudioMixer.mixer exists");
            if (mixer != null)
            {
                // FindMatchingGroups uses path substring match, so "Master" also matches Master/UI etc.
                // Require an exact group named Master (the true root), not Length == 1.
                check(System.Array.Exists(mixer.FindMatchingGroups("Master"), g => g != null && g.name == "Master"), "Mixer has Master group");
                check(mixer.FindMatchingGroups("UI").Length == 1, "Mixer has UI group");
                check(mixer.FindMatchingGroups("Alerts").Length == 1, "Mixer has Alerts group");
                check(mixer.FindMatchingGroups("Voice").Length == 1, "Mixer has reserved Voice group");
                // Edit-mode exposure check: GetFloat succeeds when the parameter is exposed.
                // SetFloat can return false outside Play Mode even for valid exposed parameters.
                check(mixer.GetFloat("MasterVolume", out _), "MasterVolume parameter is exposed");
            }
            check(AudioMixerVolumeUtility.NormalizedToDecibels(0f) == -80f, "Zero volume maps to -80 dB");
            check(float.IsFinite(AudioMixerVolumeUtility.NormalizedToDecibels(0f)) &&
                  float.IsFinite(AudioMixerVolumeUtility.NormalizedToDecibels(1f)), "Mixer conversion always returns finite values");
            check(UIAudioManager.IsAlertEvent(UIAudioEvent.Notification) &&
                  UIAudioManager.IsAlertEvent(UIAudioEvent.Success) &&
                  UIAudioManager.IsAlertEvent(UIAudioEvent.Warning) &&
                  UIAudioManager.IsAlertEvent(UIAudioEvent.Error), "Approved alert event classes are gated");
            check(!UIAudioManager.IsAlertEvent(UIAudioEvent.ButtonClick) &&
                  !UIAudioManager.IsAlertEvent(UIAudioEvent.PanelOpen) &&
                  !UIAudioManager.IsAlertEvent(UIAudioEvent.SliderChanged), "Ordinary UI events are not gated");

            ValidatePanel(check);
            ValidateBootstrap(check);
            UIAudioTheme themeAsset = AssetDatabase.LoadAssetAtPath<UIAudioTheme>("Assets/Norscope/Data/UIAudioTheme.asset");
            if (themeAsset != null)
            {
                lines.Add(themeAsset.GetClip(UIAudioEvent.HomeNavigate) != null
                    ? "INFO  HomeNavigate has an assigned clip."
                    : "INFO  HomeNavigate clip is unassigned.");
                lines.Add(themeAsset.GetClip(UIAudioEvent.ButtonClick) != null
                    ? "INFO  ButtonClick has an assigned clip."
                    : "INFO  ButtonClick clip is unassigned.");
            }
            else
            {
                lines.Add("INFO  Audible testing: UIAudioTheme.asset missing.");
            }
            Debug.Log("[Norscope Settings Audio] Validation " + (ok ? "PASSED\n" : "FAILED\n") + string.Join("\n", lines));
        }

        [MenuItem(Menu + "5) Restore Baseline")]
        public static void Restore()
        {
            string folder = LatestBackup();
            if (folder == null)
            {
                Debug.LogError("[Norscope Settings Audio] No baseline backup found.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Restore Settings Audio baseline?",
                    "This restores only SettingsPanel.prefab and Bootstrap.unity from:\n" + folder,
                    "Restore", "Cancel")) return;
            FileUtil.ReplaceFile(folder + "/SettingsPanel.prefab", PanelPath);
            FileUtil.ReplaceFile(folder + "/Bootstrap.unity", BootstrapPath);
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Settings Audio] Restored prefab and scene from " + folder + ". Runtime source and mixer assets were not deleted.");
        }

        private static bool Inspect(out string report)
        {
            var lines = new List<string>();
            bool ready = true;
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            ready &= root != null;
            lines.Add((root != null ? "PASS  " : "BLOCKER  ") + "SettingsPanel.prefab exists");
            if (root != null)
            {
                Transform audio = FindDescendant(root.transform, "AudioView");
                Transform master = audio != null ? FindDescendant(audio, "MasterVolumeCard") : null;
                Transform alerts = audio != null ? FindDescendant(audio, "AlertSoundsToggle") : null;
                ready &= audio != null && master != null && alerts != null;
                lines.Add((audio != null && master != null && alerts != null ? "PASS  " : "BLOCKER  ") + "Authored Audio hierarchy resolves");
                lines.Add("PLAN  Add one transparent SliderHitArea under MasterVolumeCard; no existing geometry changes");
                lines.Add("PLAN  Enable Master decrease/increase and Alert Sounds Button only");
                lines.Add("PLAN  Keep Voice Guidance volume/toggle and Microphone disabled");
            }
            lines.Add("PLAN  Create mixer groups Master/UI/Alerts/Voice and expose four volume parameters");
            lines.Add("PLAN  Route the existing UIAudioManager through UI and Alerts groups; no duplicate manager");
            lines.Add("PLAN  Modify only Settings audio controls and Bootstrap audio routing");
            report = string.Join("\n", lines);
            return ready;
        }

        private static AudioMixer CreateMixer()
        {
            AudioMixer existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            Assembly editorAssembly = typeof(EditorWindow).Assembly;
            Type controllerType = editorAssembly.GetType("UnityEditor.Audio.AudioMixerController");
            if (controllerType == null) return null;
            object controller = existing;
            if (controller == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MixerPath));
                MethodInfo create = controllerType.GetMethod("CreateMixerControllerAtPath",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                controller = create != null ? create.Invoke(null, new object[] { MixerPath }) : null;
            }
            if (controller == null) return null;

            object master = GetMember(controller, "masterGroup");
            AudioMixer currentMixer = (AudioMixer)controller;
            if (currentMixer.FindMatchingGroups("UI").Length == 0) CreateChildGroup(editorAssembly, controller, master, "UI");
            if (currentMixer.FindMatchingGroups("Alerts").Length == 0) CreateChildGroup(editorAssembly, controller, master, "Alerts");
            if (currentMixer.FindMatchingGroups("Voice").Length == 0) CreateChildGroup(editorAssembly, controller, master, "Voice");
            AssetDatabase.SaveAssets();
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) return null;

            float unused;
            if (!mixer.GetFloat("MasterVolume", out unused)) ExposeGroupVolume(controller, master, "MasterVolume");
            foreach (string groupName in new[] { "UI", "Alerts", "Voice" })
            {
                AudioMixerGroup group = mixer.FindMatchingGroups(groupName).FirstOrDefault();
                string parameter = groupName == "Alerts" ? "AlertVolume" : groupName + "Volume";
                if (group != null && !mixer.GetFloat(parameter, out unused)) ExposeGroupVolume(controller, group, parameter);
            }
            EditorUtility.SetDirty((UnityEngine.Object)controller);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        private static void CreateChildGroup(Assembly assembly, object controller, object parent, string name)
        {
            MethodInfo create = controller.GetType().GetMethod("CreateNewGroup",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(bool) }, null);
            if (create == null)
                throw new MissingMethodException("AudioMixerController.CreateNewGroup API not found");
            object child = create.Invoke(controller, new object[] { name, false });
            if (child == null)
                throw new InvalidOperationException("Unity failed to create AudioMixer group " + name);
            MethodInfo add = controller.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name.Contains("AddChildToParent") && m.GetParameters().Length == 2);
            if (add == null) throw new MissingMethodException("AudioMixerController AddChildToParent API not found");
            add.Invoke(controller, new[] { child, parent });
        }

        private static void ExposeGroupVolume(object controller, object group, string exposedName)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            Type controllerType = controller.GetType();
            Assembly asm = controllerType.Assembly;
            Type groupPathType = asm.GetType("UnityEditor.Audio.AudioGroupParameterPath");
            MethodInfo getVol = group.GetType().GetMethod("GetGUIDForVolume",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo add = controllerType.GetMethod("AddExposedParameter",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getVol == null || add == null || groupPathType == null)
                throw new MissingMethodException("AudioMixer expose API unavailable");

            object guid = getVol.Invoke(group, null);
            object path = Activator.CreateInstance(groupPathType, group, guid);
            add.Invoke(controller, new[] { path });

            // Unity auto-names as MyExposedParam*; rename to the required script name.
            PropertyInfo exposedProperty = controllerType.GetProperty("exposedParameters",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Array entries = exposedProperty.GetValue(controller) as Array;
            if (entries == null) return;

            Type entryType = entries.GetType().GetElementType();
            FieldInfo guidField = entryType.GetField("guid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo nameField = entryType.GetField("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo guidProperty = entryType.GetProperty("guid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo nameProperty = entryType.GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < entries.Length; i++)
            {
                object entry = entries.GetValue(i);
                object entryGuid = guidField != null ? guidField.GetValue(entry) : guidProperty.GetValue(entry);
                if (entryGuid == null || !entryGuid.Equals(guid)) continue;

                if (nameField != null) nameField.SetValue(entry, exposedName);
                else if (nameProperty != null && nameProperty.CanWrite) nameProperty.SetValue(entry, exposedName);
                entries.SetValue(entry, i); // struct write-back
                break;
            }

            exposedProperty.SetValue(controller, entries);
        }

        private static object GetMember(object instance, string name)
        {
            Type type = instance.GetType();
            PropertyInfo p = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null) return p.GetValue(instance);
            FieldInfo f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return f != null ? f.GetValue(instance) : null;
        }

        private static void WirePanel()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                Transform audio = FindDescendant(root.transform, "AudioView");
                Transform master = FindDescendant(audio, "MasterVolumeCard");
                Transform alerts = FindDescendant(audio, "AlertSoundsToggle");
                Button decrease = FindDescendant(master, "DecreaseButton").GetComponent<Button>();
                Button increase = FindDescendant(master, "IncreaseButton").GetComponent<Button>();
                TMP_Text value = FindDescendant(master, "Value").GetComponent<TMP_Text>();
                Transform track = FindDescendant(master, "Track");
                Button alertButton = alerts.GetComponent<Button>();
                Image alertTrack = FindDescendant(alerts, "ToggleTrack").GetComponent<Image>();
                TMP_Text alertStatus = FindDescendant(alerts, "UnavailableMessage").GetComponent<TMP_Text>();

                Transform hit = master.Find("SliderHitArea");
                if (hit == null)
                {
                    var go = new GameObject("SliderHitArea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
                    hit = go.transform;
                    hit.SetParent(master, false);
                    RectTransform rt = (RectTransform)hit;
                    RectTransform trackRt = (RectTransform)track;
                    rt.anchorMin = trackRt.anchorMin;
                    rt.anchorMax = trackRt.anchorMax;
                    rt.pivot = trackRt.pivot;
                    rt.anchoredPosition = new Vector2(trackRt.anchoredPosition.x, trackRt.anchoredPosition.y + 15f);
                    rt.sizeDelta = new Vector2(trackRt.sizeDelta.x, 36f);
                    Image transparent = go.GetComponent<Image>();
                    transparent.color = new Color(1f, 1f, 1f, 0f);
                    transparent.raycastTarget = true;
                    Slider slider = go.GetComponent<Slider>();
                    slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;
                    slider.direction = Slider.Direction.LeftToRight;
                    slider.targetGraphic = transparent;
                }
                Slider masterSlider = hit.GetComponent<Slider>();
                decrease.interactable = true;
                increase.interactable = true;
                alertButton.interactable = true;
                Transform unavailable = FindDescendant(master, "UnavailableMessage");
                if (unavailable != null) unavailable.gameObject.SetActive(false);

                SettingsAudioController controller = root.GetComponent<SettingsAudioController>();
                if (controller == null) controller = root.AddComponent<SettingsAudioController>();
                controller.Configure(masterSlider, decrease, increase, value, alertButton, alertTrack, alertStatus);
                RemoveDeferred(root.GetComponent<SettingsPanel>(), decrease, increase, alertButton);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void RemoveDeferred(SettingsPanel panel, params Selectable[] enabled)
        {
            SerializedObject so = new SerializedObject(panel);
            SerializedProperty array = so.FindProperty("deferredControls");
            var retained = new List<UnityEngine.Object>();
            for (int i = 0; i < array.arraySize; i++)
            {
                UnityEngine.Object item = array.GetArrayElementAtIndex(i).objectReferenceValue;
                if (item != null && !enabled.Contains(item as Selectable)) retained.Add(item);
            }
            array.arraySize = retained.Count;
            for (int i = 0; i < retained.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = retained[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBootstrap(AudioMixer mixer)
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            UIAudioManager[] managers = UnityEngine.Object.FindObjectsByType<UIAudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (managers.Length != 1) throw new InvalidOperationException("Expected exactly one UIAudioManager in Bootstrap");
            UIAudioManager manager = managers[0];
            AudioSource[] sources = manager.GetComponents<AudioSource>();
            AudioSource ui = sources.Length > 0 ? sources[0] : manager.gameObject.AddComponent<AudioSource>();
            AudioSource alerts = sources.Length > 1 ? sources[1] : manager.gameObject.AddComponent<AudioSource>();
            ui.playOnAwake = alerts.playOnAwake = false;
            ui.spatialBlend = alerts.spatialBlend = 0f;
            ui.outputAudioMixerGroup = mixer.FindMatchingGroups("UI").First();
            alerts.outputAudioMixerGroup = mixer.FindMatchingGroups("Alerts").First();
            manager.ConfigureAudioRouting(mixer, ui, alerts);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ValidatePanel(Action<bool, string> check)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            SettingsAudioController controller = root != null ? root.GetComponent<SettingsAudioController>() : null;
            check(controller != null, "SettingsPanel has SettingsAudioController");
            Transform audio = root != null ? FindDescendant(root.transform, "AudioView") : null;
            Transform master = audio != null ? FindDescendant(audio, "MasterVolumeCard") : null;
            Slider slider = master != null ? FindDescendant(master, "SliderHitArea")?.GetComponent<Slider>() : null;
            check(slider != null && slider.minValue == 0f && slider.maxValue == 1f, "Master slider uses the approved 0–1 range");
            check(master != null && FindDescendant(master, "DecreaseButton").GetComponent<Button>().interactable && FindDescendant(master, "IncreaseButton").GetComponent<Button>().interactable, "Master increase and decrease buttons are enabled");
            Transform alerts = audio != null ? FindDescendant(audio, "AlertSoundsToggle") : null;
            check(alerts != null && alerts.GetComponent<Button>().interactable, "Alert Sounds toggle is enabled");
            Transform voiceCard = audio != null ? FindDescendant(audio, "VoiceGuidanceCard") : null;
            Transform voiceToggle = audio != null ? FindDescendant(audio, "VoiceGuidanceToggle") : null;
            Transform mic = audio != null ? FindDescendant(audio, "MicrophoneToggle") : null;
            check(voiceCard != null && !FindDescendant(voiceCard, "DecreaseButton").GetComponent<Button>().interactable && !FindDescendant(voiceCard, "IncreaseButton").GetComponent<Button>().interactable && voiceToggle != null && !voiceToggle.GetComponent<Button>().interactable, "Voice Guidance controls remain disabled");
            check(mic != null && !mic.GetComponent<Button>().interactable, "Microphone remains disabled");
        }

        private static void ValidateBootstrap(Action<bool, string> check)
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
                UIAudioManager[] managers = UnityEngine.Object.FindObjectsByType<UIAudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                check(managers.Length == 1, "Exactly one UIAudioManager remains in Bootstrap");
                check(managers.Length == 1 && managers[0].GetComponents<AudioSource>().Length == 2, "Existing manager has separate UI and Alerts sources");
                NorscopeSettingsService[] services = UnityEngine.Object.FindObjectsByType<NorscopeSettingsService>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                check(services.Length == 1, "Exactly one NorscopeSettingsService remains in Bootstrap");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        private static void EnsureBackupExists()
        {
            if (LatestBackup() == null) CaptureBaseline();
        }

        private static string LatestBackup()
        {
            if (!Directory.Exists(BackupRoot)) return null;
            return Directory.GetDirectories(BackupRoot, "SettingsAudio_*").OrderByDescending(x => x).FirstOrDefault();
        }
    }
}
