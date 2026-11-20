using System.Collections.Generic;
using Norscope.Navigation;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Norscope
{
    /// <summary>
    /// Sole runtime writer of <see cref="NearFarInteractor.enableFarCasting"/> for
    /// HoloLens near/far hand-ray arbitration. Left and right are independent.
    /// Enter near at &lt;= 0.10 m or while poke select is active; exit only when
    /// poke is idle and distance &gt;= 0.16 m.
    /// Body-follow HomeLauncher canvases are excluded from proximity so mid-reach
    /// across the home bar does not kill far casting.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class PhaseHandRayNearFarArbiter : MonoBehaviour
    {
        public const float DefaultEnterNearMeters = 0.10f;
        public const float DefaultExitNearMeters = 0.16f;

        private enum NearFarMode
        {
            Far = 0,
            Near = 1
        }

        private enum TransitionReason
        {
            Proximity = 0,
            Poke = 1,
            TrackingLost = 2,
            Disabled = 3
        }

        [Header("Hands (cached — wire in scene or Auto Bind)")]
        [SerializeField] private NearFarInteractor leftNearFar;
        [SerializeField] private XRPokeInteractor leftPoke;
        [SerializeField] private NearFarInteractor rightNearFar;
        [SerializeField] private XRPokeInteractor rightPoke;

        [Header("Hysteresis")]
        [SerializeField] private float enterNearMeters = DefaultEnterNearMeters;
        [SerializeField] private float exitNearMeters = DefaultExitNearMeters;

        [Header("UI proximity")]
        [SerializeField] private float uiPlaneRadialMeters = 0.55f;
        [SerializeField] private float canvasRefreshIntervalSeconds = 0.25f;

        [Header("Diagnostics")]
        [SerializeField] private bool enableStateChangeLogs;

        private NearFarMode _leftMode = NearFarMode.Far;
        private NearFarMode _rightMode = NearFarMode.Far;

        private Canvas[] _uiCanvases = System.Array.Empty<Canvas>();
        private int _cachedCanvasCount = -1;
        private float _nextCanvasRefreshTime;
        private int _uiLayerMask;

        // Reused for bounds without per-frame alloc of corner arrays beyond one buffer.
        private readonly Vector3[] _cornerBuffer = new Vector3[4];
        private readonly List<Canvas> _canvasScratch = new List<Canvas>(16);

        public float EnterNearMeters => enterNearMeters;
        public float ExitNearMeters => exitNearMeters;
        public NearFarInteractor LeftNearFar => leftNearFar;
        public NearFarInteractor RightNearFar => rightNearFar;

        private void Awake()
        {
            if (exitNearMeters <= enterNearMeters)
                exitNearMeters = enterNearMeters + 0.06f;

            _uiLayerMask = LayerMask.GetMask("UI");
            AutoBindIfNeeded();
            RefreshUiCanvases();
            ForceFar(TransitionReason.Disabled);
        }

        private void OnEnable()
        {
            AutoBindIfNeeded();
            RefreshUiCanvases();
        }

        private void OnDisable()
        {
            // Safe restore so destroying/disabling the arbiter cannot leave rays stuck off.
            ApplyMode(ref _leftMode, "Left", leftNearFar, NearFarMode.Far, TransitionReason.Disabled);
            ApplyMode(ref _rightMode, "Right", rightNearFar, NearFarMode.Far, TransitionReason.Disabled);
        }

        private void OnDestroy()
        {
            ApplyMode(ref _leftMode, "Left", leftNearFar, NearFarMode.Far, TransitionReason.Disabled);
            ApplyMode(ref _rightMode, "Right", rightNearFar, NearFarMode.Far, TransitionReason.Disabled);
        }

        private void LateUpdate()
        {
            RefreshUiCanvasesIfNeeded();
            UpdateHand(ref _leftMode, "Left", leftNearFar, leftPoke);
            UpdateHand(ref _rightMode, "Right", rightNearFar, rightPoke);
        }

        /// <summary>Editor/Apply helper — bind from hand roots without name lookup at runtime.</summary>
        public void Bind(
            NearFarInteractor leftFar,
            XRPokeInteractor leftPokeInteractor,
            NearFarInteractor rightFar,
            XRPokeInteractor rightPokeInteractor)
        {
            leftNearFar = leftFar;
            leftPoke = leftPokeInteractor;
            rightNearFar = rightFar;
            rightPoke = rightPokeInteractor;
        }

        public void RefreshUiCanvases()
        {
            Canvas[] found = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            _canvasScratch.Clear();
            if (found != null)
            {
                for (int i = 0; i < found.Length; i++)
                {
                    Canvas canvas = found[i];
                    if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                        continue;
                    if (IsBodyFollowUiCanvas(canvas))
                        continue;
                    _canvasScratch.Add(canvas);
                }
            }

            if (_uiCanvases == null || _uiCanvases.Length != _canvasScratch.Count)
                _uiCanvases = new Canvas[_canvasScratch.Count];
            for (int i = 0; i < _canvasScratch.Count; i++)
                _uiCanvases[i] = _canvasScratch[i];

            _cachedCanvasCount = _uiCanvases.Length;
            _nextCanvasRefreshTime = Time.unscaledTime + canvasRefreshIntervalSeconds;
        }

        /// <summary>
        /// HomeLauncher / HomeLauncherFollow sit ~0.5 m from the head and must not
        /// drive near/far proximity. Called only during canvas refresh, not per frame.
        /// </summary>
        private static bool IsBodyFollowUiCanvas(Canvas canvas)
        {
            if (canvas == null)
                return false;

            Transform t = canvas.transform;
            if (t.GetComponentInParent<HomeLauncherFollow>(true) != null)
                return true;
            if (t.GetComponentInParent<HomeLauncher>(true) != null)
                return true;
            return false;
        }

        private void AutoBindIfNeeded()
        {
            if (leftNearFar != null && leftPoke != null &&
                rightNearFar != null && rightPoke != null)
                return;

            // One-time bind from this hierarchy (Apply wires explicitly; this is fallback).
            NearFarInteractor[] fars = GetComponentsInChildren<NearFarInteractor>(true);
            XRPokeInteractor[] pokes = GetComponentsInChildren<XRPokeInteractor>(true);
            if (leftNearFar == null && fars != null && fars.Length > 0)
                leftNearFar = fars[0];
            if (rightNearFar == null && fars != null && fars.Length > 1)
                rightNearFar = fars[1];
            if (leftPoke == null && pokes != null && pokes.Length > 0)
                leftPoke = pokes[0];
            if (rightPoke == null && pokes != null && pokes.Length > 1)
                rightPoke = pokes[1];
        }

        private void ForceFar(TransitionReason reason)
        {
            ApplyMode(ref _leftMode, "Left", leftNearFar, NearFarMode.Far, reason);
            ApplyMode(ref _rightMode, "Right", rightNearFar, NearFarMode.Far, reason);
        }

        private void UpdateHand(
            ref NearFarMode mode,
            string handLabel,
            NearFarInteractor nearFar,
            XRPokeInteractor poke)
        {
            if (nearFar == null)
                return;

            bool trackingValid = nearFar.isActiveAndEnabled && nearFar.gameObject.activeInHierarchy;
            if (!trackingValid)
            {
                ApplyMode(ref mode, handLabel, nearFar, NearFarMode.Far, TransitionReason.TrackingLost);
                return;
            }

            bool pokeActive = IsPokeSelectActive(poke);
            float distance = MeasureMinUiDistance(poke);

            if (mode == NearFarMode.Far)
            {
                bool enterNear = pokeActive || distance <= enterNearMeters;
                if (enterNear)
                {
                    TransitionReason reason = pokeActive
                        ? TransitionReason.Poke
                        : TransitionReason.Proximity;
                    ApplyMode(ref mode, handLabel, nearFar, NearFarMode.Near, reason);
                }
            }
            else
            {
                // Stay near until BOTH idle poke and beyond exit distance.
                bool exitNear = !pokeActive && distance >= exitNearMeters;
                if (exitNear)
                    ApplyMode(ref mode, handLabel, nearFar, NearFarMode.Far, TransitionReason.Proximity);
            }
        }

        private static bool IsPokeSelectActive(XRPokeInteractor poke)
        {
            if (poke == null || !poke.isActiveAndEnabled)
                return false;
            return poke.hasSelection;
        }

        private float MeasureMinUiDistance(XRPokeInteractor poke)
        {
            if (poke == null || _uiCanvases == null || _uiCanvases.Length == 0)
                return float.PositiveInfinity;

            Transform tip = poke.GetAttachTransform(null);
            if (tip == null)
                tip = poke.transform;

            Vector3 tipPos = tip.position;
            float best = float.PositiveInfinity;

            for (int i = 0; i < _uiCanvases.Length; i++)
            {
                Canvas canvas = _uiCanvases[i];
                if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                    continue;

                if (((1 << canvas.gameObject.layer) & _uiLayerMask) == 0)
                    continue;

                Transform t = canvas.transform;
                Vector3 toTip = tipPos - t.position;
                float planar = Mathf.Abs(Vector3.Dot(toTip, t.forward));
                Vector3 onPlane = toTip - t.forward * Vector3.Dot(toTip, t.forward);
                float radial = onPlane.magnitude;

                RectTransform rt = t as RectTransform;
                if (rt != null)
                {
                    rt.GetWorldCorners(_cornerBuffer);
                    Bounds bounds = new Bounds(_cornerBuffer[0], Vector3.zero);
                    for (int c = 1; c < 4; c++)
                        bounds.Encapsulate(_cornerBuffer[c]);

                    // Inflate by enter distance so containment maps to proximity.
                    bounds.Expand(enterNearMeters * 2f);
                    if (bounds.Contains(tipPos))
                    {
                        // Approximate distance as planar separation when inside inflated bounds.
                        best = Mathf.Min(best, planar);
                        continue;
                    }
                }

                if (radial <= uiPlaneRadialMeters)
                    best = Mathf.Min(best, planar);
                else
                    best = Mathf.Min(best, toTip.magnitude);
            }

            return best;
        }

        private void ApplyMode(
            ref NearFarMode mode,
            string handLabel,
            NearFarInteractor nearFar,
            NearFarMode next,
            TransitionReason reason)
        {
            if (nearFar == null)
            {
                mode = next;
                return;
            }

            bool enableFar = next == NearFarMode.Far;
            if (nearFar.enableFarCasting != enableFar)
                nearFar.enableFarCasting = enableFar;

            if (mode == next)
                return;

            mode = next;
            LogTransition(handLabel, next, reason);
        }

        private void LogTransition(string handLabel, NearFarMode mode, TransitionReason reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!enableStateChangeLogs)
                return;

            string action = mode == NearFarMode.Near ? "entered Near" : "returned Far";
            string why;
            switch (reason)
            {
                case TransitionReason.Poke:
                    why = "poke";
                    break;
                case TransitionReason.Proximity:
                    why = "proximity";
                    break;
                case TransitionReason.TrackingLost:
                    why = "tracking lost";
                    break;
                default:
                    why = "disabled";
                    break;
            }

            Debug.Log("[Norscope HandRay] " + handLabel + " " + action + " | reason: " + why, this);
#else
            // Production Master: no diagnostics.
#endif
        }

        private void RefreshUiCanvasesIfNeeded()
        {
            if (Time.unscaledTime < _nextCanvasRefreshTime &&
                _uiCanvases != null &&
                _uiCanvases.Length > 0)
                return;

            RefreshUiCanvases();
        }
    }
}
