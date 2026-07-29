using UnityEngine;

namespace Norscope.Windows
{
    /// <summary>
    /// Central Inspector-editable spawn / size settings for spatial windows.
    /// </summary>
    [CreateAssetMenu(fileName = "SpatialWindowConfig", menuName = "Norscope/Windows/Spatial Window Config", order = 20)]
    public sealed class SpatialWindowConfig : ScriptableObject
    {
        [Header("Spawn")]
        [Min(0.3f)] public float spawnDistanceMeters = 1.0f;
        public float verticalOffsetMeters = -0.10f;
        public float horizontalOffsetMeters = 0f;

        [Header("Default panel size (metres)")]
        public float defaultWidthMeters = 0.55f;
        public float defaultHeightMeters = 0.40f;

        [Header("World-space UI")]
        [Min(1f)] public float pixelsPerMeter = 1000f;

        [Header("Scale limits (relative to default size)")]
        [Min(0.1f)] public float minimumScale = 0.40f / 0.55f; // ~0.727 → 0.40 m width
        [Min(0.1f)] public float maximumScale = 0.80f / 0.55f; // ~1.455 → 0.80 m width
        public bool preserveAspectRatio = true;
        [Min(0.01f)] public float resizeSensitivity = 1.0f;

        [Header("Handles")]
        public bool handlesVisible = true;
        public float handleWorldSizeMeters = 0.025f;

        [Header("Recenter")]
        public bool preserveScaleOnRecenter = true;
        public bool resetScaleOnRecenter = false;
        [Min(0.3f)] public float recenterDistanceMeters = 1.0f;
        public float recenterVerticalOffsetMeters = -0.10f;

        [Header("Motion")]
        [Min(1f)] public float moveFollowSharpness = 18f;

        public float CanvasLocalScale => 1f / Mathf.Max(1f, pixelsPerMeter);

        public float MinWidthMeters => defaultWidthMeters * minimumScale;
        public float MaxWidthMeters => defaultWidthMeters * maximumScale;

        public Pose GetSpawnPose(Transform cameraTransform)
        {
            return GetPoseInFront(cameraTransform, spawnDistanceMeters, verticalOffsetMeters, horizontalOffsetMeters);
        }

        public Pose GetRecenterPose(Transform cameraTransform)
        {
            return GetPoseInFront(cameraTransform, recenterDistanceMeters, recenterVerticalOffsetMeters, horizontalOffsetMeters);
        }

        public static Pose GetPoseInFront(Transform cameraTransform, float distance, float verticalOffset, float horizontalOffset)
        {
            if (cameraTransform == null)
            {
                return new Pose(new Vector3(0f, 1.15f, distance), Quaternion.identity);
            }

            Vector3 flatForward = cameraTransform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = Vector3.forward;
            }

            flatForward.Normalize();
            Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;
            Vector3 origin = cameraTransform.position;
            Vector3 position = origin
                              + flatForward * distance
                              + Vector3.up * verticalOffset
                              + flatRight * horizontalOffset;

            // Face the user; keep upright (ignore head pitch).
            Quaternion rotation = Quaternion.LookRotation(flatForward, Vector3.up);
            return new Pose(position, rotation);
        }
    }
}
