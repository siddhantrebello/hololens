using System;
using System.IO;
using UnityEngine;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Places a one-shot world-space simulation origin in front of the user.
    /// Does not follow the head after placement. Not parented to the camera.
    /// If a persisted placement exists (saved via the Inspector context menu after
    /// manually dragging the origin in Play Mode), it is restored instead of being
    /// recomputed from the camera — so a manually-placed "where QR would be" origin
    /// stays consistent across Play Mode sessions until explicitly cleared.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManualTestOriginProvider : MonoBehaviour, IArTrackingOriginProvider
    {
        public const string ProviderType = "ManualTest";
        public const string OriginObjectName = "ArTrackingOrigin_Manual";

        [Serializable]
        private sealed class PersistedOrigin
        {
            public Vector3 position;
            public Quaternion rotation;
        }

        private static string PersistedOriginPath =>
            Path.Combine(Application.persistentDataPath, "ArCalibration", "ManualOrigin.json");

        [Header("Placement (serialized, editable)")]
        [SerializeField] private float forwardDistanceMeters = 1.0f;
        [SerializeField] private float verticalOffsetMeters = -0.45f;
        [SerializeField] private float yawOffsetDegrees;

        [Header("Runtime")]
        [SerializeField] private Transform originTransform;
        [SerializeField] private bool createLabel;

        private ArTrackingState _state = ArTrackingState.Invalid;
        private static ManualTestOriginProvider _activeInstance;

        public ArTrackingState TrackingState => _state;
        public Transform OriginTransform => originTransform;
        public bool IsValid =>
            _state == ArTrackingState.Tracking &&
            originTransform != null;
        public string ProviderTypeId => ProviderType;

        public event Action<ArTrackingState> TrackingStateChanged;

        public float ForwardDistanceMeters
        {
            get => forwardDistanceMeters;
            set => forwardDistanceMeters = value;
        }

        public float VerticalOffsetMeters
        {
            get => verticalOffsetMeters;
            set => verticalOffsetMeters = value;
        }

        private void OnDestroy()
        {
            if (_activeInstance == this)
                _activeInstance = null;
        }

        public void Activate()
        {
            _activeInstance = this;
            EnsureOrigin();
            SetState(ArTrackingState.Tracking);
        }

        public void Deactivate()
        {
            // Keep origin in world for reopen reuse; only mark invalid when destroyed.
            if (originTransform == null)
                SetState(ArTrackingState.Invalid);
        }

        public void ResetOrigin()
        {
            // Prefer repositioning in place so hologram roots under the origin survive.
            RequestRescan();
        }

        /// <summary>
        /// Re-place the simulation origin without destroying child hologram roots.
        /// </summary>
        public void RequestRescan()
        {
            if (originTransform == null)
            {
                EnsureOrigin(forceRecreate: false);
                SetState(ArTrackingState.Tracking);
                return;
            }

            // Fresh camera-relative placement (keeps Calibration/Task roots parented).
            Camera cam = Camera.main;
            if (cam == null)
            {
                Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (cams != null && cams.Length > 0)
                    cam = cams[0];
            }

            Vector3 position = new Vector3(0f, 0f, forwardDistanceMeters);
            Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
            if (cam != null)
            {
                Vector3 flatForward = cam.transform.forward;
                flatForward.y = 0f;
                if (flatForward.sqrMagnitude < 0.0001f)
                    flatForward = Vector3.forward;
                flatForward.Normalize();
                position = cam.transform.position
                           + flatForward * forwardDistanceMeters
                           + Vector3.up * verticalOffsetMeters;
                rotation = Quaternion.AngleAxis(yawOffsetDegrees, Vector3.up)
                           * Quaternion.LookRotation(flatForward, Vector3.up)
                           * Quaternion.Euler(90f, 0f, 0f);
            }

            originTransform.SetPositionAndRotation(position, rotation);
            SetState(ArTrackingState.Tracking);
        }

        private void EnsureOrigin(bool forceRecreate = false)
        {
            if (!forceRecreate && originTransform != null)
                return;

            GameObject existing = GameObject.Find(OriginObjectName);
            if (!forceRecreate && existing != null)
            {
                originTransform = existing.transform;
                return;
            }

            if (forceRecreate && existing != null)
                Destroy(existing);

            Vector3 position;
            Quaternion rotation;
            if (!forceRecreate && TryLoadPersistedOrigin(out Vector3 savedPos, out Quaternion savedRot))
            {
                position = savedPos;
                rotation = savedRot;
            }
            else
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                    if (cams != null && cams.Length > 0)
                        cam = cams[0];
                }

                position = new Vector3(0f, 0f, forwardDistanceMeters);
                rotation = Quaternion.identity;
                if (cam != null)
                {
                    Vector3 flatForward = cam.transform.forward;
                    flatForward.y = 0f;
                    if (flatForward.sqrMagnitude < 0.0001f)
                        flatForward = Vector3.forward;
                    flatForward.Normalize();

                    position = cam.transform.position
                               + flatForward * forwardDistanceMeters
                               + Vector3.up * verticalOffsetMeters;
                    rotation = Quaternion.AngleAxis(yawOffsetDegrees, Vector3.up)
                               * Quaternion.LookRotation(flatForward, Vector3.up)
                               * Quaternion.Euler(90f, 0f, 0f); // horizontal work surface
                }
            }

            var go = new GameObject(OriginObjectName);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.SetParent(null, true);
            originTransform = go.transform;

            CreateSimulationMarker(go.transform);
            if (createLabel)
                CreateSimulationLabel(go.transform);
        }

        /// <summary>Small cyan sphere at the simulation origin (no axes / no text).</summary>
        private static void CreateSimulationMarker(Transform parent)
        {
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "OriginMarker";
            dot.transform.SetParent(parent, false);
            dot.transform.localPosition = Vector3.zero;
            dot.transform.localRotation = Quaternion.identity;
            dot.transform.localScale = Vector3.one * 0.018f;
            dot.layer = LayerMask.NameToLayer("Ignore Raycast");
            UnityEngine.Object.Destroy(dot.GetComponent<Collider>());
            Renderer dotRenderer = dot.GetComponent<Renderer>();
            if (dotRenderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                var mat = new Material(shader) { color = new Color(0.12f, 0.90f, 0.95f, 0.95f) };
                dotRenderer.sharedMaterial = mat;
                dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dotRenderer.receiveShadows = false;
            }
        }

        /// <summary>Captures the origin's current world position/rotation to disk so future
        /// Play Mode sessions restore this exact placement instead of recomputing from the
        /// camera. Invoke via the component's Inspector context menu (right-click the header)
        /// after manually dragging the origin to where a real QR marker would put it.</summary>
        [ContextMenu("Save Current Origin Position")]
        public void SaveOriginPosition()
        {
            if (originTransform == null)
            {
                Debug.LogWarning("[AR Guidance] No origin to save — enter Play Mode and open the Developer panel first.");
                return;
            }

            var data = new PersistedOrigin
            {
                position = originTransform.position,
                rotation = originTransform.rotation
            };

            string path = PersistedOriginPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            Debug.Log("[AR Guidance] Saved manual origin placement -> " + path);
        }

        /// <summary>Deletes the persisted placement and immediately recreates the origin at the
        /// default camera-relative position, so the next session starts fresh again.</summary>
        [ContextMenu("Clear Saved Origin (Reset To Auto-Placement)")]
        public void ClearSavedOriginPosition()
        {
            string path = PersistedOriginPath;
            if (File.Exists(path))
                File.Delete(path);
            Debug.Log("[AR Guidance] Cleared saved manual origin placement.");
            if (Application.isPlaying)
                ResetOrigin();
        }

        private static bool TryLoadPersistedOrigin(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            string path = PersistedOriginPath;
            if (!File.Exists(path))
                return false;

            try
            {
                PersistedOrigin data = JsonUtility.FromJson<PersistedOrigin>(File.ReadAllText(path));
                if (data == null)
                    return false;
                position = data.position;
                rotation = data.rotation;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AR Guidance] Failed to load saved manual origin, using default placement: " + ex.Message);
                return false;
            }
        }

        private static void CreateSimulationLabel(Transform parent)
        {
            var labelGo = new GameObject("SimulationOriginLabel");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            labelGo.transform.localRotation = Quaternion.identity;
            labelGo.layer = LayerMask.NameToLayer("Ignore Raycast");

            TextMesh tm = labelGo.AddComponent<TextMesh>();
            tm.text = "SIMULATION ORIGIN";
            tm.fontSize = 20;
            tm.characterSize = 0.01f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.96f, 0.62f, 0.04f, 0.95f);
        }

        private void SetState(ArTrackingState next)
        {
            if (_state == next)
                return;
            _state = next;
            TrackingStateChanged?.Invoke(_state);
        }
    }
}
