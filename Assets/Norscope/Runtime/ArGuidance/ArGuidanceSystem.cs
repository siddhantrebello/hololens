using UnityEngine;
using Norscope.TaskExecution;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Root AR Guidance runtime. Owns origin provider, calibration, and renderer.
    /// Holograms are children of the tracking origin — never the camera/panel/hands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArGuidanceSystem : MonoBehaviour
    {
        public static ArGuidanceSystem Instance { get; private set; }

        [SerializeField] private ArGuidanceProfile profile;
        [SerializeField] private ManualTestOriginProvider manualOrigin;
        [SerializeField] private QrTrackingOriginProvider qrOriginProvider;
        [SerializeField] private ArGuidanceRenderer guidanceRenderer;
        [SerializeField] private ArCalibrationController calibrationController;
        [SerializeField] private ArTaskGuidanceController taskGuidanceController;
        [SerializeField] private ArTaskGuidanceMap taskGuidanceMap;
        [SerializeField] private ActiveTaskSession taskSession;

        private IArCalibrationRepository _repository;
        private Transform _calibrationVisualRoot;
        private Transform _taskGuidanceRoot;
        private bool _sessionActive;

        public ArGuidanceProfile Profile => profile;
        public IArTrackingOriginProvider ActiveOrigin { get; private set; }
        public IArCalibrationRepository Repository => _repository;
        public ArGuidanceRenderer Renderer => guidanceRenderer;
        public ArCalibrationController Calibration => calibrationController;
        public ArTaskGuidanceController TaskGuidance => taskGuidanceController;
        public bool IsSessionActive => _sessionActive;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (manualOrigin == null)
                manualOrigin = GetComponent<ManualTestOriginProvider>();
            if (manualOrigin == null)
                manualOrigin = gameObject.AddComponent<ManualTestOriginProvider>();
            if (qrOriginProvider == null)
                qrOriginProvider = GetComponent<QrTrackingOriginProvider>();
            if (qrOriginProvider == null)
                qrOriginProvider = gameObject.AddComponent<QrTrackingOriginProvider>();
            if (guidanceRenderer == null)
                guidanceRenderer = GetComponent<ArGuidanceRenderer>();
            if (guidanceRenderer == null)
                guidanceRenderer = gameObject.AddComponent<ArGuidanceRenderer>();
            if (calibrationController == null)
                calibrationController = GetComponent<ArCalibrationController>();
            if (calibrationController == null)
                calibrationController = gameObject.AddComponent<ArCalibrationController>();
            if (GetComponent<ArVisibilityController>() == null)
                gameObject.AddComponent<ArVisibilityController>();
            if (taskGuidanceController == null)
                taskGuidanceController = GetComponent<ArTaskGuidanceController>();
            if (taskGuidanceController == null)
                taskGuidanceController = gameObject.AddComponent<ArTaskGuidanceController>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetProfile(ArGuidanceProfile equipmentProfile)
        {
            profile = equipmentProfile;
        }

        public void BeginSession()
        {
            if (profile == null)
            {
                Debug.LogError("[AR Guidance] Profile missing.");
                return;
            }

            ActiveOrigin = ResolveOriginProvider();
            ActiveOrigin.Activate();

            if (ActiveOrigin.OriginTransform == null)
            {
                Debug.LogError("[AR Guidance] Tracking origin unavailable.");
                return;
            }

            EnsureRoots(ActiveOrigin.OriginTransform);
            _repository = new JsonArCalibrationRepository(profile.equipmentId);
            // Calibration holograms belong under CalibrationVisualRoot, not TaskGuidanceRoot —
            // ArTaskGuidanceController never references this transform by name, it only calls
            // into guidanceRenderer's id-keyed API, so this rebind doesn't affect task logic.
            guidanceRenderer.Bind(profile, _calibrationVisualRoot);
            guidanceRenderer.EnsureInstances(_repository);
            if (profile.hologramLibrary != null)
                guidanceRenderer.EnsureLibraryInstances(profile.hologramLibrary);
            ReloadAllFromRepository();
            guidanceRenderer.HideAll();
            // Library holograms may not be in the JSON yet — still apply library defaults
            // and any saved overrides that ReloadAll already covered.
            if (profile.hologramLibrary != null)
            {
                for (int i = 0; i < profile.hologramLibrary.entries.Length; i++)
                {
                    ArHologramLibraryEntry entry = profile.hologramLibrary.entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.hologramId))
                        continue;
                    guidanceRenderer.ApplyPoseFor(entry.hologramId, _repository, profile.hologramLibrary);
                }
            }

            guidanceRenderer.EnforceScrewGroupParentIdentity();
            guidanceRenderer.HideAll();
            calibrationController.Bind(guidanceRenderer, _repository, profile.hologramLibrary);
            if (taskSession == null)
                taskSession = FindFirstObjectByType<ActiveTaskSession>();
            taskGuidanceController?.Configure(taskSession, this, taskGuidanceMap);
            _sessionActive = true;
        }

        public void EndSession()
        {
            taskGuidanceController?.HideAllTaskGuidance(false);
            calibrationController?.HidePreview();
            guidanceRenderer?.HideAll();
            ActiveOrigin?.Deactivate();
            _sessionActive = false;
        }

        /// <summary>
        /// Force a fresh QR (or Manual) origin acquisition without tearing down hologram roots.
        /// Called at task start and whenever the Developer panel opens.
        /// </summary>
        public void RequestOriginRescan()
        {
            if (!_sessionActive || ActiveOrigin == null)
                BeginSession();
            ActiveOrigin?.RequestRescan();
        }

        /// <summary>Hide every hologram immediately (task + calibration + preview).</summary>
        public void HideAllHolograms()
        {
            taskGuidanceController?.HideAllTaskGuidance(false);
            calibrationController?.HidePreview();
            guidanceRenderer?.HideAll();
        }

        private void EnsureRoots(Transform origin)
        {
            _calibrationVisualRoot = origin.Find("CalibrationVisualRoot");
            if (_calibrationVisualRoot == null)
            {
                var go = new GameObject("CalibrationVisualRoot");
                go.transform.SetParent(origin, false);
                _calibrationVisualRoot = go.transform;
            }

            _taskGuidanceRoot = origin.Find("TaskGuidanceRoot");
            if (_taskGuidanceRoot == null)
            {
                var go = new GameObject("TaskGuidanceRoot");
                go.transform.SetParent(origin, false);
                _taskGuidanceRoot = go.transform;
            }
        }

        private void ReloadAllFromRepository()
        {
            if (_repository == null)
                return;
            ArCalibrationFile file = _repository.LoadOrCreate();
            if (file.components == null)
                return;
            for (int i = 0; i < file.components.Length; i++)
                guidanceRenderer.ApplyCalibration(file.components[i]);
        }

        private IArTrackingOriginProvider ResolveOriginProvider()
        {
            return Application.isEditor
                ? (IArTrackingOriginProvider)manualOrigin
                : qrOriginProvider;
        }
    }
}
