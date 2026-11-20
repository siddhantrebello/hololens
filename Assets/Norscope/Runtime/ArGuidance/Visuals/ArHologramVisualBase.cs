using UnityEngine;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Base hologram visual binder. Prefab authors expose sizes/colors; runtime sets state.
    /// All appearance fields are Inspector-editable — do not hardcode per-task values in code.
    /// </summary>
    public abstract class ArHologramVisualBase : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string componentId;
        [SerializeField] private ArVisualType visualType;

        [Header("Transform (edit on root Transform / here as defaults)")]
        [SerializeField] private Vector3 authoredLocalPosition;
        [SerializeField] private Vector3 authoredLocalEuler;
        [SerializeField] private Vector3 authoredLocalScale = Vector3.one;

        [Header("Appearance (Inspector-editable)")]
        [SerializeField] private Sprite icon;
        [SerializeField] private SpriteRenderer iconRenderer;
        [Tooltip("Longest rendered dimension of the icon sprite, in metres. Applied to the " +
                 "icon's own dedicated child transform only — never this hologram's root — so " +
                 "it stays independent of calibration position/rotation/scale.")]
        [SerializeField, Range(0.010f, 0.050f)] private float iconSizeMeters = 0.030f;
        [SerializeField] private Color color = ArGuidancePalette.Cyan;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.85f;
        [SerializeField] private bool animationEnabled = false;
        [SerializeField] private float animationSpeed = 1f;
        [SerializeField] private string labelText;
        [SerializeField] private Vector3 labelOffset = new Vector3(0f, 0.06f, 0f);
        [SerializeField] private bool leaderLineEnabled = true;
        [SerializeField] private Vector3 leaderLineOffset = new Vector3(0f, 0.04f, 0f);
        [SerializeField] private bool startVisible;
        [SerializeField] private ArHologramVisualState authoredVisualState = ArHologramVisualState.Default;

        [Header("State colors")]
        [SerializeField] private Color defaultColor = ArGuidancePalette.Cyan;
        [SerializeField] private Color selectedColor = ArGuidancePalette.Selected;
        [SerializeField] private Color warningColor = ArGuidancePalette.Warning;
        [SerializeField] private Color dangerColor = ArGuidancePalette.Danger;
        [SerializeField] private Color completedColor = ArGuidancePalette.Completed;

        private ArHologramVisualState _state = ArHologramVisualState.Default;
        private Collider _manipulationCollider;
        private bool _visible = true;

        public string ComponentId => componentId;
        public ArVisualType VisualType => visualType;
        public ArHologramVisualState VisualState => _state;
        public bool AnimationEnabled => animationEnabled;
        public float AnimationSpeed => animationSpeed;
        public string LabelText => labelText;
        public Vector3 LabelOffset => labelOffset;
        public bool LeaderLineEnabled => leaderLineEnabled;
        public Vector3 LeaderLineOffset => leaderLineOffset;
        public bool IsVisible => _visible;
        public float Opacity => opacity;
        public Sprite Icon => icon;
        public float IconSizeMeters => iconSizeMeters;
        public Color DefaultColor => defaultColor;
        public Color DangerColor => dangerColor;
        public bool StartVisible => startVisible;

        public void ConfigureIdentity(string id, ArVisualType type)
        {
            componentId = id;
            visualType = type;
        }

        public virtual void SetVisible(bool visible)
        {
            _visible = visible;
            gameObject.SetActive(visible);
        }

        public virtual void SetVisualState(ArHologramVisualState state)
        {
            _state = state;
            color = ResolveStateColor(state);
            ApplyAppearance();
        }

        public virtual void SetOpacity(float value)
        {
            opacity = Mathf.Clamp01(value);
            ApplyAppearance();
        }

        public virtual void SetLabelText(string text)
        {
            labelText = text ?? string.Empty;
            ApplyAppearance();
            OnLabelChanged();
        }

        public virtual void SetIcon(Sprite sprite)
        {
            icon = sprite;
            ApplyIcon();
            ApplyAppearance();
        }

        public virtual void SetIconSizeMeters(float meters)
        {
            iconSizeMeters = Mathf.Clamp(meters, 0.010f, 0.050f);
            ApplyIconSize();
        }

        public virtual void SetAnimationEnabled(bool enabled)
        {
            animationEnabled = enabled;
        }

        public virtual void SetManipulationEnabled(bool enabled)
        {
            if (_manipulationCollider == null)
                _manipulationCollider = GetComponent<Collider>();
            if (_manipulationCollider != null)
                _manipulationCollider.enabled = enabled;
        }

        public void ApplyAuthoredTransform()
        {
            transform.localPosition = authoredLocalPosition;
            transform.localRotation = Quaternion.Euler(authoredLocalEuler);
            transform.localScale = authoredLocalScale;
            NotifyPoseApplied();
        }

        /// <summary>
        /// Called after library/calibration code sets this hologram's local pose.
        /// Animation subclasses must re-cache any rest/base pose so they don't
        /// overwrite the applied position on the next Update.
        /// </summary>
        public virtual void NotifyPoseApplied() { }

        protected Color ResolveStateColor(ArHologramVisualState state)
        {
            switch (state)
            {
                case ArHologramVisualState.Selected: return selectedColor;
                case ArHologramVisualState.Warning: return warningColor;
                case ArHologramVisualState.Danger: return dangerColor;
                case ArHologramVisualState.Completed: return completedColor;
                default: return defaultColor;
            }
        }

        protected Color CurrentColor
        {
            get
            {
                Color c = color;
                c.a = opacity;
                return c;
            }
        }

        public void RestoreDecorativeLayer()
        {
            EnsureIgnoreRaycastLayer();
            SetManipulationEnabled(false);
        }

        protected virtual void Awake()
        {
            EnsureIgnoreRaycastLayer();
            _state = authoredVisualState;
            color = ResolveStateColor(_state);
            ApplyIcon();
            ApplyAppearance();
            SetManipulationEnabled(false);
            if (!startVisible)
                SetVisible(false);
        }

        protected virtual void ApplyAppearance()
        {
            ApplyIconTint();
        }

        protected virtual void OnLabelChanged() { }

        protected void ApplyIcon()
        {
            if (iconRenderer == null)
                iconRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (iconRenderer == null)
                return;
            iconRenderer.sprite = icon;
            iconRenderer.enabled = icon != null;
            ApplyIconTint();
            ApplyIconSize();
        }

        /// <summary>Sizes the icon's own dedicated child transform so its longest rendered
        /// dimension equals iconSizeMeters, preserving the sprite's real aspect ratio (read
        /// from Sprite.bounds, not guessed). Only ever touches iconRenderer's own transform —
        /// never this hologram's root — so calibration position/rotation/scale can never
        /// inflate the icon, and resizing the icon can never move or resize the hologram.
        /// Not per-frame: only re-runs when appearance is (re)applied (Awake, SetIcon,
        /// SetIconSizeMeters, editor OnValidate).</summary>
        private void ApplyIconSize()
        {
            if (icon == null || iconRenderer == null)
                return;
            if (iconRenderer.transform == transform)
                return; // icon must live on its own dedicated child — never scale the root

            Vector2 spriteSize = icon.bounds.size;
            float longest = Mathf.Max(spriteSize.x, spriteSize.y);
            if (longest <= 0.0001f)
                return;

            float scale = iconSizeMeters / longest;
            Vector3 current = iconRenderer.transform.localScale;
            iconRenderer.transform.localScale = new Vector3(scale, scale, current.z);
        }

        protected void ApplyIconTint()
        {
            if (iconRenderer == null)
                return;
            iconRenderer.color = CurrentColor;
        }

        protected void EnsureIgnoreRaycastLayer()
        {
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            if (layer < 0)
                return;
            SetLayerRecursive(transform, layer);
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        protected static void ApplyLineColor(LineRenderer line, Color c)
        {
            if (line == null)
                return;
            line.startColor = c;
            line.endColor = c;
        }

        protected static void SetLeaderActive(LineRenderer leader, bool enabled)
        {
            if (leader == null)
                return;
            leader.enabled = enabled;
            if (leader.gameObject != null && leader.gameObject != leader.transform.root.gameObject)
                leader.gameObject.SetActive(enabled);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                _state = authoredVisualState;
                color = ResolveStateColor(_state);
            }

            ApplyIcon();
            ApplyAppearance();
        }
#endif
    }
}
