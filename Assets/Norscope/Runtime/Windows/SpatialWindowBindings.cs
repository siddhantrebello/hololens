using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Windows
{
    /// <summary>
    /// Prefab-authored references for SpatialWindow wiring.
    /// Keeps runtime independent from child object names.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpatialWindowBindings : MonoBehaviour
    {
        [SerializeField] private Transform grabRegion;
        [SerializeField] private RectTransform resizeHandle;
        [SerializeField] private Transform closeButtonRoot;

        public Transform GrabRegion => grabRegion;
        public RectTransform ResizeHandle => resizeHandle;
        public Button CloseButton => closeButtonRoot != null ? closeButtonRoot.GetComponentInChildren<Button>(true) : null;
        public RectTransform CloseButtonRect => closeButtonRoot as RectTransform;

#if UNITY_EDITOR
        [ContextMenu("Auto-Fill From Names")]
        private void AutoFillFromNames()
        {
            if (grabRegion == null)
                grabRegion = FindDeep(transform, "GrabRegion");
            if (resizeHandle == null)
                resizeHandle = FindDeep(transform, "BottomRight") as RectTransform;
            if (closeButtonRoot == null)
                closeButtonRoot = FindDeep(transform, "CloseButton");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
#endif
    }
}
