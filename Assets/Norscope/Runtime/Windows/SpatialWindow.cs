using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Norscope.Audio;
using Norscope.UI;

namespace Norscope.Windows
{
    /// <summary>
    /// Owns one InteractionTestPanel instance. Does not rebuild UI hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpatialWindow : MonoBehaviour
    {
        [SerializeField] private SpatialWindowId windowId = SpatialWindowId.InteractionTestPanel;
        [SerializeField] private SpatialWindowConfig config;
        [SerializeField] private Transform windowRoot;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private SpatialWindowBindings bindings;
        [SerializeField] private Transform grabRegion;
        [SerializeField] private RectTransform resizeHandle;
        [SerializeField] private Button closeButton;

        private SpatialWindowHandle _handle;
        private SpatialWindowResizeHandle _resize;
        private float _currentScale = 1f;
        private bool _loggedSpawn;
        private bool _isOpen = true;

        public SpatialWindowId WindowId => windowId;
        public SpatialWindowConfig Config => config;
        public Transform WindowRoot => windowRoot != null ? windowRoot : transform;
        public bool IsOpen => _isOpen && isActiveAndEnabled;
        public bool IsMoving { get; private set; }
        public bool IsResizing { get; private set; }
        public float CurrentScale => _currentScale;

        public event Action<SpatialWindow> Closed;

        public void Initialize(SpatialWindowId id, SpatialWindowConfig windowConfig, RectTransform panel, SpatialWindowBindings prefabBindings)
        {
            windowId = id;
            config = windowConfig;
            panelRoot = panel;
            windowRoot = transform;
            bindings = prefabBindings;
            ResolveChildReferences();
            ConfigureGraphicRaycastTargets();
            WireCloseButton();
            SetupHandles();
            ApplyDefaultSize();
            SetCanvasUiLayer();
            _isOpen = true;
            LogSpawnOnce();
            UIAudioManager.Play(UIAudioEvent.PanelOpen);
        }

        public void BindExisting(SpatialWindowConfig windowConfig, SpatialWindowBindings prefabBindings)
        {
            config = windowConfig;
            windowRoot = transform;
            bindings = prefabBindings;
            ResolveChildReferences();
            ConfigureGraphicRaycastTargets();
            WireCloseButton();
            SetupHandles();
            if (Mathf.Approximately(_currentScale, 0f) || _currentScale < 0.01f)
            {
                _currentScale = 1f;
            }

            SetCanvasUiLayer();
            _isOpen = gameObject.activeInHierarchy;
        }

        private void ConfigureGraphicRaycastTargets()
        {
            if (panelRoot == null)
            {
                return;
            }

            foreach (Graphic graphic in panelRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null)
                {
                    continue;
                }

                Selectable selectable = graphic.GetComponent<Selectable>();
                if (selectable != null)
                {
                    graphic.raycastTarget = selectable.targetGraphic == graphic;
                    continue;
                }

                Selectable parentSelectable = graphic.GetComponentInParent<Selectable>(true);
                if (parentSelectable != null)
                {
                    graphic.raycastTarget = parentSelectable.targetGraphic == graphic;
                    continue;
                }

                // Decorative chrome should not block poke/ray access to actual controls.
                graphic.raycastTarget = false;
            }
        }

        private void ResolveChildReferences()
        {
            if (panelRoot == null)
            {
                SpatialWindowBindings bound = GetComponentInChildren<SpatialWindowBindings>(true);
                if (bound != null)
                {
                    panelRoot = bound.transform as RectTransform;
                }
                else
                {
                    panelRoot = GetComponentInChildren<RectTransform>(true);
                }
            }

            Transform searchRoot = panelRoot != null ? panelRoot : transform;
            if (bindings != null)
            {
                grabRegion = bindings.GrabRegion;
                resizeHandle = bindings.ResizeHandle;
                closeButton = bindings.CloseButton;
            }

#if UNITY_EDITOR
            bool usedFallback = false;
            if (grabRegion == null)
            {
                grabRegion = FindDeep(searchRoot, "GrabRegion");
                usedFallback = true;
            }
            if (resizeHandle == null)
            {
                resizeHandle = FindDeep(searchRoot, "BottomRight") as RectTransform;
                usedFallback = true;
            }
            if (closeButton == null)
            {
                Transform close = FindDeep(searchRoot, "CloseButton");
                closeButton = close != null ? close.GetComponentInChildren<Button>(true) : null;
                usedFallback = true;
            }
            if (usedFallback)
            {
                Debug.LogWarning("[WINDOW] SpatialWindowBindings missing or incomplete on panel prefab. Using name fallback in Editor only.");
            }
#endif

            if (grabRegion == null || resizeHandle == null || closeButton == null)
            {
                Debug.LogError("[WINDOW] SpatialWindow is missing required prefab references. Check SpatialWindowBindings on the panel prefab.");
            }
        }

        private void SetupHandles()
        {
            Transform searchRoot = panelRoot != null ? panelRoot : transform;
            RectTransform titleBar = FindDeep(searchRoot, "TitleBar") as RectTransform;
            RectTransform closeButtonRect = closeButton != null ? closeButton.transform as RectTransform : null;
            Image grabVisual = null;
            if (grabRegion != null)
            {
                grabVisual = grabRegion.GetComponent<Image>();
            }

            // World-space grab volume under unscaled window root (same path as cube/sphere).
            Transform existing = WindowRoot.Find("WindowGrabVolume");
            GameObject grabVolume = existing != null ? existing.gameObject : new GameObject("WindowGrabVolume");
            if (existing == null)
            {
                grabVolume.transform.SetParent(WindowRoot, false);
            }

            _handle = grabVolume.GetComponent<SpatialWindowHandle>();
            if (_handle == null)
            {
                _handle = grabVolume.AddComponent<SpatialWindowHandle>();
            }

            _handle.Initialize(this, titleBar, closeButtonRect, grabVisual);

            // Remove any old canvas-scaled collider on GrabRegion so it cannot fight the volume.
            if (grabRegion != null)
            {
                SpatialWindowHandle legacy = grabRegion.GetComponent<SpatialWindowHandle>();
                if (legacy != null && legacy != _handle)
                {
                    Destroy(legacy);
                }

                BoxCollider legacyBox = grabRegion.GetComponent<BoxCollider>();
                if (legacyBox != null)
                {
                    Destroy(legacyBox);
                }

                XRSimpleInteractable legacyIx = grabRegion.GetComponent<XRSimpleInteractable>();
                if (legacyIx != null)
                {
                    Destroy(legacyIx);
                }
            }

            Transform resizeExisting = WindowRoot.Find("WindowResizeVolume");
            GameObject resizeVolume = resizeExisting != null ? resizeExisting.gameObject : new GameObject("WindowResizeVolume");
            if (resizeExisting == null)
            {
                resizeVolume.transform.SetParent(WindowRoot, false);
            }

            Image resizeVisual = resizeHandle != null ? resizeHandle.GetComponent<Image>() : null;
            RectTransform cornerRt = resizeHandle as RectTransform;

            _resize = resizeVolume.GetComponent<SpatialWindowResizeHandle>();
            if (_resize == null)
            {
                _resize = resizeVolume.AddComponent<SpatialWindowResizeHandle>();
            }

            _resize.Initialize(this, cornerRt, resizeVisual);

            if (resizeHandle != null)
            {
                SpatialWindowResizeHandle legacyResize = resizeHandle.GetComponent<SpatialWindowResizeHandle>();
                if (legacyResize != null && legacyResize != _resize)
                {
                    Destroy(legacyResize);
                }

                BoxCollider legacyResizeBox = resizeHandle.GetComponent<BoxCollider>();
                if (legacyResizeBox != null)
                {
                    Destroy(legacyResizeBox);
                }

                XRSimpleInteractable legacyResizeIx = resizeHandle.GetComponent<XRSimpleInteractable>();
                if (legacyResizeIx != null)
                {
                    Destroy(legacyResizeIx);
                }
            }
        }

        private void WireCloseButton()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        private void ApplyDefaultSize()
        {
            if (panelRoot == null || config == null)
            {
                return;
            }

            // Prefer scaling the window root; keep authored pixel layout intact.
            _currentScale = 1f;
            WindowRoot.localScale = Vector3.one;

            NorscopePanelRoot panelSize = panelRoot.GetComponent<NorscopePanelRoot>();
            if (panelSize != null)
            {
                // Panel root already authored at ~0.55x0.38 via pixels; leave RectTransform alone.
            }
            else
            {
                float ppm = config.pixelsPerMeter;
                panelRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, config.defaultWidthMeters * ppm);
                panelRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, config.defaultHeightMeters * ppm);
            }
        }

        private void SetCanvasUiLayer()
        {
            int ui = LayerMask.NameToLayer("UI");
            if (ui < 0)
            {
                return;
            }

            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                // Keep grab/resize / world grab volume on Grab for XRI physics.
                if (t == grabRegion || t == resizeHandle || t.name == "WindowGrabVolume" || t.name == "WindowResizeVolume")
                {
                    continue;
                }

                int grabLayer = LayerMask.NameToLayer("Grab");
                if (grabLayer >= 0 && t.gameObject.layer == grabLayer)
                {
                    continue;
                }

                if (t.GetComponent<Graphic>() != null || t.GetComponent<Canvas>() != null || t.GetComponent<TMP_Text>() != null)
                {
                    t.gameObject.layer = ui;
                }
            }
        }

        public void BeginMove(SpatialWindowHandle handle)
        {
            if (IsResizing)
            {
                return;
            }

            IsMoving = true;
            SetContentInteractionBlocked(true);
            UIAudioManager.Play(UIAudioEvent.PanelMoveStart);
        }

        public void EndMove(SpatialWindowHandle handle)
        {
            IsMoving = false;
            if (!IsResizing)
            {
                SetContentInteractionBlocked(false);
            }

            UIAudioManager.Play(UIAudioEvent.PanelMoveEnd);
        }

        public void BeginResize(SpatialWindowResizeHandle handle)
        {
            if (IsMoving)
            {
                return;
            }

            IsResizing = true;
            SetContentInteractionBlocked(true);
            UIAudioManager.Play(UIAudioEvent.PanelResizeStart);
        }

        public void EndResize(SpatialWindowResizeHandle handle)
        {
            IsResizing = false;
            if (!IsMoving)
            {
                SetContentInteractionBlocked(false);
            }

            UIAudioManager.Play(UIAudioEvent.PanelResizeEnd);
        }

        private void SetContentInteractionBlocked(bool blocked)
        {
            if (panelRoot == null)
            {
                return;
            }

            CanvasGroup group = panelRoot.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = panelRoot.gameObject.AddComponent<CanvasGroup>();
            }

            // Block UI button clicks while moving/resizing; XRI grab/resize use physics colliders.
            group.blocksRaycasts = !blocked;
            group.interactable = !blocked;
        }

        public void SetUniformScale(float scale)
        {
            if (config == null)
            {
                _currentScale = scale;
                WindowRoot.localScale = Vector3.one * scale;
                return;
            }

            _currentScale = Mathf.Clamp(scale, config.minimumScale, config.maximumScale);
            WindowRoot.localScale = Vector3.one * _currentScale;
        }

        public void ResetScale()
        {
            SetUniformScale(1f);
        }

        public void Recenter(Transform cameraTransform)
        {
            if (config == null || cameraTransform == null)
            {
                return;
            }

            Pose pose = config.GetRecenterPose(cameraTransform);
            WindowRoot.SetPositionAndRotation(pose.position, pose.rotation);

            if (config.resetScaleOnRecenter || !config.preserveScaleOnRecenter)
            {
                ResetScale();
            }
            // else preserve current scale.

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                UIAudioManager.Play(UIAudioEvent.PanelOpen);
            }

            _isOpen = true;
            Debug.Log("[WINDOW] " + windowId + " recentered");
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            IsMoving = false;
            IsResizing = false;
            Debug.Log("[WINDOW] " + windowId + " closed");

            // Deactivate + reuse (simpler, preserves instance, no prefab asset destruction).
            gameObject.SetActive(false);
            UIAudioManager.Play(UIAudioEvent.PanelClose);
            Closed?.Invoke(this);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _isOpen = true;
            UIAudioManager.Play(UIAudioEvent.PanelOpen);
        }

        public Vector2 GetWorldSizeMeters()
        {
            float ppm = config != null ? config.pixelsPerMeter : 1000f;
            float widthPx = 550f;
            float heightPx = 380f;
            if (panelRoot != null)
            {
                widthPx = panelRoot.rect.width > 1f ? panelRoot.rect.width : panelRoot.sizeDelta.x;
                heightPx = panelRoot.rect.height > 1f ? panelRoot.rect.height : panelRoot.sizeDelta.y;
            }

            float s = _currentScale;
            // Canvas local scale is also applied under this root.
            Canvas canvas = GetComponentInChildren<Canvas>(true);
            float canvasScale = canvas != null ? Mathf.Abs(canvas.transform.localScale.x) : (1f / ppm);
            return new Vector2(widthPx * canvasScale * s, heightPx * canvasScale * s);
        }

        private void LogSpawnOnce()
        {
            if (_loggedSpawn)
            {
                return;
            }

            _loggedSpawn = true;
            Vector2 size = GetWorldSizeMeters();
            Camera cam = Camera.main;
            float dist = cam != null ? Vector3.Distance(cam.transform.position, WindowRoot.position) : -1f;
            Debug.Log("[WINDOW] " + windowId + " spawned");
            Debug.Log("[WINDOW] worldWidth=" + size.x.ToString("F3"));
            Debug.Log("[WINDOW] worldHeight=" + size.y.ToString("F3"));
            Debug.Log("[WINDOW] distanceFromCamera=" + dist.ToString("F3"));
            Debug.Log("[WINDOW] scale=" + _currentScale.ToString("F3"));
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
