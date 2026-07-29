using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Norscope.Navigation;

namespace Norscope.Windows
{
    /// <summary>
    /// Instantiates a registry-resolved panel prefab under a world-space canvas host.
    /// Does not generate control hierarchies — only hosts the authored prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpatialWindowSpawner : MonoBehaviour
    {
        [SerializeField] private SpatialWindowConfig config;
        [SerializeField] private PanelRegistry panelRegistry;
        [SerializeField] private GameObject interactionTestPanelPrefab;

        public SpatialWindowConfig Config => config;

        public void SetReferences(SpatialWindowConfig windowConfig, GameObject panelPrefab)
        {
            config = windowConfig;
            interactionTestPanelPrefab = panelPrefab;
        }

        public void SetReferences(SpatialWindowConfig windowConfig, PanelRegistry registry, GameObject fallbackPrefab)
        {
            config = windowConfig;
            panelRegistry = registry;
            interactionTestPanelPrefab = fallbackPrefab;
        }

        public SpatialWindow Spawn(SpatialWindowId id, SpatialWindowConfig windowConfig)
        {
            if (windowConfig == null)
            {
                windowConfig = config;
            }

            if (windowConfig == null)
            {
                Debug.LogError("[WINDOW] SpatialWindowConfig missing.");
                return null;
            }

            GameObject panelPrefab = ResolvePrefab(id);
            if (panelPrefab == null)
            {
                Debug.LogError("[WINDOW] Panel prefab missing for id=" + id);
                return null;
            }

            Pose pose = windowConfig.GetSpawnPose(Camera.main != null ? Camera.main.transform : null);

            GameObject root = new GameObject("SpatialWindow_" + id);
            root.transform.SetPositionAndRotation(pose.position, pose.rotation);

            GameObject canvasGo = new GameObject("WindowCanvas");
            canvasGo.transform.SetParent(root.transform, false);
            canvasGo.transform.localPosition = Vector3.zero;
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = Vector3.one * windowConfig.CanvasLocalScale;

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                canvasGo.layer = uiLayer;
            }

            RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(
                windowConfig.defaultWidthMeters * windowConfig.pixelsPerMeter,
                windowConfig.defaultHeightMeters * windowConfig.pixelsPerMeter);

            GameObject panelInstance = Instantiate(panelPrefab, canvasGo.transform, false);
            panelInstance.name = panelPrefab.name;
            RectTransform panelRt = panelInstance.GetComponent<RectTransform>();
            SpatialWindowBindings bindings = panelInstance.GetComponent<SpatialWindowBindings>();
            if (panelRt != null)
            {
                panelRt.anchoredPosition = Vector2.zero;
                panelRt.localRotation = Quaternion.identity;
                panelRt.localScale = Vector3.one;

                // Prefer authored panel size for canvas host.
                float w = panelRt.rect.width > 1f ? panelRt.rect.width : panelRt.sizeDelta.x;
                float h = panelRt.rect.height > 1f ? panelRt.rect.height : panelRt.sizeDelta.y;
                if (w > 1f && h > 1f)
                {
                    canvasRt.sizeDelta = new Vector2(w, h);
                }
            }

            if (uiLayer >= 0)
            {
                foreach (Transform t in panelInstance.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = uiLayer;
                }
            }

            SpatialWindow window = root.AddComponent<SpatialWindow>();
            window.Initialize(id, windowConfig, panelRt, bindings);

            Phase3UiPokeSupport pokeSupport = FindFirstObjectByType<Phase3UiPokeSupport>();
            if (pokeSupport != null)
            {
                pokeSupport.RefreshUiCanvases();
            }

            return window;
        }

        private GameObject ResolvePrefab(SpatialWindowId id)
        {
            if (panelRegistry == null)
            {
                panelRegistry = GetComponent<PanelRegistry>();
                if (panelRegistry == null)
                {
                    panelRegistry = FindFirstObjectByType<PanelRegistry>();
                }
            }

            if (panelRegistry != null)
            {
                GameObject fromRegistry = panelRegistry.GetPrefab(id);
                if (fromRegistry != null)
                {
                    return fromRegistry;
                }
            }

            if (id == SpatialWindowId.InteractionTestPanel)
            {
                return interactionTestPanelPrefab;
            }

            return null;
        }
    }
}
