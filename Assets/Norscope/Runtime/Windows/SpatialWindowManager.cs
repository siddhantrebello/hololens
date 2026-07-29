using System.Collections.Generic;
using UnityEngine;

namespace Norscope.Windows
{
    /// <summary>
    /// Tracks open windows, prevents duplicate IDs, open/close/recenter.
    /// Does not generate UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpatialWindowManager : MonoBehaviour
    {
        public static SpatialWindowManager Instance { get; private set; }

        [SerializeField] private SpatialWindowSpawner spawner;
        [SerializeField] private SpatialWindowConfig config;

        private readonly Dictionary<SpatialWindowId, SpatialWindow> _windows =
            new Dictionary<SpatialWindowId, SpatialWindow>();

        public void SetReferences(SpatialWindowSpawner windowSpawner, SpatialWindowConfig windowConfig)
        {
            spawner = windowSpawner;
            config = windowConfig;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[WINDOW] Duplicate SpatialWindowManager destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (spawner == null)
            {
                spawner = GetComponent<SpatialWindowSpawner>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public SpatialWindow Open(SpatialWindowId id)
        {
            if (id == SpatialWindowId.None)
            {
                return null;
            }

            if (_windows.TryGetValue(id, out SpatialWindow existing) && existing != null)
            {
                if (!existing.IsOpen)
                {
                    // Reopen at last world pose (no duplicate, no forced recenter).
                    existing.Show();
                    Debug.Log("[WINDOW] Open reactivated existing instance id=" + id);
                }
                else
                {
                    Debug.Log("[WINDOW] Open ignored — already open id=" + id);
                }

                return existing;
            }

            // Remove stale null refs.
            if (_windows.ContainsKey(id) && _windows[id] == null)
            {
                _windows.Remove(id);
            }

            if (spawner == null)
            {
                Debug.LogError("[WINDOW] SpatialWindowSpawner missing.");
                return null;
            }

            SpatialWindow created = spawner.Spawn(id, config != null ? config : spawner.Config);
            if (created == null)
            {
                return null;
            }

            created.Closed += OnWindowClosed;
            _windows[id] = created;
            return created;
        }

        public void Close(SpatialWindowId id)
        {
            if (_windows.TryGetValue(id, out SpatialWindow window) && window != null)
            {
                window.Close();
            }
        }

        public void Recenter(SpatialWindowId id)
        {
            if (_windows.TryGetValue(id, out SpatialWindow window) && window != null)
            {
                if (!window.IsOpen)
                {
                    window.Show();
                }

                window.Recenter(GetCameraTransform());
                return;
            }

            // No instance yet — open (spawn once).
            Open(id);
        }

        public bool HasOpen(SpatialWindowId id)
        {
            return _windows.TryGetValue(id, out SpatialWindow w) && w != null && w.IsOpen;
        }

        public SpatialWindow Get(SpatialWindowId id)
        {
            _windows.TryGetValue(id, out SpatialWindow w);
            return w;
        }

        private void OnWindowClosed(SpatialWindow window)
        {
            // Keep dictionary entry for reuse (deactivated instance).
        }

        private static Transform GetCameraTransform()
        {
            Camera cam = Camera.main;
            return cam != null ? cam.transform : null;
        }
    }
}
