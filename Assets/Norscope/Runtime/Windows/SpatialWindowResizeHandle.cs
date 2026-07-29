using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Norscope.Windows
{
    /// <summary>
    /// Corner resize via a world-space Grab-layer volume (not canvas-scaled collider).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpatialWindowResizeHandle : MonoBehaviour
    {
        [SerializeField] private SpatialWindow window;
        [SerializeField] private Image visual;
        [SerializeField] private RectTransform cornerRect;
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private BoxCollider box;
        [SerializeField] private Color normalColor = new Color(0.133f, 0.827f, 0.933f, 0.35f);
        [SerializeField] private Color hoverColor = new Color(0.133f, 0.827f, 0.933f, 0.75f);
        [SerializeField] private Color activeColor = new Color(0.133f, 0.827f, 0.933f, 1f);

        private bool _resizing;
        private Transform _follow;
        private float _startDistance;
        private float _startScale;

        public bool IsResizing => _resizing;

        public void Initialize(SpatialWindow owner, RectTransform corner, Image handleVisual)
        {
            window = owner;
            cornerRect = corner;
            visual = handleVisual;
            EnsureInteractable();

            bool visible = owner != null && owner.Config != null ? owner.Config.handlesVisible : true;
            if (visual != null)
            {
                visual.raycastTarget = false;
                Color c = normalColor;
                c.a = visible ? normalColor.a : 0.05f;
                visual.color = c;
            }

            SyncVolumeToCorner();
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
            EndResize();
        }

        private void LateUpdate()
        {
            if (!_resizing)
            {
                SyncVolumeToCorner();
                return;
            }

            if (window == null || _follow == null || window.IsMoving)
            {
                return;
            }

            float dist = Vector3.Distance(window.WindowRoot.position, _follow.position);
            float sensitivity = window.Config != null ? window.Config.resizeSensitivity : 1f;
            float ratio = (_startDistance > 0.001f) ? (dist / _startDistance) : 1f;
            float targetScale = _startScale * Mathf.Lerp(1f, ratio, sensitivity);
            window.SetUniformScale(targetScale);
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

            interactable.selectMode = InteractableSelectMode.Multiple;
            if (interactable.colliders.Count == 0 || !interactable.colliders.Contains(box))
            {
                interactable.colliders.Clear();
                interactable.colliders.Add(box);
            }
        }

        private void SyncVolumeToCorner()
        {
            if (window == null || box == null)
            {
                return;
            }

            float sizeMeters = window.Config != null ? window.Config.handleWorldSizeMeters : 0.03f;
            sizeMeters = Mathf.Clamp(sizeMeters, 0.02f, 0.06f);

            Vector3 worldCenter;
            Quaternion worldRot = window.WindowRoot.rotation;
            if (cornerRect != null)
            {
                Vector3[] corners = new Vector3[4];
                cornerRect.GetWorldCorners(corners);
                worldCenter = (corners[0] + corners[2]) * 0.5f;
                worldCenter -= window.WindowRoot.forward * 0.01f;
            }
            else
            {
                worldCenter = window.WindowRoot.position
                              - window.WindowRoot.up * 0.14f
                              + window.WindowRoot.right * 0.22f
                              - window.WindowRoot.forward * 0.01f;
            }

            transform.SetPositionAndRotation(worldCenter, worldRot);
            transform.localScale = Vector3.one;
            box.size = new Vector3(sizeMeters, sizeMeters, sizeMeters);
            box.center = Vector3.zero;
        }

        private void OnHoverEntered(HoverEnterEventArgs _)
        {
            if (!_resizing)
            {
                ApplyColor(hoverColor);
            }
        }

        private void OnHoverExited(HoverExitEventArgs _)
        {
            if (!_resizing)
            {
                ApplyColor(normalColor);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (window == null || window.IsMoving)
            {
                return;
            }

            window.BeginResize(this);
            _resizing = true;
            _follow = args.interactorObject.GetAttachTransform(interactable);
            _startDistance = Vector3.Distance(window.WindowRoot.position, _follow.position);
            _startScale = window.CurrentScale;
            ApplyColor(activeColor);
        }

        private void OnSelectExited(SelectExitEventArgs _)
        {
            EndResize();
        }

        private void EndResize()
        {
            if (!_resizing)
            {
                return;
            }

            _resizing = false;
            _follow = null;
            if (window != null)
            {
                window.EndResize(this);
            }

            ApplyColor(normalColor);
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
