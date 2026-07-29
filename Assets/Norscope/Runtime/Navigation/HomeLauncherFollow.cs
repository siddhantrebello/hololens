using UnityEngine;

namespace Norscope.Navigation
{
    /// <summary>
    /// Critically damped head follow for the permanent Home Launcher.
    /// Never grabs / resizes — only smooth pose follow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeLauncherFollow : MonoBehaviour
    {
        [Header("Follow pose")]
        [SerializeField] private float followDistance = 0.5f;
        [SerializeField] private float verticalOffset = -0.5f;
        [SerializeField] private float horizontalOffset = 0f;

        [Header("Damping")]
        [Tooltip("Higher = snappier. SmoothDamp time ≈ 1 / followSpeed.")]
        [SerializeField] private float followSpeed = 14f;
        [SerializeField] private float rotationSpeed = 14f;

        [SerializeField] private Transform followTarget;

        private Vector3 _velocity;

        public float FollowDistance
        {
            get => followDistance;
            set => followDistance = value;
        }

        public float VerticalOffset
        {
            get => verticalOffset;
            set => verticalOffset = value;
        }

        public float HorizontalOffset
        {
            get => horizontalOffset;
            set => horizontalOffset = value;
        }

        public float FollowSpeed
        {
            get => followSpeed;
            set => followSpeed = Mathf.Max(0.01f, value);
        }

        public float RotationSpeed
        {
            get => rotationSpeed;
            set => rotationSpeed = Mathf.Max(0.01f, value);
        }

        private void LateUpdate()
        {
            Transform cam = ResolveCamera();
            if (cam == null)
            {
                return;
            }

            Pose target = ComputeTargetPose(cam);
            float smoothTime = 1f / Mathf.Max(0.01f, followSpeed);
            transform.position = Vector3.SmoothDamp(transform.position, target.position, ref _velocity, smoothTime);

            float rotT = 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, rotT);
        }

        public Pose ComputeTargetPose(Transform cameraTransform)
        {
            if (cameraTransform == null)
            {
                return new Pose(transform.position, transform.rotation);
            }

            Vector3 flatForward = cameraTransform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = Vector3.forward;
            }

            flatForward.Normalize();
            Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;

            // ~45° below eye at default (distance 0.5, vertical -0.5).
            Vector3 position = cameraTransform.position
                               + flatForward * followDistance
                               + Vector3.up * verticalOffset
                               + flatRight * horizontalOffset;

            Quaternion rotation = Quaternion.LookRotation(flatForward, Vector3.up);
            return new Pose(position, rotation);
        }

        public void SnapToTarget()
        {
            Transform cam = ResolveCamera();
            if (cam == null)
            {
                return;
            }

            Pose pose = ComputeTargetPose(cam);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            _velocity = Vector3.zero;
        }

        private Transform ResolveCamera()
        {
            if (followTarget != null)
            {
                return followTarget;
            }

            Camera cam = Camera.main;
            return cam != null ? cam.transform : null;
        }
    }
}
