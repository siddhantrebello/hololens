using System;
using UnityEngine;

namespace Norscope.ArGuidance
{
    [Serializable]
    public sealed class ArHologramLibraryEntry
    {
        public string hologramId = string.Empty;
        public string displayName = string.Empty;
        public ArVisualType visualType = ArVisualType.None;
        public GameObject prefab;
        public Vector3 defaultLocalPosition;
        public Vector3 defaultLocalEuler;
        public Vector3 defaultLocalScale = Vector3.one;
        public bool isShared;
        [TextArea(1, 3)] public string notes;
    }

    /// <summary>
    /// Registry of reusable AR hologram prefabs for an equipment profile.
    /// Separate from calibration persistence — library owns authored defaults.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ArHologramLibrary",
        menuName = "Norscope/AR Guidance/Hologram Library")]
    public sealed class ArHologramLibrary : ScriptableObject
    {
        public string equipmentId = "dell_precision_3570";
        public string workflowId = "replace-battery";
        public ArHologramLibraryEntry[] entries = Array.Empty<ArHologramLibraryEntry>();

        public ArHologramLibraryEntry Find(string hologramId)
        {
            if (entries == null || string.IsNullOrEmpty(hologramId))
                return null;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].hologramId == hologramId)
                    return entries[i];
            }

            return null;
        }

        public GameObject GetPrefab(string hologramId)
        {
            ArHologramLibraryEntry e = Find(hologramId);
            return e != null ? e.prefab : null;
        }
    }
}
