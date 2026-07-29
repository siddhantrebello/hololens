using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Norscope
{
    /// <summary>
    /// Phase 3: when a poke tip is near world-space UI, disable that hand's far casting
    /// so XRPokeInteractor can drive TrackedDeviceGraphicRaycaster without the ray stealing UI.
    /// Mirrors the Hands Demo PokeGestureDetector → enableFarCasting wiring, but by proximity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Phase3UiPokeSupport : MonoBehaviour
    {
        [SerializeField] private float nearUiDistance = 0.12f;
        [SerializeField] private float pokeDepthMeters = 0.15f;
        [SerializeField] private bool faceCanvasesTowardCamera = true;

        private NearFarInteractor[] _nearFar;
        private XRPokeInteractor[] _poke;
        private Canvas[] _uiCanvases;
        private int _cachedCanvasCount = -1;
        private float _nextCanvasRefreshTime;
        private const float CanvasRefreshIntervalSeconds = 0.25f;

        private void Awake()
        {
            CacheHands();
            CacheCanvases();
            ConfigurePokeDepth();
            if (faceCanvasesTowardCamera)
            {
                FaceCanvases();
            }
        }

        private void LateUpdate()
        {
            if (_nearFar == null || _poke == null)
            {
                CacheHands();
            }

            RefreshCanvasesIfNeeded();

            if (_nearFar == null || _poke == null)
            {
                return;
            }

            for (int i = 0; i < _poke.Length && i < _nearFar.Length; i++)
            {
                XRPokeInteractor poke = _poke[i];
                NearFarInteractor nearFar = _nearFar[i];
                if (poke == null || nearFar == null)
                {
                    continue;
                }

                bool nearUi = IsPokeNearUi(poke);
                if (nearUi)
                {
                    if (nearFar.enableFarCasting)
                    {
                        nearFar.enableFarCasting = false;
                    }
                }
                else if (!nearFar.enableFarCasting)
                {
                    // Restore unless another system (poke gesture) intentionally left it off
                    // while still in poke gesture — proximity restore is fine for Phase 3 tests.
                    nearFar.enableFarCasting = true;
                }
            }
        }

        private void CacheHands()
        {
            var leftHand = GameObject.Find("Left Hand");
            var rightHand = GameObject.Find("Right Hand");
            _nearFar = new NearFarInteractor[2];
            _poke = new XRPokeInteractor[2];

            if (leftHand != null)
            {
                _nearFar[0] = leftHand.GetComponentInChildren<NearFarInteractor>(true);
                _poke[0] = leftHand.GetComponentInChildren<XRPokeInteractor>(true);
            }

            if (rightHand != null)
            {
                _nearFar[1] = rightHand.GetComponentInChildren<NearFarInteractor>(true);
                _poke[1] = rightHand.GetComponentInChildren<XRPokeInteractor>(true);
            }
        }

        private void CacheCanvases()
        {
            _uiCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _cachedCanvasCount = _uiCanvases != null ? _uiCanvases.Length : 0;
            _nextCanvasRefreshTime = Time.unscaledTime + CanvasRefreshIntervalSeconds;
        }

        private void RefreshCanvasesIfNeeded()
        {
            if (_uiCanvases == null || _uiCanvases.Length == 0)
            {
                CacheCanvases();
                return;
            }

            if (Time.unscaledTime < _nextCanvasRefreshTime)
            {
                return;
            }

            int currentCount = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            if (currentCount != _cachedCanvasCount)
            {
                CacheCanvases();
                return;
            }

            _nextCanvasRefreshTime = Time.unscaledTime + CanvasRefreshIntervalSeconds;
        }

        /// <summary>Call after spawning runtime world-space UI (e.g. SpatialWindow).</summary>
        public void RefreshUiCanvases()
        {
            CacheCanvases();
            if (faceCanvasesTowardCamera)
            {
                FaceCanvases();
            }
        }

        private void ConfigurePokeDepth()
        {
            if (_poke == null)
            {
                return;
            }

            for (int i = 0; i < _poke.Length; i++)
            {
                if (_poke[i] != null)
                {
                    _poke[i].pokeDepth = pokeDepthMeters;
                }
            }
        }

        private void FaceCanvases()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            string[] names =
            {
                "HandTestCanvas",
                "InteractionTestCanvas",
                "DiagnosticsPanel",
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

                // World-space UI faces -Z of the RectTransform; look at camera then flip.
                Vector3 toCam = cam.transform.position - go.transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                go.transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);

                TrackedDeviceGraphicRaycaster raycaster = go.GetComponent<TrackedDeviceGraphicRaycaster>();
                if (raycaster == null)
                {
                    go.AddComponent<TrackedDeviceGraphicRaycaster>();
                }
            }
        }

        private bool IsPokeNearUi(XRPokeInteractor poke)
        {
            if (_uiCanvases == null)
            {
                return false;
            }

            Transform tip = poke.GetAttachTransform(null);
            if (tip == null)
            {
                tip = poke.transform;
            }

            Vector3 tipPos = tip.position;
            float maxDistSq = nearUiDistance * nearUiDistance;

            for (int i = 0; i < _uiCanvases.Length; i++)
            {
                Canvas canvas = _uiCanvases[i];
                if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                {
                    continue;
                }

                if (((1 << canvas.gameObject.layer) & LayerMask.GetMask("UI")) == 0)
                {
                    continue;
                }

                // Distance to canvas plane.
                Transform t = canvas.transform;
                Vector3 toTip = tipPos - t.position;
                float planar = Mathf.Abs(Vector3.Dot(toTip, t.forward));
                Vector3 onPlane = toTip - t.forward * Vector3.Dot(toTip, t.forward);
                float radial = onPlane.magnitude;

                RectTransform rt = canvas.transform as RectTransform;
                if (rt != null)
                {
                    Vector3[] corners = new Vector3[4];
                    rt.GetWorldCorners(corners);
                    Bounds bounds = new Bounds(corners[0], Vector3.zero);
                    for (int c = 1; c < 4; c++)
                    {
                        bounds.Encapsulate(corners[c]);
                    }

                    bounds.Expand(nearUiDistance);
                    if (bounds.Contains(tipPos))
                    {
                        return true;
                    }
                }

                if (planar <= nearUiDistance && radial <= 0.55f)
                {
                    return true;
                }

                if ((tipPos - t.position).sqrMagnitude <= maxDistSq)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
