using System.Collections.Generic;
using Norscope.Windows;
using UnityEngine;

namespace Norscope.Navigation
{
    /// <summary>
    /// Coordinates parent/child panel transitions through the existing registry and window
    /// manager. It never instantiates panels directly and never modifies authored UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PanelNavigationService : MonoBehaviour
    {
        public static PanelNavigationService Instance { get; private set; }

        [SerializeField] private PanelRegistry panelRegistry;
        [SerializeField] private SpatialWindowManager windowManager;

        private readonly Dictionary<PanelType, PanelType> _runtimeParents =
            new Dictionary<PanelType, PanelType>();

        public void SetReferences(PanelRegistry registry, SpatialWindowManager manager)
        {
            panelRegistry = registry;
            windowManager = manager;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[NAV] Duplicate PanelNavigationService destroyed.");
                Destroy(this);
                return;
            }

            Instance = this;
            if (panelRegistry == null) panelRegistry = GetComponent<PanelRegistry>();
            if (windowManager == null) windowManager = GetComponent<SpatialWindowManager>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Opens child first. Only after success is its runtime parent recorded and every other
        /// primary panel hidden. Existing SpatialWindow instances are retained for reuse.
        /// </summary>
        public SpatialWindow Navigate(PanelType parent, PanelType child)
        {
            if (!CanNavigate(parent, child)) return null;

            SpatialWindow childWindow = panelRegistry.Open(child);
            if (childWindow == null || !childWindow.IsOpen)
            {
                Debug.LogWarning("[NAV] Navigation to " + child +
                                 " failed; current panel remains visible.");
                return null;
            }

            _runtimeParents[child] = parent;
            HideAllPrimaryPanelsExcept(child);
            return childWindow;
        }

        /// <summary>
        /// Runtime-recorded parent always wins. The current panel is hidden only after its
        /// parent has reopened successfully.
        /// </summary>
        public bool Back(PanelType current, PanelType fallbackParent = PanelType.None)
        {
            PanelType parent = ResolveParent(current, fallbackParent);
            if (parent == PanelType.None)
            {
                Debug.LogWarning("[NAV] Back ignored for " + current +
                                 ": no runtime or fallback parent is available.");
                return false;
            }

            if (panelRegistry == null || windowManager == null)
            {
                Debug.LogError("[NAV] Back failed: navigation service references are missing.");
                return false;
            }

            SpatialWindow parentWindow = panelRegistry.Open(parent);
            if (parentWindow == null || !parentWindow.IsOpen)
            {
                Debug.LogWarning("[NAV] Back from " + current + " failed because parent " +
                                 parent + " could not be opened. Current panel remains visible.");
                return false;
            }

            HideAllPrimaryPanelsExcept(parent);
            return true;
        }

        public bool TryGetRuntimeParent(PanelType child, out PanelType parent)
        {
            return _runtimeParents.TryGetValue(child, out parent);
        }

        public PanelType ResolveParent(PanelType current, PanelType fallbackParent)
        {
            if (_runtimeParents.TryGetValue(current, out PanelType runtimeParent) &&
                runtimeParent != PanelType.None)
            {
                return runtimeParent;
            }

            return fallbackParent;
        }

        private bool CanNavigate(PanelType parent, PanelType child)
        {
            if (parent == PanelType.None || child == PanelType.None || parent == child)
            {
                Debug.LogWarning("[NAV] Invalid navigation request: " + parent + " -> " + child);
                return false;
            }

            if (panelRegistry == null || windowManager == null)
            {
                Debug.LogError("[NAV] Navigation failed: service references are missing.");
                return false;
            }

            return true;
        }

        private void HideAllPrimaryPanelsExcept(PanelType panelToKeep)
        {
            IReadOnlyList<PanelRegistryEntry> entries = panelRegistry.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                PanelRegistryEntry entry = entries[i];
                if (entry == null ||
                    entry.panelType == PanelType.None ||
                    entry.panelType == panelToKeep ||
                    entry.behaviour == PanelBehaviourType.HeadFollowing ||
                    entry.windowId == SpatialWindowId.None)
                {
                    continue;
                }

                if (windowManager.HasOpen(entry.windowId))
                {
                    windowManager.Close(entry.windowId);
                }
            }
        }
    }
}
