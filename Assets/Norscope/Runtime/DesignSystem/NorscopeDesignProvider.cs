using System;
using UnityEngine;

namespace Norscope.DesignSystem
{
    /// <summary>Presentation-only access point for the active master design profile.</summary>
    [DisallowMultipleComponent]
    public sealed class NorscopeDesignProvider : MonoBehaviour
    {
        public static NorscopeDesignProvider Instance { get; private set; }

        [SerializeField] private NorscopeDesignProfile activeProfile;

        public NorscopeDesignProfile ActiveProfile => activeProfile;
        public event Action<NorscopeDesignProfile> ProfileChanged;

        public void SetProfile(NorscopeDesignProfile profile)
        {
            if (activeProfile == profile) return;
            activeProfile = profile;
            ProfileChanged?.Invoke(activeProfile);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DESIGN] Duplicate NorscopeDesignProvider disabled.");
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
