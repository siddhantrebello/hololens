using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Norscope.Windows
{
    /// <summary>
    /// TEMPORARY Phase 4 test launcher. Not the final Home launcher.
    /// Keyboard uses Input System (project is Input System-only).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TemporarySpatialWindowLauncher : MonoBehaviour
    {
        [SerializeField] private SpatialWindowManager manager;
        [SerializeField] private SpatialWindowId targetId = SpatialWindowId.InteractionTestPanel;
        [SerializeField] private bool createWorldButtons = true;
        [SerializeField] private bool openOnStart = true;
        [Tooltip("World position of the TEMP Open/Close/Recenter buttons. Raise Y to move them higher.")]
        [SerializeField] private Vector3 tempLauncherWorldPosition = new Vector3(-0.45f, 1.35f, 0.85f);

        private void Awake()
        {
            if (manager == null)
            {
                manager = FindFirstObjectByType<SpatialWindowManager>();
            }

            if (createWorldButtons)
            {
                CreateTempButtons();
            }
        }

        private void Start()
        {
            if (openOnStart && manager != null)
            {
                Open();
            }
        }

        private void Update()
        {
            if (manager == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.oKey.wasPressedThisFrame)
            {
                Open();
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                Close();
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                Recenter();
            }
        }

        public void Open()
        {
            manager.Open(targetId);
        }

        public void Close()
        {
            manager.Close(targetId);
        }

        public void Recenter()
        {
            manager.Recenter(targetId);
        }

        private void CreateTempButtons()
        {
            if (GameObject.Find("TEMP_SpatialWindowLauncher") != null)
            {
                return;
            }

            GameObject canvasGo = new GameObject("TEMP_SpatialWindowLauncher");
            canvasGo.transform.position = tempLauncherWorldPosition;
            canvasGo.transform.localScale = Vector3.one * 0.001f;
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0)
            {
                canvasGo.layer = ui;
            }

            RectTransform rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(360f, 220f);

            CreateButton(canvasGo.transform, "TEMP Open Panel", new Vector2(0f, 60f), Open);
            CreateButton(canvasGo.transform, "TEMP Close Panel", new Vector2(0f, 0f), Close);
            CreateButton(canvasGo.transform, "TEMP Recenter Panel", new Vector2(0f, -60f), Recenter);
        }

        private static void CreateButton(Transform parent, string label, Vector2 anchored, UnityEngine.Events.UnityAction action)
        {
            GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0)
            {
                go.layer = ui;
            }

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300f, 48f);
            rt.anchoredPosition = anchored;

            Image image = go.GetComponent<Image>();
            image.color = new Color(0.18f, 0.32f, 0.42f, 0.95f);

            Button button = go.GetComponent<Button>();
            button.onClick.AddListener(action);

            GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            if (ui >= 0)
            {
                textGo.layer = ui;
            }

            Text text = textGo.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 22;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
        }
    }
}
