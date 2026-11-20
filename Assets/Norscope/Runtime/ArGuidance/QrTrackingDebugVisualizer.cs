using UnityEngine;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Minimal origin-lock marker: a small cyan sphere at the QR origin.
    /// No cube, axes, or status text.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QrTrackingDebugVisualizer : MonoBehaviour
    {
        [SerializeField] private float sphereDiameterMeters = 0.018f;
        [SerializeField] private Color trackedColor = new(0.12f, 0.90f, 0.95f, 0.95f);
        [SerializeField] private Color limitedColor = new(0.12f, 0.90f, 0.95f, 0.55f);
        [SerializeField] private Color lostColor = new(0.12f, 0.90f, 0.95f, 0.25f);

        private Transform _root;
        private MeshRenderer _sphereRenderer;
        private Material _material;

        public void Attach(Transform parent)
        {
            if (parent == null)
                return;

            if (_root == null)
                Build();

            _root.SetParent(parent, false);
            _root.localPosition = Vector3.zero;
            _root.localRotation = Quaternion.identity;
            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        public void Refresh(ArTrackingState state, string status, string payload, bool hasAcceptedPayloads)
        {
            if (_root == null || !_root.gameObject.activeSelf)
                return;

            Color color = state switch
            {
                ArTrackingState.Tracking => trackedColor,
                ArTrackingState.Limited => limitedColor,
                ArTrackingState.Lost => lostColor,
                ArTrackingState.Searching => limitedColor,
                _ => lostColor
            };

            if (_material != null)
                _material.color = color;

            bool show =
                state == ArTrackingState.Tracking ||
                state == ArTrackingState.Limited ||
                state == ArTrackingState.Lost ||
                state == ArTrackingState.Searching;

            if (_sphereRenderer != null)
                _sphereRenderer.enabled = show;
        }

        private void Build()
        {
            _root = new GameObject("QrTrackingDebug").transform;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "QrOriginSphere";
            sphere.transform.SetParent(_root, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localRotation = Quaternion.identity;
            sphere.transform.localScale = Vector3.one * sphereDiameterMeters;
            Object.Destroy(sphere.GetComponent<Collider>());
            _sphereRenderer = sphere.GetComponent<MeshRenderer>();

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Standard");
            _material = new Material(shader) { color = trackedColor };
            _sphereRenderer.sharedMaterial = _material;
            _sphereRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _sphereRenderer.receiveShadows = false;
        }
    }
}
