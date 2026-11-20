using System;
using System.Collections.Generic;
using Microsoft.MixedReality.OpenXR;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityTrackingState = UnityEngine.XR.ARSubsystems.TrackingState;
using Pose = UnityEngine.Pose;

namespace Norscope.ArGuidance
{
    [DisallowMultipleComponent]
    public sealed class QrTrackingOriginProvider : MonoBehaviour, IArTrackingOriginProvider
    {
        public const string ProviderType = "QrTracking";
        public const string OriginObjectName = "ArTrackingOrigin_QR";
        [Header("Accepted QR payloads")]
        [SerializeField] private string[] acceptedPayloads =
        {
            JsonArCalibrationRepository.DefaultMarkerPayload
        };

        [Header("Tracking")]
        [SerializeField] private float lostTimeoutSeconds = 1.0f;
        [SerializeField] private float limitedTimeoutSeconds = 0.25f;
        [SerializeField] private bool useMostStableTransformMode = true;

        [Header("Pose smoothing")]
        [SerializeField, Min(0.01f)] private float positionSmoothTimeSeconds = 0.12f;
        [SerializeField, Min(0.01f)] private float rotationSmoothTimeSeconds = 0.10f;
        [SerializeField] private bool snapOnFirstDetection = true;

        [Header("Debug visuals")]
        [SerializeField] private QrTrackingDebugVisualizer debugVisualizer;

        private GameObject _originObject;
        private ARMarkerManager _markerManager;
        private ARSession _arSession;
        private ArTrackingState _state = ArTrackingState.Invalid;
        private string _status = "QR tracking inactive";
        private Pose _targetPose = Pose.identity;
        private bool _hasTargetPose;
        private bool _active;
        private bool _stickyOriginMode;
        private bool _originFrozen;
        private float _lastSeenUnscaledTime = float.NegativeInfinity;
        private string _activePayload = string.Empty;
        private readonly HashSet<string> _acceptedPayloadSet = new(StringComparer.Ordinal);

        public ArTrackingState TrackingState => _state;
        /// <summary>
        /// Valid once an accepted QR has been locked at least once. Stays valid while
        /// the marker is temporarily lost so holograms hold the last pose; realigns
        /// as soon as the QR is seen again at a new position.
        /// </summary>
        public bool IsValid =>
            _hasTargetPose &&
            OriginTransform != null &&
            TrackingState != ArTrackingState.Invalid;
        public Transform OriginTransform => _originObject != null ? _originObject.transform : null;
        public string Status => _status;
        public string ActivePayload => _activePayload;
        public bool HasAcceptedPayloads => _acceptedPayloadSet.Count > 0;
        public bool IsOriginFrozen => _originFrozen;
        public bool StickyOriginMode => _stickyOriginMode;
        public float PositionSmoothTimeSeconds => positionSmoothTimeSeconds;
        public float RotationSmoothTimeSeconds => rotationSmoothTimeSeconds;
        public float LostTimeoutSeconds => lostTimeoutSeconds;
        public float LimitedTimeoutSeconds => limitedTimeoutSeconds;
        public string ProviderTypeId => ProviderType;

        public event Action<ArTrackingState> TrackingStateChanged;

        private void Awake()
        {
            RebuildAcceptedPayloads();
            if (debugVisualizer == null)
                debugVisualizer = GetComponent<QrTrackingDebugVisualizer>();
            if (debugVisualizer == null)
                debugVisualizer = gameObject.AddComponent<QrTrackingDebugVisualizer>();
        }

        private void OnValidate()
        {
            positionSmoothTimeSeconds = Mathf.Max(0.01f, positionSmoothTimeSeconds);
            rotationSmoothTimeSeconds = Mathf.Max(0.01f, rotationSmoothTimeSeconds);
            limitedTimeoutSeconds = Mathf.Max(0f, limitedTimeoutSeconds);
            lostTimeoutSeconds = Mathf.Max(limitedTimeoutSeconds + 0.01f, lostTimeoutSeconds);
            RebuildAcceptedPayloads();
        }

        private void Update()
        {
            if (!_active || _originObject == null)
                return;

            UpdateTrackingStateFromTimeouts();
            ApplyPoseSmoothing(Time.unscaledDeltaTime);
            debugVisualizer?.Refresh(TrackingState, Status, ActivePayload, HasAcceptedPayloads);
        }

        /// <summary>
        /// Legacy sticky flag — now triggers a fresh QR rescan instead of freezing.
        /// Pose keeps following the marker whenever it is visible.
        /// </summary>
        public void SetStickyOriginMode(bool enabled)
        {
            _stickyOriginMode = enabled;
            _originFrozen = false;
            if (enabled && _active)
                RequestRescan();
            else if (!enabled && _active)
                StartMarkerScanning();
        }

        public void Activate()
        {
            _active = true;
            _originFrozen = false;
            EnsureOriginObject();
            RebuildAcceptedPayloads();

            if (!EnsureMarkerRuntime())
            {
                SetState(ArTrackingState.Invalid, "QR runtime unavailable");
                debugVisualizer?.Attach(OriginTransform);
                debugVisualizer?.Refresh(TrackingState, Status, ActivePayload, HasAcceptedPayloads);
                return;
            }

            StartMarkerScanning();
            debugVisualizer?.Attach(OriginTransform);
            SetState(ArTrackingState.Searching,
                HasAcceptedPayloads ? "Scan approved QR code" : "Configure accepted QR payloads");
            debugVisualizer?.Refresh(TrackingState, Status, ActivePayload, HasAcceptedPayloads);
        }

        public void Deactivate()
        {
            _active = false;
            _stickyOriginMode = false;
            _originFrozen = false;
            StopMarkerScanning();

            _hasTargetPose = false;
            _activePayload = string.Empty;
            _lastSeenUnscaledTime = float.NegativeInfinity;
            SetState(ArTrackingState.Invalid, "QR tracking inactive");
            debugVisualizer?.Hide();
        }

        public void ResetOrigin()
        {
            // Prefer rescan — destroying the origin GO would orphan hologram roots.
            RequestRescan();
        }

        /// <summary>
        /// Clear the current lock and scan again without destroying the origin transform.
        /// </summary>
        public void RequestRescan()
        {
            _originFrozen = false;
            _hasTargetPose = false;
            _activePayload = string.Empty;
            _lastSeenUnscaledTime = float.NegativeInfinity;

            if (!_active)
            {
                Activate();
                return;
            }

            EnsureOriginObject();
            if (!EnsureMarkerRuntime())
            {
                SetState(ArTrackingState.Invalid, "QR runtime unavailable");
                return;
            }

            StartMarkerScanning();
            SetState(ArTrackingState.Searching,
                HasAcceptedPayloads ? "Scan approved QR code" : "Configure accepted QR payloads");
            debugVisualizer?.Attach(OriginTransform);
            debugVisualizer?.Refresh(TrackingState, Status, ActivePayload, HasAcceptedPayloads);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[ArGuidance] QR rescan requested — searching for approved marker.");
#endif
        }

        private void EnsureOriginObject()
        {
            if (_originObject != null)
                return;

            _originObject = new GameObject(OriginObjectName);
            _originObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _originObject.hideFlags = HideFlags.None;
        }

        private bool EnsureMarkerRuntime()
        {
            XROrigin xrOrigin = FindFirstObjectByType<XROrigin>();
            if (xrOrigin == null)
            {
                Debug.LogWarning("[ArGuidance] QR tracking requires an XROrigin in the scene.", this);
                return false;
            }

            _arSession = FindFirstObjectByType<ARSession>();
            if (_arSession == null)
            {
                GameObject sessionObject = new("AR Session");
                _arSession = sessionObject.AddComponent<ARSession>();
            }

            _markerManager = xrOrigin.GetComponent<ARMarkerManager>();
            if (_markerManager == null)
                _markerManager = xrOrigin.gameObject.AddComponent<ARMarkerManager>();

            return _markerManager != null;
        }

        private void OnMarkersChanged(ARMarkersChangedEventArgs args)
        {
            ProcessMarkers(args.added);
            ProcessMarkers(args.updated);
        }

        private void ProcessMarkers(IReadOnlyList<ARMarker> markers)
        {
            if (markers == null)
                return;

            for (int i = 0; i < markers.Count; i++)
            {
                ARMarker marker = markers[i];
                if (marker == null || marker.markerType != ARMarkerType.QRCode)
                    continue;

                string payload = marker.GetDecodedString() ?? string.Empty;
                LogDecodedPayload(payload);
                if (!IsAcceptedPayload(payload))
                    continue;

                bool firstLock = !_hasTargetPose;
                _activePayload = payload;
                _lastSeenUnscaledTime = Time.unscaledTime;
                _targetPose = new Pose(marker.transform.position, marker.transform.rotation);

                // Snap on first lock after a rescan; thereafter smooth-follow QR moves.
                if (firstLock && snapOnFirstDetection)
                    OriginTransform.SetPositionAndRotation(_targetPose.position, _targetPose.rotation);

                _hasTargetPose = true;
                _originFrozen = false;
                SetState(ResolveTrackingState(marker), BuildTrackedStatus(marker));
                return;
            }
        }

        private void StartMarkerScanning()
        {
            if (_markerManager == null && !EnsureMarkerRuntime())
                return;

            _markerManager.markersChanged -= OnMarkersChanged;
            _markerManager.markersChanged += OnMarkersChanged;
            _markerManager.enabledMarkerTypes = new[] { ARMarkerType.QRCode };
            _markerManager.defaultTransformMode = useMostStableTransformMode
                ? TransformMode.MostStable
                : TransformMode.Center;
            _markerManager.enabled = true;
            if (_arSession != null)
                _arSession.enabled = true;
        }

        private void StopMarkerScanning()
        {
            if (_markerManager == null)
                return;

            _markerManager.markersChanged -= OnMarkersChanged;
            _markerManager.enabled = false;
        }

        private void UpdateTrackingStateFromTimeouts()
        {
            if (!_hasTargetPose)
            {
                if (HasAcceptedPayloads)
                    SetState(ArTrackingState.Searching, "Scan approved QR code");
                else
                    SetState(ArTrackingState.Invalid, "Configure accepted QR payloads");
                return;
            }

            float unseenSeconds = Time.unscaledTime - _lastSeenUnscaledTime;
            if (unseenSeconds >= lostTimeoutSeconds)
            {
                // Keep last pose valid; keep scanning so a moved QR realigns when found.
                SetState(ArTrackingState.Lost, "TRACKING LOST — scan QR to realign");
                return;
            }

            if (unseenSeconds >= limitedTimeoutSeconds)
            {
                SetState(ArTrackingState.Limited, "QR TRACKED (limited)");
                return;
            }

            if (_state != ArTrackingState.Tracking)
                SetState(ArTrackingState.Tracking, "QR TRACKED");
        }

        private void ApplyPoseSmoothing(float deltaTime)
        {
            if (!_hasTargetPose || deltaTime <= 0f)
                return;

            float positionAlpha = ExponentialAlpha(positionSmoothTimeSeconds, deltaTime);
            float rotationAlpha = ExponentialAlpha(rotationSmoothTimeSeconds, deltaTime);
            Transform origin = OriginTransform;
            origin.position = Vector3.Lerp(origin.position, _targetPose.position, positionAlpha);
            origin.rotation = Quaternion.Slerp(origin.rotation, _targetPose.rotation, rotationAlpha);
        }

        private static float ExponentialAlpha(float smoothTimeSeconds, float deltaTime)
        {
            if (smoothTimeSeconds <= 0.0001f)
                return 1f;

            return 1f - Mathf.Exp(-deltaTime / smoothTimeSeconds);
        }

        private ArTrackingState ResolveTrackingState(ARMarker marker)
        {
            return marker.trackingState == UnityTrackingState.Tracking
                ? ArTrackingState.Tracking
                : ArTrackingState.Limited;
        }

        private string BuildTrackedStatus(ARMarker marker)
        {
            return marker.trackingState == UnityTrackingState.Tracking
                ? "QR TRACKED"
                : "QR TRACKED (limited)";
        }

        private bool IsAcceptedPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return false;

            return _acceptedPayloadSet.Contains(payload.Trim());
        }

        private void RebuildAcceptedPayloads()
        {
            _acceptedPayloadSet.Clear();
            if (acceptedPayloads == null)
                return;

            for (int i = 0; i < acceptedPayloads.Length; i++)
            {
                string payload = acceptedPayloads[i]?.Trim();
                if (!string.IsNullOrEmpty(payload))
                    _acceptedPayloadSet.Add(payload);
            }
        }

        private void SetState(ArTrackingState state, string status)
        {
            _status = status;
            if (_state == state)
                return;

            _state = state;
            TrackingStateChanged?.Invoke(_state);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDecodedPayload(string payload)
        {
            if (!string.IsNullOrWhiteSpace(payload))
                Debug.Log($"[ArGuidance] Decoded QR payload: {payload}");
        }
    }
}
