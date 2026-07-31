using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Norscope.Navigation;
using Norscope.UI;

namespace Norscope.Editor
{
    [Serializable] internal sealed class D3HomeData
    {
        public string capturedUtc;
        public string backupPath;
        public string backupHash;
        public string[] hierarchy;
        public D3ObjectComponents[] components;
        public D3Button[] buttons;
        public D3Card[] cards;
        public D3Image[] images;
        public D3PanelRoot panelRoot;
        public D3Source[] sources;
    }

    [Serializable] internal sealed class D3ObjectComponents
    {
        public string path;
        public string[] types;
    }

    [Serializable] internal sealed class D3Button
    {
        public string path;
        public string identity;
        public string targetGraphic;
        public string[] listeners;
    }

    [Serializable] internal sealed class D3Card
    {
        public string path;
        public int destination;
        public string button;
        public string background;
        public string border;
        public string icon;
        public string title;
        public string subtitle;
        public string iconSprite;
    }

    [Serializable] internal sealed class D3Image
    {
        public string path;
        public string identity;
        public string sprite;
    }

    [Serializable] internal sealed class D3PanelRoot
    {
        public string theme;
        public string worldSpaceConfig;
        public string root;
        public string background;
        public string border;
        public string title;
    }

    [Serializable] internal sealed class D3Source
    {
        public string path;
        public string hash;
    }

    internal static class D3HomeBaseline
    {
        internal const string HomePath =
            "Assets/Norscope/UI/Panels/HomePanel.prefab";
        internal const string BaselinePath =
            "Assets/Norscope/Editor/Validation/D3HomeBaseline.json";
        private const string BackupRoot =
            "Assets/Norscope/DesignSystem/Backups";

        internal static readonly string[] CardNames =
        {
            "Card_TaskLibrary", "Card_AskAI", "Card_Reports",
            "Card_Settings", "Card_Storage", "Card_About"
        };

        internal static bool Capture(out string report)
        {
            if (File.Exists(BaselinePath))
            {
                string archiveFolder = BackupRoot + "/BaselineManifests";
                EnsureFolder(archiveFolder);
                string archivePath = archiveFolder + "/D3HomeBaseline_" +
                                     DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") +
                                     ".json";
                File.Copy(BaselinePath, archivePath, false);
                AssetDatabase.ImportAsset(archivePath,
                    ImportAssetOptions.ForceUpdate);
            }
            if (!File.Exists(HomePath))
            {
                report = "HomePanel.prefab is missing.";
                return false;
            }

            D3HomeData data = CaptureCurrent();
            string folder = BackupRoot + "/PreD3Home_" +
                            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            EnsureFolder(BackupRoot);
            EnsureFolder(folder);
            string backup = folder + "/HomePanel.prefab";
            if (!AssetDatabase.CopyAsset(HomePath, backup))
            {
                report = "Could not create Home-only backup.";
                return false;
            }
            data.backupPath = backup;
            data.backupHash = Hash(backup);
            if (data.backupHash != Hash(HomePath))
            {
                report = "Home backup hash verification failed.";
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath));
            File.WriteAllText(BaselinePath, JsonUtility.ToJson(data, true));
            AssetDatabase.ImportAsset(BaselinePath, ImportAssetOptions.ForceUpdate);
            report = "Captured a fresh Home hierarchy, components, Buttons, listeners, card " +
                     "destinations/references, sprites, panel references and " +
                     data.sources.Length + " runtime source hashes.\nBackup: " + backup;
            return true;
        }

        internal static bool Compare(bool allowApprovedD3Changes, out string report)
        {
            D3HomeData before = Load();
            if (before == null)
            {
                report = "Run Capture Home Baseline first.";
                return false;
            }
            D3HomeData now = CaptureCurrent();
            var issues = new List<string>();

            string[] allowed = ApprovedAddedPaths();
            string[] newlyAllowed = allowApprovedD3Changes
                ? allowed.Where(path => !before.hierarchy.Contains(path)).ToArray()
                : Array.Empty<string>();
            string[] currentOriginalHierarchy = allowApprovedD3Changes
                ? now.hierarchy.Where(path => !newlyAllowed.Contains(path)).ToArray()
                : now.hierarchy;
            if (!before.hierarchy.SequenceEqual(currentOriginalHierarchy))
                issues.Add("Original Home hierarchy changed outside the approved additions.");
            D3ObjectComponents[] currentOriginalComponents =
                allowApprovedD3Changes
                    ? now.components.Where(record =>
                        !newlyAllowed.Contains(record.path)).ToArray()
                    : now.components;
            if (Serialize(before.components) != Serialize(currentOriginalComponents))
                issues.Add("Components on original Home objects changed.");
            if (Serialize(before.buttons) != Serialize(now.buttons))
                issues.Add("Button identity, target graphic or persistent listener changed.");
            if (!CardsMatch(before.cards, now.cards, allowApprovedD3Changes))
                issues.Add("Card destination, reference or original icon sprite changed.");

            D3Image[] originalImages = allowApprovedD3Changes
                ? now.images.Where(image => !newlyAllowed.Contains(image.path)).ToArray()
                : now.images;
            if (!ImagesMatch(before.images, originalImages,
                    allowApprovedD3Changes))
                issues.Add("Original Image or sprite reference changed.");

            if (!PanelRootMatches(before.panelRoot, now.panelRoot,
                    allowApprovedD3Changes))
                issues.Add("NorscopePanelRoot references changed outside the approved " +
                           "Home-only title reference exception.");
            if (Serialize(before.sources) != Serialize(now.sources))
                issues.Add("Runtime source hash changed.");

            report = issues.Count == 0
                ? "Home functional baseline matches. " +
                  (allowApprovedD3Changes
                      ? "Only approved hierarchy/title-reference changes are present."
                      : "No D3 changes are present.")
                : string.Join("\n", issues);
            return issues.Count == 0;
        }

        internal static bool Restore(out string report)
        {
            D3HomeData data = Load();
            if (data == null || string.IsNullOrEmpty(data.backupPath) ||
                !File.Exists(data.backupPath) ||
                Hash(data.backupPath) != data.backupHash)
            {
                report = "Verified D3 Home backup is missing.";
                return false;
            }
            File.Copy(data.backupPath, HomePath, true);
            AssetDatabase.ImportAsset(HomePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            report = "Restored HomePanel.prefab from " + data.backupPath +
                     "\nTimestamp: " + DateTime.UtcNow.ToString("O");
            return true;
        }

        internal static string[] ApprovedAddedPaths()
        {
            var paths = new List<string>
            {
                "HomePanel/FigmaContent",
                "HomePanel/Content/BrandLogo",
                "HomePanel/Content/Wordmark",
                "HomePanel/Content/Wordmark/IconContainer",
                "HomePanel/Content/Wordmark/IconContainer/Icon",
                "HomePanel/Content/Wordmark/Label",
                "HomePanel/Content/Separator",
                "HomePanel/Content/Footer",
                "HomePanel/TitleBar/HeaderSubtitle"
            };
            foreach (string name in CardNames)
            {
                string card = "HomePanel/Content/" + name;
                paths.Add(card + "/Shadow");
                paths.Add(card + "/IconContainer");
                paths.Add(card + "/Chevron");
                paths.Add(card + "/TopSheen");
                paths.Add(card + "/FocusOutline");
                paths.Add(card + "/FocusAccent");
            }
            return paths.ToArray();
        }

        private static bool CardsMatch(
            D3Card[] before, D3Card[] now, bool allowApprovedVisuals)
        {
            if (!allowApprovedVisuals)
                return Serialize(before) == Serialize(now);
            if (before.Length != now.Length) return false;
            for (int i = 0; i < before.Length; i++)
            {
                D3Card a = before[i];
                D3Card b = now[i];
                if (a.path != b.path ||
                    a.destination != b.destination ||
                    a.button != b.button ||
                    a.background != b.background ||
                    a.border != b.border ||
                    a.icon != b.icon ||
                    a.title != b.title ||
                    a.subtitle != b.subtitle)
                    return false;
            }
            return true;
        }

        private static bool ImagesMatch(
            D3Image[] before, D3Image[] now, bool allowApprovedVisuals)
        {
            if (!allowApprovedVisuals)
                return Serialize(before) == Serialize(now);
            if (before.Length != now.Length) return false;
            for (int i = 0; i < before.Length; i++)
            {
                D3Image a = before[i];
                D3Image b = now[i];
                if (a.path != b.path || a.identity != b.identity)
                    return false;
                bool approvedSprite =
                    a.path.EndsWith("/Icon", StringComparison.Ordinal) ||
                    a.path.EndsWith("/Chevron", StringComparison.Ordinal);
                if (!approvedSprite && a.sprite != b.sprite)
                    return false;
            }
            return true;
        }

        private static D3HomeData CaptureCurrent()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomePath);
            Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);
            D3ObjectComponents[] components = transforms.Select(transform =>
                new D3ObjectComponents
                {
                    path = PathOf(transform, prefab.transform),
                    types = transform.GetComponents<Component>()
                        .Where(component => component != null)
                        .Select(component => component.GetType().FullName)
                        .OrderBy(value => value).ToArray()
                }).OrderBy(record => record.path).ToArray();

            D3Button[] buttons = prefab.GetComponentsInChildren<Button>(true)
                .Select(button => new D3Button
                {
                    path = PathOf(button.transform, prefab.transform),
                    identity = Identity(button),
                    targetGraphic = Identity(button.targetGraphic),
                    listeners = Enumerable.Range(
                            0, button.onClick.GetPersistentEventCount())
                        .Select(index =>
                            Identity(button.onClick.GetPersistentTarget(index)) +
                            "::" +
                            button.onClick.GetPersistentMethodName(index))
                        .ToArray()
                }).OrderBy(record => record.path).ToArray();

            D3Card[] cards = prefab.GetComponentsInChildren<HomePanelCard>(true)
                .Select(CaptureCard).OrderBy(record => record.path).ToArray();
            D3Image[] images = prefab.GetComponentsInChildren<Image>(true)
                .Select(image => new D3Image
                {
                    path = PathOf(image.transform, prefab.transform),
                    identity = Identity(image),
                    sprite = Identity(image.sprite)
                }).OrderBy(record => record.path).ToArray();

            NorscopePanelRoot panel = prefab.GetComponent<NorscopePanelRoot>();
            SerializedObject panelSo = new SerializedObject(panel);
            D3PanelRoot panelRoot = new D3PanelRoot
            {
                theme = Ref(panelSo, "theme"),
                worldSpaceConfig = Ref(panelSo, "worldSpaceConfig"),
                root = Ref(panelSo, "root"),
                background = Ref(panelSo, "background"),
                border = Ref(panelSo, "border"),
                title = Ref(panelSo, "title")
            };

            string[] sourcePaths = Directory.GetFiles(
                    "Assets/Norscope/Runtime", "*.cs", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/')).OrderBy(path => path).ToArray();
            return new D3HomeData
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                hierarchy = transforms.Select(transform =>
                        PathOf(transform, prefab.transform))
                    .OrderBy(value => value).ToArray(),
                components = components,
                buttons = buttons,
                cards = cards,
                images = images,
                panelRoot = panelRoot,
                sources = sourcePaths.Select(path =>
                    new D3Source { path = path, hash = Hash(path) }).ToArray()
            };
        }

        private static D3Card CaptureCard(HomePanelCard card)
        {
            SerializedObject so = new SerializedObject(card);
            Image icon = so.FindProperty("icon")?.objectReferenceValue as Image;
            return new D3Card
            {
                path = PathOf(card.transform,
                    card.transform.root),
                destination = so.FindProperty("targetPanel")?.enumValueIndex ?? -1,
                button = Ref(so, "button"),
                background = Ref(so, "background"),
                border = Ref(so, "border"),
                icon = Ref(so, "icon"),
                title = Ref(so, "title"),
                subtitle = Ref(so, "subtitle"),
                iconSprite = Identity(icon != null ? icon.sprite : null)
            };
        }

        private static bool PanelRootMatches(
            D3PanelRoot before, D3PanelRoot now, bool allowTitleClear)
        {
            return before.theme == now.theme &&
                   before.worldSpaceConfig == now.worldSpaceConfig &&
                   before.root == now.root &&
                   before.background == now.background &&
                   before.border == now.border &&
                   (allowTitleClear ? string.IsNullOrEmpty(now.title) :
                       before.title == now.title);
        }

        private static string Ref(SerializedObject so, string property)
        {
            return Identity(so.FindProperty(property)?.objectReferenceValue);
        }

        private static string Identity(UnityEngine.Object value)
        {
            if (value == null) return string.Empty;
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                value, out string guid, out long localId)
                ? guid + ":" + localId
                : value.GetType().FullName + ":" + value.name;
        }

        private static string PathOf(Transform value, Transform root)
        {
            var names = new Stack<string>();
            Transform current = value;
            while (current != null)
            {
                names.Push(current.name);
                if (current == root) break;
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return string.Concat(sha.ComputeHash(stream)
                    .Select(value => value.ToString("x2")));
            }
        }

        private static string Serialize<T>(T[] values)
        {
            return string.Join("|", values.Select(value =>
                JsonUtility.ToJson(value)));
        }

        private static D3HomeData Load()
        {
            return File.Exists(BaselinePath)
                ? JsonUtility.FromJson<D3HomeData>(File.ReadAllText(BaselinePath))
                : null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
