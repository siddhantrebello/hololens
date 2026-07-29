using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Norscope.Navigation;
using Norscope.TaskLibrary;
using Norscope.UI;
using Norscope.Windows;

namespace Norscope.Editor
{
    /// <summary>
    /// Phase 6: Task Library panel only. Does not build AI, Reports, Settings, Storage, About.
    /// Every visual property of TaskCard.prefab and TaskLibraryPanel.prefab — position, size,
    /// spacing, color, font — is a real, Inspector-edited value baked in once at build time;
    /// nothing is computed at runtime. Normal Create/Wire never overwrites an existing prefab —
    /// only the explicit destructive rebuild commands do, each with its own confirmation dialog
    /// and timestamped backup.
    /// </summary>
    public static class Phase6TaskLibraryConfigurator
    {
        private const string BootstrapScenePath = "Assets/Norscope/Scenes/Bootstrap.unity";
        private const string ThemePath = "Assets/Norscope/UI/DesignSystem/Themes/NorscopeTheme.asset";
        private const string WorldConfigPath = "Assets/Norscope/UI/DesignSystem/Themes/NorscopeWorldSpaceUiConfig.asset";
        private const string RoundedSpritePath = "Assets/Norscope/UI/DesignSystem/Sprites/UIRounded_8.png";
        private const string TaskIconPath = "Assets/Norscope/UI/DesignSystem/Sprites/IconTasks.png";
        private const string CloseButtonPath = "Assets/Norscope/UI/Controls/NorscopeCloseButton.prefab";
        private const string TaskLibraryPanelPath = "Assets/Norscope/UI/Panels/TaskLibraryPanel.prefab";
        private const string TaskCardPrefabPath = "Assets/Norscope/UI/Controls/TaskCard.prefab";
        private const string PrefabBackupDir = "Assets/Norscope/UI/Panels/_Backups";
        private const string TaskDataDir = "Assets/Norscope/Data/Tasks";
        private const string TaskLibraryDatabasePath = "Assets/Norscope/Data/TaskLibraryDatabase.asset";
        private const string TaskLibraryPanelSourcePath = "Assets/Norscope/Runtime/TaskLibrary/TaskLibraryPanel.cs";
        private const string TaskCardViewSourcePath = "Assets/Norscope/Runtime/TaskLibrary/TaskCardView.cs";

        private struct StepSeed
        {
            public string title;
            public string instruction;
            public string warning;
            public string verification;

            public StepSeed(string title, string instruction, string warning = "", string verification = "")
            {
                this.title = title;
                this.instruction = instruction;
                this.warning = warning;
                this.verification = verification;
            }
        }

        private struct TaskSeed
        {
            public string id;
            public string displayName;
            public string description;
            public string duration;
            public TaskDifficulty difficulty;
            public StepSeed[] steps;
        }

        private static readonly TaskSeed[] InitialTaskSeed =
        {
            new TaskSeed
            {
                id = "clean-laptop-fan", displayName = "Clean Laptop Fan",
                description = "Remove dust buildup from the cooling fan and vents.",
                duration = "20 min", difficulty = TaskDifficulty.Beginner,
                steps = new[]
                {
                    new StepSeed("Power Down and Unplug", "Shut down the laptop completely and disconnect the AC adapter.",
                        warning: "Never open the case while the laptop is powered or plugged in."),
                    new StepSeed("Remove the Back Panel", "Unscrew and lift off the bottom access panel to expose the cooling fan."),
                    new StepSeed("Loosen Dust with Compressed Air", "Hold the fan blades still with a toothpick and use short bursts of compressed air to dislodge dust.",
                        warning: "Do not let the fan spin freely under air pressure — this can generate damaging voltage."),
                    new StepSeed("Wipe Down the Vents", "Use a dry microfiber cloth to remove loosened dust from the vent openings."),
                    new StepSeed("Reassemble and Test", "Replace the back panel, power on the laptop, and confirm the fan spins freely and quietly.",
                        verification: "Fan noise should be noticeably reduced and airflow from the vent should feel stronger.")
                }
            },
            new TaskSeed
            {
                id = "replace-cooling-fan", displayName = "Replace Cooling Fan",
                description = "Remove the worn fan and install a replacement.",
                duration = "45 min", difficulty = TaskDifficulty.Intermediate,
                steps = new[]
                {
                    new StepSeed("Power Down and Discharge", "Shut down, unplug, and remove the battery if possible. Hold the power button for 10 seconds to discharge residual power.",
                        warning: "Static discharge can damage internal components — use an anti-static wrist strap if available."),
                    new StepSeed("Access the Fan Assembly", "Remove the back panel and any brackets covering the fan and heatsink."),
                    new StepSeed("Disconnect the Fan Cable", "Gently unplug the fan's connector from the motherboard.",
                        warning: "Pull the connector housing, not the wires, to avoid damaging the cable."),
                    new StepSeed("Remove and Replace the Fan", "Unscrew the old fan and lift it out. Install the replacement fan in the same orientation and secure the screws."),
                    new StepSeed("Reconnect and Reassemble", "Plug the new fan's cable back in, replace covers, and power on.",
                        verification: "Fan should spin immediately at startup with no rattling or grinding noise.")
                }
            },
            new TaskSeed
            {
                id = "replace-battery", displayName = "Replace Battery",
                description = "Remove the old battery and install a new one.",
                duration = "15 min", difficulty = TaskDifficulty.Beginner,
                steps = new[]
                {
                    new StepSeed("Power Down Fully", "Shut down the laptop and unplug the charger."),
                    new StepSeed("Remove the Back Panel", "Unscrew and remove the bottom access panel."),
                    new StepSeed("Disconnect the Old Battery", "Unclip the battery connector and remove the retaining screws.",
                        warning: "If the battery is swollen, handle it carefully and do not puncture it."),
                    new StepSeed("Install the New Battery", "Place the new battery in the same orientation, secure the screws, and reconnect the connector."),
                    new StepSeed("Test the New Battery", "Reassemble the panel, power on, and confirm the OS detects the battery and it begins charging.",
                        verification: "Battery icon should show a charging state and report an accurate charge percentage.")
                }
            },
            new TaskSeed
            {
                id = "upgrade-ssd", displayName = "Upgrade SSD",
                description = "Replace the storage drive with a higher-capacity SSD.",
                duration = "30 min", difficulty = TaskDifficulty.Intermediate,
                steps = new[]
                {
                    new StepSeed("Back Up Your Data", "Back up all important files or create a full disk image before starting.",
                        warning: "This procedure will not preserve existing data on the drive unless you clone it first."),
                    new StepSeed("Power Down and Open the Case", "Shut down, unplug, and remove the back panel to access the storage bay."),
                    new StepSeed("Remove the Old Drive", "Unscrew and gently slide out the existing SSD or HDD from its slot or cable."),
                    new StepSeed("Install the New SSD", "Insert the new SSD into the same slot at a slight angle, then press down and secure the mounting screw.",
                        warning: "Ensure the connector is fully seated before screwing down — a loose connection can prevent boot."),
                    new StepSeed("Boot and Initialize", "Power on, enter BIOS to confirm the drive is detected, then install or restore the operating system.",
                        verification: "New SSD should appear in BIOS/OS disk management with its full rated capacity.")
                }
            },
            new TaskSeed
            {
                id = "replace-ram", displayName = "Replace RAM",
                description = "Swap an existing memory module for a new one.",
                duration = "10 min", difficulty = TaskDifficulty.Beginner,
                steps = new[]
                {
                    new StepSeed("Power Down and Ground Yourself", "Shut down, unplug, and touch a grounded metal surface to discharge static.",
                        warning: "Static electricity can silently damage memory modules."),
                    new StepSeed("Access the RAM Slots", "Remove the back panel or dedicated memory cover."),
                    new StepSeed("Remove the Old Module", "Spread the retaining clips outward until the module pops up, then slide it out at an angle."),
                    new StepSeed("Insert the New Module", "Align the notch on the new module with the slot, insert at an angle, and press down firmly until the clips lock.",
                        warning: "Uneven pressure can crack the module or the slot — press evenly on both ends."),
                    new StepSeed("Verify Detection", "Power on and check the system information or BIOS to confirm the new capacity is recognized.",
                        verification: "Total installed memory shown in the OS should match the new module's rated capacity.")
                }
            },
            new TaskSeed
            {
                id = "clean-keyboard", displayName = "Clean Keyboard",
                description = "Remove debris and sanitize the keys.",
                duration = "15 min", difficulty = TaskDifficulty.Beginner,
                steps = new[]
                {
                    new StepSeed("Power Down", "Shut down the laptop and unplug it before cleaning."),
                    new StepSeed("Remove Loose Debris", "Turn the laptop upside down and gently shake, or use compressed air between the keys to dislodge crumbs and dust."),
                    new StepSeed("Clean the Keycaps", "Lightly dampen a microfiber cloth with isopropyl alcohol and wipe each keycap.",
                        warning: "Never spray liquid directly onto the keyboard — apply it to the cloth first."),
                    new StepSeed("Clean Between the Keys", "Use a soft brush or cotton swab to clean around the edges of each key."),
                    new StepSeed("Let Dry and Test", "Allow the keyboard to air dry fully before powering on, then test that every key registers correctly.",
                        verification: "Every key should register once in a text field with no sticking or double inputs.")
                }
            }
        };

        // ---------------------------------------------------------------
        // 1) Create Task Library Assets If Missing
        // ---------------------------------------------------------------

        [MenuItem("Norscope/HoloLens/Phase 6/1) Create Task Library Prefabs If Missing")]
        public static void CreateTaskLibraryPrefabsIfMissingMenu()
        {
            CreateTaskLibraryPrefabsIfMissing();
        }

        public static void CreateTaskLibraryPrefabsIfMissing()
        {
            bool dataCreated = CreateTaskDataAssetsIfMissing();
            bool cardCreated = CreateTaskCardPrefabIfMissing();
            bool panelCreated = CreateTaskLibraryPanelPrefabIfMissing();

            if (dataCreated || cardCreated || panelCreated)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log("[Norscope Phase6] Create Task Library Prefabs If Missing — Task data: " +
                      (dataCreated ? "created" : "already existed, left untouched") +
                      "; TaskCard.prefab: " + (cardCreated ? "created" : "already existed, left untouched") +
                      "; TaskLibraryPanel: " + (panelCreated ? "created" : "already existed, left untouched") + ".");
        }

        /// <summary>Creates each TaskDefinition asset only if missing (per-asset check), and the
        /// TaskLibraryDatabase only if missing. Never touches the database's task list or any
        /// task's field values once they already exist — that content is the user's to edit.</summary>
        private static bool CreateTaskDataAssetsIfMissing()
        {
            Directory.CreateDirectory(TaskDataDir);
            Sprite defaultIcon = AssetDatabase.LoadAssetAtPath<Sprite>(TaskIconPath);
            bool anyCreated = false;

            var definitions = new TaskDefinition[InitialTaskSeed.Length];
            for (int i = 0; i < InitialTaskSeed.Length; i++)
            {
                TaskSeed seed = InitialTaskSeed[i];
                string assetPath = TaskDataDir + "/" + SanitizeFileName(seed.displayName) + ".asset";
                TaskDefinition existing = AssetDatabase.LoadAssetAtPath<TaskDefinition>(assetPath);
                if (existing != null)
                {
                    definitions[i] = existing;
                    continue;
                }

                TaskDefinition def = ScriptableObject.CreateInstance<TaskDefinition>();
                SerializedObject so = new SerializedObject(def);
                so.FindProperty("taskId").stringValue = seed.id;
                so.FindProperty("displayName").stringValue = seed.displayName;
                so.FindProperty("description").stringValue = seed.description;
                so.FindProperty("icon").objectReferenceValue = defaultIcon;
                so.FindProperty("estimatedDuration").stringValue = seed.duration;
                so.FindProperty("difficulty").enumValueIndex = (int)seed.difficulty;
                so.FindProperty("isEnabled").boolValue = true;

                SerializedProperty stepsProp = so.FindProperty("steps");
                stepsProp.arraySize = seed.steps.Length;
                for (int s = 0; s < seed.steps.Length; s++)
                {
                    StepSeed step = seed.steps[s];
                    SerializedProperty stepProp = stepsProp.GetArrayElementAtIndex(s);
                    stepProp.FindPropertyRelative("stepTitle").stringValue = step.title;
                    stepProp.FindPropertyRelative("instruction").stringValue = step.instruction;
                    stepProp.FindPropertyRelative("image").objectReferenceValue = null;
                    stepProp.FindPropertyRelative("warning").stringValue = step.warning;
                    stepProp.FindPropertyRelative("verification").stringValue = step.verification;
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                AssetDatabase.CreateAsset(def, assetPath);
                definitions[i] = def;
                anyCreated = true;
            }

            if (!File.Exists(TaskLibraryDatabasePath))
            {
                TaskLibraryDatabase database = ScriptableObject.CreateInstance<TaskLibraryDatabase>();
                SerializedObject soDb = new SerializedObject(database);
                SerializedProperty tasksProp = soDb.FindProperty("tasks");
                tasksProp.arraySize = definitions.Length;
                for (int i = 0; i < definitions.Length; i++)
                {
                    tasksProp.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
                }

                soDb.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(database, TaskLibraryDatabasePath);
                anyCreated = true;
            }

            return anyCreated;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name;
        }

        private static bool CreateTaskCardPrefabIfMissing()
        {
            if (File.Exists(TaskCardPrefabPath))
            {
                return false;
            }

            BuildTaskCardPrefab();
            return true;
        }

        private static bool CreateTaskLibraryPanelPrefabIfMissing()
        {
            if (File.Exists(TaskLibraryPanelPath))
            {
                return false;
            }

            BuildTaskLibraryPanelPrefab();
            return true;
        }

        // ---------------------------------------------------------------
        // 2) Wire Task Library Scene
        // ---------------------------------------------------------------

        [MenuItem("Norscope/HoloLens/Phase 6/2) Wire Task Library Scene")]
        public static void WireTaskLibrarySceneMenu()
        {
            WireTaskLibraryScene();
        }

        public static void WireTaskLibraryScene()
        {
            CreateTaskLibraryPrefabsIfMissing();

            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            PanelRegistry registry = Object.FindFirstObjectByType<PanelRegistry>();
            if (registry == null)
            {
                Debug.LogError("[Norscope Phase6] PanelRegistry not found in Bootstrap. Run Wire Phase 5 Scene first.");
                return;
            }

            GameObject taskLibraryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TaskLibraryPanelPath);
            SerializedObject soReg = new SerializedObject(registry);
            SerializedProperty entries = soReg.FindProperty("entries");
            bool found = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                SerializedProperty panelType = entry.FindPropertyRelative("panelType");
                if ((PanelType)panelType.enumValueIndex == PanelType.TaskLibrary)
                {
                    entry.FindPropertyRelative("prefab").objectReferenceValue = taskLibraryPrefab;
                    found = true;
                    break;
                }
            }

            soReg.ApplyModifiedPropertiesWithoutUndo();

            if (!found)
            {
                Debug.LogError("[Norscope Phase6] PanelRegistry has no TaskLibrary entry to wire. Run Wire Phase 5 Scene first.");
                return;
            }

            registry.RebuildLookup();
            EditorUtility.SetDirty(registry);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Norscope Phase6] Wire Task Library Scene complete. TaskLibraryPanel.prefab was not modified.");
        }

        // ---------------------------------------------------------------
        // 4) Rebuild Task Library Panel (Destructive) — regenerates ONLY TaskLibraryPanel.prefab.
        //    Never touches TaskCard.prefab.
        // ---------------------------------------------------------------

        [MenuItem("Norscope/HoloLens/Phase 6/4) Rebuild Task Library Panel (Destructive)")]
        public static void RebuildTaskLibraryPanelDestructiveMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Rebuild Task Library Panel — Destructive",
                "This will REPLACE exactly this asset, regenerating it from code:\n\n" +
                "  • " + TaskLibraryPanelPath + "\n\n" +
                "ANY MANUAL EDITS you made in Prefab Mode — panel dimensions, title bar, scroll " +
                "view, resize handle, anything — will be PERMANENTLY LOST and replaced with the " +
                "generated defaults.\n\n" +
                "NOT touched by this command:\n" +
                "  • " + TaskCardPrefabPath + " (your card design is untouched)\n" +
                "  • All TaskDefinition assets in " + TaskDataDir + "\n" +
                "  • " + TaskLibraryDatabasePath + " (list, order, and content unchanged)\n\n" +
                "A timestamped backup of the replaced prefab will be saved to:\n" + PrefabBackupDir +
                "\n\nThis cannot be undone with Ctrl+Z. Continue?",
                "Rebuild and Overwrite",
                "Cancel");

            if (!confirmed)
            {
                Debug.Log("[Norscope Phase6] Rebuild Task Library Panel (Destructive) cancelled by user.");
                return;
            }

            CreateTaskDataAssetsIfMissing();
            CreateTaskCardPrefabIfMissing();
            BackupPrefabIfExists(TaskLibraryPanelPath);

            BuildTaskLibraryPanelPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Phase6] Rebuild Task Library Panel (Destructive) complete — " +
                      "TaskLibraryPanel.prefab regenerated; TaskCard.prefab and task data untouched. " +
                      "Run Wire Task Library Scene next.");
        }

        // ---------------------------------------------------------------
        // 5) Rebuild Task Card Prefab (Destructive) — regenerates ONLY TaskCard.prefab.
        // ---------------------------------------------------------------

        [MenuItem("Norscope/HoloLens/Phase 6/5) Rebuild Task Card Prefab (Destructive)")]
        public static void RebuildTaskCardPrefabDestructiveMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Rebuild Task Card Prefab — Destructive",
                "This will REPLACE exactly this asset, regenerating it from code:\n\n" +
                "  • " + TaskCardPrefabPath + "\n\n" +
                "ANY MANUAL EDITS you made in Prefab Mode — icon size, text position, colors, " +
                "spacing, everything — will be PERMANENTLY LOST and replaced with the generated " +
                "defaults.\n\n" +
                "NOT touched by this command:\n" +
                "  • " + TaskLibraryPanelPath + " (title bar, scroll view are untouched)\n" +
                "  • All task content (TaskDefinition assets and TaskLibraryDatabase.asset)\n\n" +
                "A timestamped backup of the replaced prefab will be saved to:\n" + PrefabBackupDir +
                "\n\nThis cannot be undone with Ctrl+Z. Continue?",
                "Rebuild and Overwrite",
                "Cancel");

            if (!confirmed)
            {
                Debug.Log("[Norscope Phase6] Rebuild Task Card Prefab (Destructive) cancelled by user.");
                return;
            }

            BackupPrefabIfExists(TaskCardPrefabPath);
            BuildTaskCardPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Norscope Phase6] Rebuild Task Card Prefab (Destructive) complete — " +
                      "TaskCard.prefab regenerated. Existing cards already placed in a scene keep " +
                      "their old appearance until re-instantiated; enter Play Mode to see the new design.");
        }

        private static void BackupPrefabIfExists(string assetPath)
        {
            if (!File.Exists(assetPath))
            {
                return;
            }

            Directory.CreateDirectory(PrefabBackupDir);
            AssetDatabase.Refresh();

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = AssetDatabase.GenerateUniqueAssetPath(
                PrefabBackupDir + "/" + fileName + "_" + timestamp + ".prefab");
            AssetDatabase.CopyAsset(assetPath, backupPath);
            Debug.Log("[Norscope Phase6] Backed up " + assetPath + " -> " + backupPath);
        }

        // ---------------------------------------------------------------
        // TaskCard.prefab — built once via bake-and-freeze: elements are positioned using a
        // live HorizontalLayoutGroup/VerticalLayoutGroups just long enough to compute correct
        // values, then each child is frozen to an independent, top-left-anchored RectTransform
        // and every layout group is destroyed. The whole card is one Button — no separate Open
        // button — with transition=None so its own manual hover/press border-color writes don't
        // fight Selectable's built-in ColorTint.
        // ---------------------------------------------------------------
        private static string BuildTaskCardPrefab()
        {
            NorscopeTheme theme = AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            Sprite taskIcon = AssetDatabase.LoadAssetAtPath<Sprite>(TaskIconPath);
            TMP_FontAsset font = theme != null && theme.fonts != null ? theme.fonts.bodyFont : null;
            if (font == null)
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            }

            const float CardWidth = 640f;
            const float CardHeight = 104f;

            GameObject root = new GameObject("TaskCard", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(CardWidth, CardHeight);

            Image border = root.AddComponent<Image>();
            border.sprite = rounded;
            border.type = Image.Type.Sliced;
            border.color = new Color(0.149f, 0.188f, 0.255f, 1f);
            // Full card bounds — SpatialWindow.ConfigureGraphicRaycastTargets() strips
            // raycastTarget from every Graphic except a Selectable's exact targetGraphic, so
            // this must be the one authoritative hit target (see cardButton below).
            border.raycastTarget = true;

            // Content Content's VerticalLayoutGroup needs an explicit, unambiguous
            // preferredHeight from this card — Image also implements ILayoutElement and would
            // otherwise tie with this at the default priority (0), making the effective height
            // whichever is larger by accident. layoutPriority = 1 makes this LayoutElement win
            // outright, with no dependency on sprite border/native-size behavior.
            LayoutElement rootLe = root.AddComponent<LayoutElement>();
            rootLe.preferredHeight = CardHeight;
            rootLe.minHeight = CardHeight * 0.75f;
            rootLe.layoutPriority = 1;

            GameObject bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(root.transform, false);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(2f, 2f);
            bgRt.offsetMax = new Vector2(-2f, -2f);
            Image bg = bgGo.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.102f, 0.137f, 0.188f, 0.96f);
            bg.raycastTarget = false;
            bgGo.AddComponent<LayoutElement>().ignoreLayout = true;

            // Whole-card Button: this IS the click target, not a decoy. transition=None because
            // TaskCardView drives border.color manually on hover/press — leaving the default
            // ColorTint transition active would fight those writes every frame.
            Button cardButton = root.AddComponent<Button>();
            cardButton.targetGraphic = border;
            cardButton.transition = Selectable.Transition.None;
            cardButton.interactable = true;

            HorizontalLayoutGroup rowHl = root.AddComponent<HorizontalLayoutGroup>();
            rowHl.padding = new RectOffset(16, 16, 12, 12);
            rowHl.spacing = 14f;
            rowHl.childAlignment = TextAnchor.MiddleLeft;
            rowHl.childControlWidth = true;
            rowHl.childControlHeight = true;
            rowHl.childForceExpandWidth = false;
            rowHl.childForceExpandHeight = true;

            // ---- Icon ----
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(root.transform, false);
            Image icon = iconGo.AddComponent<Image>();
            icon.sprite = taskIcon;
            icon.color = theme != null ? theme.primaryAccent : Color.cyan;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            LayoutElement iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 44f;
            iconLe.minWidth = 44f;
            iconLe.preferredHeight = 44f;

            // ---- TextColumn ----
            GameObject textColumn = new GameObject("TextColumn", typeof(RectTransform));
            textColumn.transform.SetParent(root.transform, false);
            textColumn.AddComponent<LayoutElement>().flexibleWidth = 1f;
            VerticalLayoutGroup textVl = textColumn.AddComponent<VerticalLayoutGroup>();
            textVl.spacing = 4f;
            textVl.childAlignment = TextAnchor.MiddleLeft;
            textVl.childControlWidth = true;
            textVl.childForceExpandWidth = true;
            textVl.childControlHeight = false;
            textVl.childForceExpandHeight = false;

            GameObject titleGo = new GameObject("TaskTitle", typeof(RectTransform));
            titleGo.transform.SetParent(textColumn.transform, false);
            TMP_Text titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.font = font;
            titleText.fontSize = 17f;
            titleText.color = theme != null ? theme.primaryText : Color.white;
            titleText.raycastTarget = false;
            titleGo.AddComponent<LayoutElement>().preferredHeight = 23f;

            GameObject descGo = new GameObject("Description", typeof(RectTransform));
            descGo.transform.SetParent(textColumn.transform, false);
            TMP_Text descText = descGo.AddComponent<TextMeshProUGUI>();
            descText.font = font;
            descText.fontSize = 12f;
            descText.color = theme != null ? theme.secondaryText : Color.gray;
            descText.raycastTarget = false;
            descGo.AddComponent<LayoutElement>().preferredHeight = 20f;

            // ---- MetaColumn ----
            GameObject metaColumn = new GameObject("MetaColumn", typeof(RectTransform));
            metaColumn.transform.SetParent(root.transform, false);
            LayoutElement metaLe = metaColumn.AddComponent<LayoutElement>();
            metaLe.preferredWidth = 180f;
            metaLe.minWidth = 180f;
            HorizontalLayoutGroup metaHl = metaColumn.AddComponent<HorizontalLayoutGroup>();
            metaHl.spacing = 10f;
            metaHl.childAlignment = TextAnchor.MiddleRight;
            metaHl.childControlWidth = false;
            metaHl.childControlHeight = true;
            metaHl.childForceExpandHeight = true;

            GameObject durationGo = new GameObject("Duration", typeof(RectTransform));
            durationGo.transform.SetParent(metaColumn.transform, false);
            TMP_Text durationText = durationGo.AddComponent<TextMeshProUGUI>();
            durationText.font = font;
            durationText.fontSize = 12f;
            durationText.color = theme != null ? theme.secondaryText : Color.gray;
            durationText.raycastTarget = false;
            durationGo.AddComponent<LayoutElement>().preferredWidth = 74f;

            GameObject badgeGo = new GameObject("DifficultyBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(metaColumn.transform, false);
            Image badgeBg = badgeGo.AddComponent<Image>();
            badgeBg.sprite = rounded;
            badgeBg.type = Image.Type.Sliced;
            badgeBg.raycastTarget = false;
            LayoutElement badgeLe = badgeGo.AddComponent<LayoutElement>();
            badgeLe.preferredWidth = 92f;
            badgeLe.preferredHeight = 26f;

            GameObject badgeTextGo = new GameObject("Label", typeof(RectTransform));
            badgeTextGo.transform.SetParent(badgeGo.transform, false);
            RectTransform badgeTextRt = badgeTextGo.GetComponent<RectTransform>();
            badgeTextRt.anchorMin = Vector2.zero;
            badgeTextRt.anchorMax = Vector2.one;
            badgeTextRt.offsetMin = Vector2.zero;
            badgeTextRt.offsetMax = Vector2.zero;
            TMP_Text badgeText = badgeTextGo.AddComponent<TextMeshProUGUI>();
            badgeText.font = font;
            badgeText.fontSize = 11f;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.raycastTarget = false;

            if (root.GetComponent<UiButtonPressScale>() == null)
            {
                root.AddComponent<UiButtonPressScale>();
            }

            // Bake outer-to-inner: root's own children first (so TextColumn/MetaColumn end up
            // with their correct final width before their own children are baked), then each
            // inner container. Every LayoutGroup is destroyed right after its bake, so nothing
            // is left computing layout at runtime or in Prefab Mode.
            BakeChildrenToManualRects(rootRt);
            Object.DestroyImmediate(rowHl);
            BakeChildrenToManualRects((RectTransform)textColumn.transform);
            Object.DestroyImmediate(textVl);
            BakeChildrenToManualRects((RectTransform)metaColumn.transform);
            Object.DestroyImmediate(metaHl);

            TaskCardView view = root.AddComponent<TaskCardView>();
            SerializedObject soView = new SerializedObject(view);
            soView.FindProperty("icon").objectReferenceValue = icon;
            soView.FindProperty("titleText").objectReferenceValue = titleText;
            soView.FindProperty("descriptionText").objectReferenceValue = descText;
            soView.FindProperty("durationText").objectReferenceValue = durationText;
            soView.FindProperty("difficultyBadgeBackground").objectReferenceValue = badgeBg;
            soView.FindProperty("difficultyBadgeText").objectReferenceValue = badgeText;
            soView.FindProperty("cardButton").objectReferenceValue = cardButton;
            soView.FindProperty("border").objectReferenceValue = border;
            soView.FindProperty("normalBorderColor").colorValue = border.color;
            soView.FindProperty("hoverBorderColor").colorValue = new Color(0.133f, 0.827f, 0.933f, 1f);
            soView.FindProperty("pressedBorderColor").colorValue = new Color(0.098f, 0.6f, 0.686f, 1f);
            soView.FindProperty("theme").objectReferenceValue = theme;
            soView.FindProperty("fallbackIcon").objectReferenceValue = taskIcon;
            soView.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, TaskCardPrefabPath);
            Object.DestroyImmediate(root);
            return TaskCardPrefabPath;
        }

        // ---------------------------------------------------------------
        // TaskLibraryPanel.prefab — title bar / scroll view are baked to independent manual
        // RectTransforms (same technique as HomePanel) so the macro placement is fully hand-
        // editable. Content's own VerticalLayoutGroup + ContentSizeFitter is kept live and
        // permanent — required for a dynamic, growing task list; the user's own spec calls for
        // exactly this ("Use Unity's ScrollRect + VerticalLayoutGroup + ContentSizeFitter. Do
        // not manually position runtime cards."). Content is left EMPTY when saved — no nested
        // preview instance — since a nested PrefabUtility.InstantiatePrefab instance here caused
        // a real "Missing Nested Prefab Asset" corruption in the previous implementation when
        // its source prefab was later deleted. Preview by entering Play Mode instead.
        // ---------------------------------------------------------------
        private static string BuildTaskLibraryPanelPrefab()
        {
            NorscopeTheme theme = AssetDatabase.LoadAssetAtPath<NorscopeTheme>(ThemePath);
            NorscopeWorldSpaceUiConfig world = AssetDatabase.LoadAssetAtPath<NorscopeWorldSpaceUiConfig>(WorldConfigPath);
            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            TMP_FontAsset font = theme != null && theme.fonts != null ? theme.fonts.bodyFont : null;
            if (font == null)
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            }

            const float PanelWidth = 700f;
            const float PanelHeight = 520f;
            const float TitleBarHeight = 48f;

            GameObject root = new GameObject("TaskLibraryPanel", typeof(RectTransform));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            GameObject bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(root.transform, false);
            StretchFull(bgGo.GetComponent<RectTransform>());
            Image bg = bgGo.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.color = theme != null ? theme.panelBackground : Color.black;
            bg.raycastTarget = true;
            bgGo.AddComponent<LayoutElement>().ignoreLayout = true;

            GameObject borderGo = new GameObject("Border", typeof(RectTransform));
            borderGo.transform.SetParent(root.transform, false);
            StretchFull(borderGo.GetComponent<RectTransform>());
            Image border = borderGo.AddComponent<Image>();
            border.sprite = rounded;
            border.type = Image.Type.Sliced;
            border.color = theme != null ? theme.border : Color.gray;
            border.raycastTarget = false;
            border.enabled = false;
            borderGo.AddComponent<LayoutElement>().ignoreLayout = true;

            VerticalLayoutGroup rootLayout = root.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(0, 0, 0, 0);
            rootLayout.spacing = 0f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = false;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // ---- Title bar ----
            GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform));
            titleBar.transform.SetParent(root.transform, false);
            // rootLayout hasn't been rebuilt yet at this point, so TitleBar is still its
            // default (100x100) RectTransform. Its own HorizontalLayoutGroup gets force-rebuilt
            // below (via BakeChildrenToManualRects) to freeze Title/GrabRegion/CloseButton — that
            // rebuild needs TitleBar's real width or it bakes children against a too-narrow
            // container. Set it explicitly now; root's later bake pass (which sizes TitleBar
            // itself within the panel) confirms/overwrites it with the same value anyway.
            titleBar.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelWidth, TitleBarHeight);
            Image titleBg = titleBar.AddComponent<Image>();
            titleBg.sprite = rounded;
            titleBg.type = Image.Type.Sliced;
            titleBg.color = theme != null ? theme.elevatedPanelBackground : Color.gray;
            HorizontalLayoutGroup titleHl = titleBar.AddComponent<HorizontalLayoutGroup>();
            titleHl.padding = new RectOffset(14, 10, 8, 8);
            titleHl.spacing = 8f;
            titleHl.childAlignment = TextAnchor.MiddleLeft;
            titleHl.childControlWidth = false;
            titleHl.childControlHeight = true;
            titleHl.childForceExpandHeight = true;
            LayoutElement titleLe = titleBar.AddComponent<LayoutElement>();
            titleLe.minHeight = TitleBarHeight;
            titleLe.preferredHeight = TitleBarHeight;

            GameObject titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(titleBar.transform, false);
            TMP_Text title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "Tasks";
            title.font = font;
            title.fontSize = theme != null ? theme.headingSize : 20f;
            title.color = theme != null ? theme.primaryText : Color.white;
            title.raycastTarget = false;
            titleGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

            GameObject grab = new GameObject("GrabRegion", typeof(RectTransform));
            grab.transform.SetParent(titleBar.transform, false);
            Image grabImg = grab.AddComponent<Image>();
            grabImg.color = theme != null
                ? new Color(theme.border.r, theme.border.g, theme.border.b, 0.65f)
                : Color.gray;
            grabImg.raycastTarget = true;
            LayoutElement grabLe = grab.AddComponent<LayoutElement>();
            grabLe.preferredWidth = 72f;
            grabLe.preferredHeight = 8f;

            GameObject closePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CloseButtonPath);
            GameObject close = closePrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(closePrefab)
                : new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            close.name = "CloseButton";
            close.transform.SetParent(titleBar.transform, false);

            BakeChildrenToManualRects((RectTransform)titleBar.transform);
            Object.DestroyImmediate(titleHl);

            // ---- Scrollable task list ----
            GameObject scrollRoot = new GameObject("ScrollView", typeof(RectTransform));
            scrollRoot.transform.SetParent(root.transform, false);
            LayoutElement scrollLe = scrollRoot.AddComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1f;
            scrollLe.preferredHeight = PanelHeight - TitleBarHeight;
            ScrollRect scrollRect = scrollRoot.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollRoot.transform, false);
            StretchFull(viewport.GetComponent<RectTransform>());
            Image viewportImg = viewport.AddComponent<Image>();
            viewportImg.color = Color.clear;
            viewportImg.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            // childControlHeight=true is what makes this list respect each card's own
            // preferredHeight (LayoutElement, layoutPriority=1); childForceExpandHeight=false
            // stops it from stretching cards to fill leftover space. Padding/Spacing here are
            // the Inspector-editable "list padding" / "spacing between cards" controls — live,
            // permanent, never touched by any runtime script.
            VerticalLayoutGroup contentVl = content.AddComponent<VerticalLayoutGroup>();
            contentVl.padding = new RectOffset(12, 12, 10, 10);
            contentVl.spacing = 10f;
            contentVl.childAlignment = TextAnchor.UpperLeft;
            contentVl.childControlWidth = true;
            contentVl.childForceExpandWidth = true;
            contentVl.childControlHeight = true;
            contentVl.childForceExpandHeight = false;
            ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.content = contentRt;
            scrollRect.viewport = viewport.GetComponent<RectTransform>();

            BakeChildrenToManualRects(rt);
            Object.DestroyImmediate(rootLayout);

            GameObject handles = new GameObject("ResizeHandles", typeof(RectTransform));
            handles.transform.SetParent(root.transform, false);
            StretchFull(handles.GetComponent<RectTransform>());
            handles.AddComponent<LayoutElement>().ignoreLayout = true;
            PlaceHandle(handles.transform, "BottomRight", new Vector2(1f, 0f), new Vector2(-10f, 10f), theme);

            NorscopePanelRoot panelRoot = root.AddComponent<NorscopePanelRoot>();
            SerializedObject soPanel = new SerializedObject(panelRoot);
            soPanel.FindProperty("theme").objectReferenceValue = theme;
            soPanel.FindProperty("worldSpaceConfig").objectReferenceValue = world;
            soPanel.FindProperty("root").objectReferenceValue = rt;
            soPanel.FindProperty("background").objectReferenceValue = bg;
            soPanel.FindProperty("border").objectReferenceValue = border;
            soPanel.FindProperty("title").objectReferenceValue = title;
            soPanel.FindProperty("sizeMeters").vector2Value = new Vector2(PanelWidth / 1000f, PanelHeight / 1000f);
            soPanel.FindProperty("sizePixels").vector2Value = new Vector2(PanelWidth, PanelHeight);
            soPanel.FindProperty("useMetersAsSource").boolValue = true;
            soPanel.ApplyModifiedPropertiesWithoutUndo();
            panelRoot.Apply();

            SpatialWindowBindings bindings = root.AddComponent<SpatialWindowBindings>();
            SerializedObject soBind = new SerializedObject(bindings);
            soBind.FindProperty("grabRegion").objectReferenceValue = grab.transform;
            soBind.FindProperty("resizeHandle").objectReferenceValue = handles.transform.Find("BottomRight") as RectTransform;
            soBind.FindProperty("closeButtonRoot").objectReferenceValue = close.transform;
            soBind.ApplyModifiedPropertiesWithoutUndo();

            TaskLibraryDatabase taskDatabase = AssetDatabase.LoadAssetAtPath<TaskLibraryDatabase>(TaskLibraryDatabasePath);
            TaskCardView cardPrefabView = AssetDatabase.LoadAssetAtPath<TaskCardView>(TaskCardPrefabPath);

            TaskLibraryPanel taskLibraryPanel = root.AddComponent<TaskLibraryPanel>();
            SerializedObject soTlp = new SerializedObject(taskLibraryPanel);
            soTlp.FindProperty("taskDatabase").objectReferenceValue = taskDatabase;
            soTlp.FindProperty("cardContent").objectReferenceValue = contentRt;
            soTlp.FindProperty("cardPrefab").objectReferenceValue = cardPrefabView;
            soTlp.ApplyModifiedPropertiesWithoutUndo();

            // Content is intentionally left empty — see method doc comment above. Clear
            // whatever the AddComponent<TaskLibraryPanel> above may have populated via its own
            // OnEnable (harmless either way since it only instantiates+configures, never
            // resizes anything) so the saved prefab's Content starts clean.
            for (int i = contentRt.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(contentRt.GetChild(i).gameObject);
            }

            bgGo.transform.SetAsFirstSibling();

            PrefabUtility.SaveAsPrefabAsset(root, TaskLibraryPanelPath);
            Object.DestroyImmediate(root);
            return TaskLibraryPanelPath;
        }

        private static void PlaceHandle(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, NorscopeTheme theme)
        {
            GameObject handle = new GameObject(name, typeof(RectTransform));
            handle.transform.SetParent(parent, false);
            RectTransform rt = handle.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = new Vector2(16f, 16f);
            rt.anchoredPosition = anchoredPos;
            Image img = handle.AddComponent<Image>();
            img.color = theme != null
                ? new Color(theme.primaryAccent.r, theme.primaryAccent.g, theme.primaryAccent.b, 0.55f)
                : Color.cyan;
            img.raycastTarget = true;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Same technique used elsewhere in this project (HomePanel, InteractionTestPanel)
        /// — duplicated locally rather than shared, so those builders stay untouched. Used once,
        /// at build time, to convert live-layout-positioned children into permanent, independent
        /// RectTransforms. Never called at runtime.</summary>
        private static void BakeChildrenToManualRects(RectTransform container)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(container);

            for (int i = 0; i < container.childCount; i++)
            {
                RectTransform child = container.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                LayoutElement le = child.GetComponent<LayoutElement>();
                if (le != null && le.ignoreLayout)
                {
                    continue;
                }

                FreezeToTopLeftAnchor(child);
            }
        }

        private static void FreezeToTopLeftAnchor(RectTransform rt)
        {
            RectTransform parentRt = rt.parent as RectTransform;
            if (parentRt == null)
            {
                return;
            }

            Vector3[] worldCorners = new Vector3[4]; // 0=BL, 1=TL, 2=TR, 3=BR
            rt.GetWorldCorners(worldCorners);

            Vector3 topLeftInParent = parentRt.InverseTransformPoint(worldCorners[1]);
            Vector3 bottomRightInParent = parentRt.InverseTransformPoint(worldCorners[3]);

            float width = bottomRightInParent.x - topLeftInParent.x;
            float height = topLeftInParent.y - bottomRightInParent.y;

            Rect parentLocalRect = parentRt.rect;
            float offsetX = topLeftInParent.x - parentLocalRect.xMin;
            float offsetY = parentLocalRect.yMax - topLeftInParent.y;

            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(offsetX, offsetY);
        }

        // ---------------------------------------------------------------
        // 3) Validate Task Library
        // ---------------------------------------------------------------

        [MenuItem("Norscope/HoloLens/Phase 6/3) Validate Task Library")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out string report);
            Debug.Log(ok
                ? "[Norscope Phase6] Validate Task Library PASSED\n" + report
                : "[Norscope Phase6] Validate Task Library FAILED\n" + report);
            EditorUtility.DisplayDialog(
                "Validate Task Library",
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

            Check(File.Exists(TaskCardPrefabPath), "TaskCard.prefab exists");
            Check(File.Exists(TaskLibraryPanelPath), "TaskLibraryPanel.prefab exists");
            Check(File.Exists(TaskCardViewSourcePath), "TaskCardView.cs exists");
            Check(NoRuntimeHierarchyConstruction(), "TaskLibraryPanel.cs contains no runtime GameObject/RectTransform construction");
            Check(CardsComeFromPrefabInstantiate(), "Cards come from Instantiate(cardPrefab) — not built in code");
            Check(NoResetMethodsOnCardOrPanel(), "No OnValidate/Update on TaskCardView or TaskLibraryPanel resets content");

            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TaskCardPrefabPath);
            if (cardPrefab != null)
            {
                TaskCardView cardView = cardPrefab.GetComponent<TaskCardView>();
                Check(cardView != null, "TaskCard.prefab has a TaskCardView component");

                Button cardButtonComp = cardPrefab.GetComponent<Button>();
                Check(cardButtonComp != null, "TaskCard.prefab's root has a Button (whole card is clickable)");
                Check(cardButtonComp != null && cardButtonComp.transition == Selectable.Transition.None,
                    "TaskCard's Button transition is None (no fight with manual border-color writes)");

                LayoutElement cardLe = cardPrefab.GetComponent<LayoutElement>();
                Check(cardLe != null && cardLe.preferredHeight > 0f, "TaskCard has a LayoutElement with preferredHeight > 0");
                Check(cardLe != null && cardLe.layoutPriority > 0,
                    "TaskCard's LayoutElement has layoutPriority > 0 (unambiguously wins over its own Image)");

                if (cardView != null)
                {
                    SerializedObject soCard = new SerializedObject(cardView);
                    string[] requiredRefs = { "icon", "titleText", "descriptionText", "durationText",
                        "difficultyBadgeBackground", "difficultyBadgeText", "cardButton", "border" };
                    bool allWired = requiredRefs.All(f => soCard.FindProperty(f).objectReferenceValue != null);
                    Check(allWired, "TaskCard.prefab's TaskCardView has all content targets wired");
                }
            }

            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TaskLibraryPanelPath);
            TaskLibraryPanel panelComp = null;
            if (panelPrefab != null)
            {
                Check(panelPrefab.GetComponent<NorscopePanelRoot>() != null, "TaskLibraryPanel has NorscopePanelRoot");
                Check(panelPrefab.GetComponent<SpatialWindowBindings>() != null, "TaskLibraryPanel has SpatialWindowBindings");
                panelComp = panelPrefab.GetComponent<TaskLibraryPanel>();
                Check(panelComp != null, "TaskLibraryPanel has TaskLibraryPanel component");
                Check(panelPrefab.transform.Find("TitleBar") != null, "TaskLibraryPanel has TitleBar");
                Check(panelPrefab.transform.Find("TitleBar/CloseButton") != null, "TitleBar has a CloseButton");
                Check(panelPrefab.transform.Find("TitleBar/GrabRegion") != null, "TitleBar has a GrabRegion");

                Transform scrollView = panelPrefab.transform.Find("ScrollView");
                Check(scrollView != null && scrollView.GetComponent<ScrollRect>() != null,
                    "TaskLibraryPanel has a working ScrollRect");

                Transform contentTransform = scrollView != null ? scrollView.Find("Viewport/Content") : null;
                VerticalLayoutGroup listVl = contentTransform != null ? contentTransform.GetComponent<VerticalLayoutGroup>() : null;
                ContentSizeFitter listFitter = contentTransform != null ? contentTransform.GetComponent<ContentSizeFitter>() : null;
                Check(listVl != null, "Content has a VerticalLayoutGroup (per spec: ScrollRect + VerticalLayoutGroup + ContentSizeFitter)");
                Check(listFitter != null, "Content has a ContentSizeFitter");
                Check(listVl != null && !listVl.childForceExpandHeight, "Content's VerticalLayoutGroup does not force-expand child height");
                Check(listVl != null && listVl.childControlHeight, "Content's VerticalLayoutGroup respects each card's own preferredHeight");
                Check(contentTransform != null && contentTransform.childCount == 0,
                    "Content has no nested preview instance saved in the prefab (avoids prior corruption class)");

                if (panelComp != null)
                {
                    SerializedObject soPanelCheck = new SerializedObject(panelComp);
                    Check(soPanelCheck.FindProperty("taskDatabase").objectReferenceValue != null, "TaskLibraryPanel has a TaskLibraryDatabase assigned");
                    Check(soPanelCheck.FindProperty("cardPrefab").objectReferenceValue != null, "TaskLibraryPanel has a cardPrefab assigned");
                }
            }

            TaskLibraryDatabase database = AssetDatabase.LoadAssetAtPath<TaskLibraryDatabase>(TaskLibraryDatabasePath);
            Check(database != null, "TaskLibraryDatabase.asset exists");
            if (database != null)
            {
                var names = database.Tasks.Where(t => t != null).Select(t => t.DisplayName).ToList();
                bool hasAllSix = InitialTaskSeed.All(seed => names.Contains(seed.displayName));
                Check(database.Tasks.Count >= InitialTaskSeed.Length && hasAllSix, "Database contains the six initial tasks");

                var ids = database.Tasks.Where(t => t != null).Select(t => t.TaskId).ToList();
                Check(ids.All(id => !string.IsNullOrWhiteSpace(id)), "All task IDs are non-empty");
                Check(ids.Distinct().Count() == ids.Count, "All task IDs are unique");

                bool everyTaskHasSteps = database.Tasks.Where(t => t != null).All(t => t.Steps.Count > 0);
                Check(everyTaskHasSteps, "Every task has at least one step");

                bool anyWarningPresent = database.Tasks.Where(t => t != null)
                    .SelectMany(t => t.Steps).Any(s => !string.IsNullOrWhiteSpace(s.warning));
                bool anyVerificationPresent = database.Tasks.Where(t => t != null)
                    .SelectMany(t => t.Steps).Any(s => !string.IsNullOrWhiteSpace(s.verification));
                Check(anyWarningPresent, "At least one step demonstrates the optional warning field");
                Check(anyVerificationPresent, "At least one step demonstrates the optional verification field");

                // Empirically prove the real repository logic: enabled tasks appear in database
                // order with their steps intact, disabled tasks are excluded — exercises the
                // actual runtime code path, not a re-implementation of it.
                var repository = new ScriptableObjectTaskRepository(database);
                var allTasks = repository.GetAll();
                var expectedOrderedEnabledIds = database.Tasks.Where(t => t != null && t.IsEnabled).Select(t => t.TaskId).ToList();
                Check(expectedOrderedEnabledIds.SequenceEqual(allTasks.Select(t => t.id)), "Enabled tasks appear in database order");
                Check(allTasks.All(t => t.steps != null && t.steps.Count > 0), "Repository output carries each task's steps through");
            }

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            PanelRegistry registry = Object.FindFirstObjectByType<PanelRegistry>();
            Check(registry != null, "PanelRegistry present in Bootstrap");
            if (registry != null)
            {
                registry.RebuildLookup();
                bool hasEntry = registry.TryGetEntry(PanelType.TaskLibrary, out PanelRegistryEntry entry);
                Check(hasEntry && entry.prefab != null, "PanelRegistry TaskLibrary entry has a prefab");
                Check(hasEntry && entry.behaviour == PanelBehaviourType.WorldLocked, "TaskLibrary behaviour is WorldLocked");
                Check(hasEntry && entry.windowId == SpatialWindowId.TaskLibrary, "TaskLibrary maps to SpatialWindowId.TaskLibrary");
            }

            GameObject homePanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Norscope/UI/Panels/HomePanel.prefab");
            HomePanelController homeController = homePanelPrefab != null ? homePanelPrefab.GetComponent<HomePanelController>() : null;
            bool tasksCardWired = homeController != null && homeController.Cards != null &&
                homeController.Cards.Any(c => c != null && c.TargetPanel == PanelType.TaskLibrary);
            Check(tasksCardWired, "Home Panel Tasks card targets PanelType.TaskLibrary");

            // Empirically prove normal Create/Wire never overwrites either prefab or existing
            // content data.
            string cardBefore = ComputeFileHash(TaskCardPrefabPath);
            string panelBefore = ComputeFileHash(TaskLibraryPanelPath);
            string databaseBefore = ComputeFileHash(TaskLibraryDatabasePath);
            var taskAssetHashesBefore = GetTaskDefinitionAssetHashes();

            CreateTaskLibraryPrefabsIfMissing();

            Check(cardBefore == ComputeFileHash(TaskCardPrefabPath), "Normal Create/Wire does not overwrite TaskCard.prefab");
            Check(panelBefore == ComputeFileHash(TaskLibraryPanelPath), "Normal Create/Wire does not overwrite TaskLibraryPanel.prefab");
            Check(databaseBefore == ComputeFileHash(TaskLibraryDatabasePath), "Normal Create/Wire does not reset the TaskLibraryDatabase list");
            Check(taskAssetHashesBefore.SequenceEqual(GetTaskDefinitionAssetHashes()), "Normal Create/Wire does not recreate existing TaskDefinition assets");

            report = sb.ToString();
            return fails == 0;
        }

        private static System.Collections.Generic.List<string> GetTaskDefinitionAssetHashes()
        {
            var hashes = new System.Collections.Generic.List<string>();
            if (!Directory.Exists(TaskDataDir))
            {
                return hashes;
            }

            foreach (string path in Directory.GetFiles(TaskDataDir, "*.asset").OrderBy(p => p))
            {
                hashes.Add(ComputeFileHash(path.Replace('\\', '/')));
            }

            return hashes;
        }

        /// <summary>Static source guard: TaskLibraryPanel.cs must not construct GameObjects or
        /// RectTransforms at runtime — every position/size lives on TaskCard.prefab instead.</summary>
        private static bool NoRuntimeHierarchyConstruction()
        {
            if (!File.Exists(TaskLibraryPanelSourcePath))
            {
                return false;
            }

            string source = File.ReadAllText(TaskLibraryPanelSourcePath);
            return !source.Contains("new GameObject(") &&
                   !source.Contains("AddComponent<RectTransform>") &&
                   !source.Contains("AddComponent<LayoutElement>") &&
                   !source.Contains("AddComponent<HorizontalLayoutGroup>") &&
                   !source.Contains("AddComponent<VerticalLayoutGroup>");
        }

        private static bool CardsComeFromPrefabInstantiate()
        {
            if (!File.Exists(TaskLibraryPanelSourcePath))
            {
                return false;
            }

            string source = File.ReadAllText(TaskLibraryPanelSourcePath);
            return source.Contains("Instantiate(cardPrefab");
        }

        /// <summary>Reflection guard: neither TaskCardView nor TaskLibraryPanel may implement
        /// OnValidate/Update, which is exactly the pattern that would silently reset content or
        /// fight the user's own edits every frame/every Inspector change.</summary>
        private static bool NoResetMethodsOnCardOrPanel()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            return typeof(TaskLibraryPanel).GetMethod("OnValidate", flags) == null &&
                   typeof(TaskLibraryPanel).GetMethod("Update", flags) == null &&
                   typeof(TaskCardView).GetMethod("OnValidate", flags) == null &&
                   typeof(TaskCardView).GetMethod("Update", flags) == null;
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
