using System.Collections.Generic;
using UnityEngine;
using Norscope.Windows;

namespace Norscope.Navigation
{
    /// <summary>
    /// Resolves PanelType → prefab / behaviour. Home Launcher calls Open(Home);
    /// no direct prefab references on callers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PanelRegistry : MonoBehaviour
    {
        public static PanelRegistry Instance { get; private set; }

        [SerializeField] private SpatialWindowManager windowManager;
        [SerializeField] private PanelRegistryEntry[] entries = System.Array.Empty<PanelRegistryEntry>();

        private readonly Dictionary<PanelType, PanelRegistryEntry> _byType =
            new Dictionary<PanelType, PanelRegistryEntry>();
        private readonly Dictionary<SpatialWindowId, PanelRegistryEntry> _byWindowId =
            new Dictionary<SpatialWindowId, PanelRegistryEntry>();

        public IReadOnlyList<PanelRegistryEntry> Entries => entries;

        public void SetReferences(SpatialWindowManager manager, PanelRegistryEntry[] registryEntries)
        {
            windowManager = manager;
            entries = registryEntries ?? System.Array.Empty<PanelRegistryEntry>();
            RebuildLookup();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[NAV] Duplicate PanelRegistry destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (windowManager == null)
            {
                windowManager = GetComponent<SpatialWindowManager>();
            }

            RebuildLookup();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void RebuildLookup()
        {
            _byType.Clear();
            _byWindowId.Clear();
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                PanelRegistryEntry entry = entries[i];
                if (entry == null || entry.panelType == PanelType.None)
                {
                    continue;
                }

                if (_byType.ContainsKey(entry.panelType))
                {
                    Debug.LogError("[NAV] Duplicate PanelType in registry: " + entry.panelType);
                    continue;
                }

                _byType[entry.panelType] = entry;

                if (entry.windowId != SpatialWindowId.None)
                {
                    if (_byWindowId.ContainsKey(entry.windowId))
                    {
                        Debug.LogError("[NAV] Duplicate SpatialWindowId in registry: " + entry.windowId);
                        continue;
                    }

                    _byWindowId[entry.windowId] = entry;
                }
            }
        }

        public bool TryGetEntry(PanelType type, out PanelRegistryEntry entry)
        {
            return _byType.TryGetValue(type, out entry);
        }

        public bool TryGetEntry(SpatialWindowId id, out PanelRegistryEntry entry)
        {
            return _byWindowId.TryGetValue(id, out entry);
        }

        public GameObject GetPrefab(SpatialWindowId id)
        {
            if (_byWindowId.TryGetValue(id, out PanelRegistryEntry entry) && entry != null)
            {
                return entry.prefab;
            }

            return null;
        }

        public PanelBehaviourType GetBehaviour(PanelType type)
        {
            if (_byType.TryGetValue(type, out PanelRegistryEntry entry) && entry != null)
            {
                return entry.behaviour;
            }

            return PanelBehaviourType.WorldLocked;
        }

        /// <summary>
        /// Opens a panel according to its behaviour. Never duplicates.
        /// Placeholder entries (null prefab) only log.
        /// </summary>
        public SpatialWindow Open(PanelType type)
        {
            if (type == PanelType.None)
            {
                return null;
            }

            if (!_byType.TryGetValue(type, out PanelRegistryEntry entry) || entry == null)
            {
                Debug.LogWarning("[NAV] PanelType not registered: " + type);
                return null;
            }

            if (entry.behaviour == PanelBehaviourType.HeadFollowing)
            {
                Debug.LogWarning("[NAV] Open ignored for HeadFollowing type: " + type);
                return null;
            }

            if (entry.prefab == null)
            {
                Debug.Log("[NAV] Opening " + type + " — placeholder (no prefab yet)");
                return null;
            }

            if (windowManager == null)
            {
                Debug.LogError("[NAV] SpatialWindowManager missing on PanelRegistry.");
                return null;
            }

            if (entry.windowId == SpatialWindowId.None)
            {
                Debug.LogError("[NAV] PanelType " + type + " has no SpatialWindowId.");
                return null;
            }

            switch (entry.behaviour)
            {
                case PanelBehaviourType.RecenterOnOpen:
                    // Spawn if missing, or bring existing (open or closed) to front.
                    windowManager.Recenter(entry.windowId);
                    return windowManager.Get(entry.windowId);

                case PanelBehaviourType.WorldLocked:
                default:
                    return windowManager.Open(entry.windowId);
            }
        }
    }
}
