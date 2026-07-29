using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Norscope.Windows
{
    /// <summary>
    /// World-space title-bar grab volume (same Grab-layer physics path as the cube/sphere).
    /// Kept as a child of the unscaled window root so canvas 0.001 scale cannot shrink the collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpatialWindowHandle : MonoBehaviour
    {
        [SerializeField] private SpatialWindow window;
        [SerializeField] private Image visual;
        [SerializeField] private RectTransform titleBar;
        [SerializeField] private RectTransform closeButtonRect;
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private BoxCollider box;
        [SerializeField] private Color normalColor = new Color(0.149f, 0.188f, 0.255f, 0.65f);
        [SerializeField] private Color hoverColor = new Color(0.133f, 0.827f, 0.933f, 0.45f);
        [SerializeField] private Color grabbedColor = new Color(0.133f, 0.827f, 0.933f, 0.85f);

        private bool _grabbed;
        private Transform _follow;
        private Vector3 _grabLocalOffset;
        private Quaternion _grabRotationOffset;

        public bool IsGrabbing => _grabbed;

        /// <summary>
        /// Build or refresh a world-space grab volume under the window root.
        /// </summary>
        public void Initialize(SpatialWindow owner, RectTransform titleBarRect, RectTransform closeRect, Image grabVisual)
        {
            window = owner;
            titleBar = titleBarRect;
            closeButtonRect = closeRect;
            visual = grabVisual;
            EnsureInteractable();

            if (visual != null)
            {
                visual.raycastTarget = false;
                ApplyColor(normalColor);
            }

            // Title bar chrome should not steal UI pinch from the grab volume.
            if (titleBar != null)
            {
                Image titleImage = titleBar.GetComponent<Image>();
                if (titleImage != null)
                {
                    titleImage.raycastTarget = false;
                }
            }

            SyncVolumeToTitleBar();
        }

        private void OnEnable()
        {
            EnsureInteractable();
            if (interactable == null)
            {
                return;
            }

            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            if (interactable == null)
            {
                return;
            }

            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
            EndGrab();
        }

        private void LateUpdate()
        {
            if (!_grabbed)
            {
                SyncVolumeToTitleBar();
                return;
            }

            if (window == null || _follow == null || window.IsResizing)
            {
                return;
            }

            Transform root = window.WindowRoot;
            float sharpness = window.Config != null ? window.Config.moveFollowSharpness : 18f;
            Vector3 targetPos = _follow.TransformPoint(_grabLocalOffset);
            Quaternion targetRot = _follow.rotation * _grabRotationOffset;

            Vector3 flatFwd = targetRot * Vector3.forward;
            flatFwd.y = 0f;
            if (flatFwd.sqrMagnitude > 0.0001f)
            {
                targetRot = Quaternion.LookRotation(flatFwd.normalized, Vector3.up);
            }

            float t = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            root.SetPositionAndRotation(
                Vector3.Lerp(root.position, targetPos, t),
                Quaternion.Slerp(root.rotation, targetRot, t));
        }

        private void EnsureInteractable()
        {
            int grabLayer = LayerMask.NameToLayer("Grab");
            if (grabLayer >= 0)
            {
                gameObject.layer = grabLayer;
            }

            if (box == null)
            {
                box = GetComponent<BoxCollider>();
            }

            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider>();
            }

            box.isTrigger = false;

            if (interactable == null)
            {
                interactable = GetComponent<XRSimpleInteractable>();
            }

            if (interactable == null)
            {
                interactable = gameObject.AddComponent<XRSimpleInteractable>();
            }

            // Match cube/sphere: allow far-ray and near pinch select.
            interactable.selectMode = InteractableSelectMode.Multiple;
            if (interactable.colliders.Count == 0 || !interactable.colliders.Contains(box))
            {
                interactable.colliders.Clear();
                interactable.colliders.Add(box);
            }
        }

        private void SyncVolumeToTitleBar()
        {
            if (window == null || box == null)
            {
                return;
            }

            float width = 0.45f;
            float height = 0.055f;
            float depth = 0.04f;
            Vector3 worldCenter;
            Quaternion worldRot = window.WindowRoot.rotation;

            if (titleBar != null)
            {
                Canvas.ForceUpdateCanvases();
                Bounds b = RectTransformToWorldBounds(titleBar);
                width = Mathf.Clamp(b.size.x * 0.85f, 0.20f, 0.70f);
                height = Mathf.Clamp(b.size.y * 1.1f, 0.045f, 0.10f);
                depth = 0.04f;
                worldCenter = b.center - window.WindowRoot.forward * 0.01f;

                if (closeButtonRect != null)
                {
                    Bounds closeBounds = RectTransformToWorldBounds(closeButtonRect);
                    float reservedWidth = Mathf.Clamp(closeBounds.size.x * 1.8f, 0.06f, 0.18f);
                    float leftShift = reservedWidth * 0.5f;
                    width = Mathf.Clamp(width - reservedWidth, 0.16f, 0.60f);
                    worldCenter -= window.WindowRoot.right * leftShift;
                }
            }
            else
            {
                // Fallback: strip across the top of the panel.
                worldCenter = window.WindowRoot.position
                              + window.WindowRoot.up * 0.14f
                              - window.WindowRoot.forward * 0.01f;
            }

            // Volume lives under window root at scale 1 — set world pose then convert.
            transform.SetPositionAndRotation(worldCenter, worldRot);
            transform.localScale = Vector3.one;

            // Collider size in local metres (parent scale is 1).
            box.size = new Vector3(width, height, depth);
            box.center = Vector3.zero;
        }

        private static Bounds RectTransformToWorldBounds(RectTransform rt)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Bounds bounds = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++)
            {
                bounds.Encapsulate(corners[i]);
            }

            return bounds;
        }

        private void OnHoverEntered(HoverEnterEventArgs _)
        {
            if (!_grabbed)
            {
                ApplyColor(hoverColor);
            }
        }

        private void OnHoverExited(HoverExitEventArgs _)
        {
            if (!_grabbed)
            {
                ApplyColor(normalColor);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (window == null || window.IsResizing)
            {
                return;
            }

            window.BeginMove(this);
            _grabbed = true;
            _follow = args.interactorObject.GetAttachTransform(interactable);
            Transform root = window.WindowRoot;
            _grabLocalOffset = _follow.InverseTransformPoint(root.position);
            _grabRotationOffset = Quaternion.Inverse(_follow.rotation) * root.rotation;
            ApplyColor(grabbedColor);
            Debug.Log("[WINDOW] InteractionTestPanel grab started");
        }

        private void OnSelectExited(SelectExitEventArgs _)
        {
            EndGrab();
        }

        private void EndGrab()
        {
            if (!_grabbed)
            {
                return;
            }

            _grabbed = false;
            _follow = null;
            if (window != null)
            {
                window.EndMove(this);
            }

            ApplyColor(normalColor);
            Debug.Log("[WINDOW] InteractionTestPanel grab released");
        }

        private void ApplyColor(Color c)
        {
            if (visual != null)
            {
                visual.color = c;
            }
        }
    }
}
