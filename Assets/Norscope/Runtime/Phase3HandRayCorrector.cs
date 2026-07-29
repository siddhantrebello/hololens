using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace Norscope
{
    /// <summary>
    /// Phase 3 only: align far-ray visual with official Aim Pose and keep a stable ~3 m line
    /// that is not shortened by Default-layer room/spatial geometry.
    /// Does not invent poses or apply Euler offsets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Phase3HandRayCorrector : MonoBehaviour
    {
        public const string GrabLayerName = "Grab";
        public const float DiagnosticRayLengthMeters = 3f;

        [SerializeField] private float rayLengthMeters = DiagnosticRayLengthMeters;
        [SerializeField] private bool applyOnAwake = true;
        [SerializeField] private bool useRaycastHitDetection = true;

        private void Awake()
        {
            if (applyOnAwake)
            {
                EnsureUiCanvasesOnUiLayer();
                Apply();
            }
        }

        [ContextMenu("Apply Phase 3 Hand Ray Fixes")]
        public void Apply()
        {
            EnsureUiCanvasesOnUiLayer();

            int grabLayer = LayerMask.NameToLayer(GrabLayerName);
            LayerMask farMask = LayerMask.GetMask("UI");
            if (grabLayer >= 0)
            {
                farMask |= 1 << grabLayer;
            }

            NearFarInteractor[] interactors = FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < interactors.Length; i++)
            {
                NearFarInteractor interactor = interactors[i];
                if (interactor == null)
                {
                    continue;
                }

                Transform handRoot = FindHandRoot(interactor.transform);
                if (handRoot == null)
                {
                    // Skip controller interactors; HL2 Phase 3 is hands-only.
                    continue;
                }

                Transform aimPose = FindChildRecursive(handRoot, "Aim Pose");
                if (aimPose == null)
                {
                    Debug.LogWarning("[Norscope Phase3] Aim Pose not found under " + handRoot.name, interactor);
                    continue;
                }

                // Cast and visual both use the official Aim Pose (OpenXR pointer/aim).
                if (interactor.farInteractionCaster is CurveInteractionCaster curveCaster)
                {
                    curveCaster.castOrigin = aimPose;
                    curveCaster.castDistance = rayLengthMeters;
                    curveCaster.raycastMask = farMask;
                    if (useRaycastHitDetection)
                    {
                        curveCaster.hitDetectionType = CurveInteractionCaster.HitDetectionType.Raycast;
                    }
                }

                CurveVisualController visual = interactor.GetComponentInChildren<CurveVisualController>(true);
                if (visual != null)
                {
                    visual.overrideLineOrigin = true;
                    visual.lineOriginTransform = aimPose;
                    visual.maxVisualCurveDistance = rayLengthMeters;
                    visual.restingVisualLineLength = rayLengthMeters;
                    visual.lineDynamicsMode = LineDynamicsMode.Traditional;
                    visual.extendLineToEmptyHit = true;
                    visual.extensionRate = 0f;
                }

                LineRenderer line = interactor.GetComponentInChildren<LineRenderer>(true);
                if (line != null)
                {
                    // Neutral restrained line; avoid magenta missing-material look.
                    line.startWidth = 0.0035f;
                    line.endWidth = 0.0015f;
                    Color c = new Color(0.85f, 0.88f, 0.92f, 0.85f);
                    line.startColor = c;
                    line.endColor = new Color(c.r, c.g, c.b, 0.35f);
                    if (line.sharedMaterial == null || IsMagenta(line.sharedMaterial))
                    {
                        Shader shader = Shader.Find("Sprites/Default");
                        if (shader != null)
                        {
                            line.sharedMaterial = new Material(shader) { color = c };
                        }
                    }
                }

                Debug.Log("[Norscope Phase3] Far ray corrected for " + interactor.name +
                          " | aim=" + aimPose.name +
                          " | length=" + rayLengthMeters +
                          " | mask=" + farMask.value, interactor);
            }
        }

        private static void EnsureUiCanvasesOnUiLayer()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0)
            {
                return;
            }

            string[] names =
            {
                "HandTestCanvas",
                "InteractionTestCanvas",
                "DiagnosticsPanel",
                "InteractionTestPanel",
                "WindowCanvas",
                "TEMP_SpatialWindowLauncher"
            };
            for (int i = 0; i < names.Length; i++)
            {
                GameObject go = GameObject.Find(names[i]);
                if (go == null)
                {
                    continue;
                }

                SetLayerRecursive(go.transform, uiLayer);
            }

            // Runtime-spawned spatial windows are not in the named list at Awake.
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                {
                    continue;
                }

                SetLayerRecursive(canvas.transform, uiLayer);
            }
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.layer == LayerMask.NameToLayer(GrabLayerName))
                {
                    continue;
                }

                t.gameObject.layer = layer;
            }
        }

        private static Transform FindHandRoot(Transform t)
        {
            while (t != null)
            {
                if (t.name == "Left Hand" || t.name == "Right Hand")
                {
                    return t;
                }

                t = t.parent;
            }

            return null;
        }

        private static bool IsMagenta(Material mat)
        {
            if (mat == null)
            {
                return true;
            }

            if (mat.shader == null || mat.shader.name.Contains("Hidden/InternalErrorShader"))
            {
                return true;
            }

            if (mat.HasProperty("_Color"))
            {
                Color c = mat.color;
                return c.r > 0.9f && c.g < 0.1f && c.b > 0.9f;
            }

            return false;
        }

        private static Transform FindChildRecursive(Transform root, string name)
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
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
