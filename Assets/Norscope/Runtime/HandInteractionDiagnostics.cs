using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace Norscope
{
    /// <summary>
    /// Temporary Phase 3 hand diagnostics. Logs state changes only; HUD updates while enabled.
    /// </summary>
    public sealed class HandInteractionDiagnostics : MonoBehaviour
    {
        [Tooltip("Show Hand Diagnostics (Phase 3).")]
        [SerializeField] public bool showHandDiagnostics = true;

        [SerializeField] public Text statusText;

        private XRHandSubsystem _handSubsystem;
        private readonly StringBuilder _sb = new StringBuilder(640);

        private bool _leftTracked;
        private bool _rightTracked;
        private bool _leftAimValid;
        private bool _rightAimValid;
        private bool _leftNear;
        private bool _rightNear;
        private bool _leftFar;
        private bool _rightFar;
        private bool _leftSelect;
        private bool _rightSelect;
        private string _leftTarget = "-";
        private string _rightTarget = "-";

        private void OnEnable()
        {
            TryResolveHandSubsystem();
        }

        private void Update()
        {
            if (!showHandDiagnostics)
            {
                if (statusText != null && statusText.gameObject.activeSelf)
                {
                    statusText.gameObject.SetActive(false);
                }

                return;
            }

            if (statusText != null && !statusText.gameObject.activeSelf)
            {
                statusText.gameObject.SetActive(true);
            }

            if (_handSubsystem == null || !_handSubsystem.running)
            {
                TryResolveHandSubsystem();
            }

            bool leftTracked = _handSubsystem != null && _handSubsystem.leftHand.isTracked;
            bool rightTracked = _handSubsystem != null && _handSubsystem.rightHand.isTracked;
            LogChange(ref _leftTracked, leftTracked, "Left hand tracked");
            LogChange(ref _rightTracked, rightTracked, "Right hand tracked");

            HandSnapshot left = CaptureHand("Left Hand", leftTracked);
            HandSnapshot right = CaptureHand("Right Hand", rightTracked);

            LogChange(ref _leftAimValid, left.aimValid, "Left aim pose valid");
            LogChange(ref _rightAimValid, right.aimValid, "Right aim pose valid");
            LogChange(ref _leftNear, left.nearActive, "Left near active");
            LogChange(ref _rightNear, right.nearActive, "Right near active");
            LogChange(ref _leftFar, left.farActive, "Left far active");
            LogChange(ref _rightFar, right.farActive, "Right far active");
            LogChange(ref _leftSelect, left.selectActive, "Left select/pinch");
            LogChange(ref _rightSelect, right.selectActive, "Right select/pinch");

            if (left.targetName != _leftTarget)
            {
                Debug.Log("[Norscope HandDiag] Left target => " + left.targetName);
                _leftTarget = left.targetName;
            }

            if (right.targetName != _rightTarget)
            {
                Debug.Log("[Norscope HandDiag] Right target => " + right.targetName);
                _rightTarget = right.targetName;
            }

            if (statusText == null)
            {
                return;
            }

            _sb.Clear();
            AppendHand(_sb, "LEFT", left);
            _sb.AppendLine();
            AppendHand(_sb, "RIGHT", right);
            statusText.text = _sb.ToString();
        }

        private HandSnapshot CaptureHand(string handObjectName, bool tracked)
        {
            var snap = new HandSnapshot
            {
                tracked = tracked,
                targetName = "-",
                rayLength = Phase3HandRayCorrector.DiagnosticRayLengthMeters
            };

            Transform hand = FindNamedTransform(handObjectName);
            if (hand == null)
            {
                return snap;
            }

            Transform aim = FindChildRecursive(hand, "Aim Pose");
            if (aim != null)
            {
                // Aim Pose is driven by XRI Aim Position/Rotation (OpenXR pointer/aim).
                snap.aimValid = tracked && aim.gameObject.activeInHierarchy;
                snap.aimPosition = aim.position;
                snap.aimForward = aim.forward;
            }

            NearFarInteractor nearFar = hand.GetComponentInChildren<NearFarInteractor>(true);
            XRPokeInteractor poke = hand.GetComponentInChildren<XRPokeInteractor>(true);
            if (nearFar != null)
            {
                snap.selectActive = nearFar.isSelectActive;
                bool pokeBusy = poke != null && (poke.hasHover || poke.hasSelection || poke.isSelectActive);
                snap.nearActive = pokeBusy;
                snap.farActive = nearFar.isActiveAndEnabled && nearFar.enableFarCasting && !pokeBusy;

                if (nearFar.hasHover)
                {
                    var targets = nearFar.interactablesHovered;
                    if (targets != null && targets.Count > 0 && targets[0] is Component c)
                    {
                        snap.targetName = c.gameObject.name;
                    }
                }

                if (nearFar.farInteractionCaster is CurveInteractionCaster caster)
                {
                    snap.rayLength = caster.castDistance;
                    // Default-layer hits (room/spatial mesh) should be excluded by Phase3HandRayCorrector mask.
                    snap.hitIsSpatialOrFloor = ((1 << LayerMask.NameToLayer("Default")) & caster.raycastMask.value) != 0;
                }

                CurveVisualController visual = nearFar.GetComponentInChildren<CurveVisualController>(true);
                if (visual != null)
                {
                    snap.rayLength = visual.restingVisualLineLength;
                }
            }

            return snap;
        }

        private static Transform FindNamedTransform(string name)
        {
            GameObject go = GameObject.Find(name);
            return go != null ? go.transform : null;
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

        private void TryResolveHandSubsystem()
        {
            var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            _handSubsystem = subsystems.Count > 0 ? subsystems[0] : null;
        }

        private static void LogChange(ref bool previous, bool current, string label)
        {
            if (previous == current)
            {
                return;
            }

            previous = current;
            Debug.Log("[Norscope HandDiag] " + label + " => " + current);
        }

        private static void AppendHand(StringBuilder sb, string title, HandSnapshot s)
        {
            sb.Append(title).AppendLine();
            sb.Append("Tracked: ").AppendLine(s.tracked ? "Yes" : "No");
            sb.Append("Aim valid: ").AppendLine(s.aimValid ? "Yes" : "No");
            sb.Append("Aim pos: ").AppendLine(s.aimPosition.ToString("F3"));
            sb.Append("Aim fwd: ").AppendLine(s.aimForward.ToString("F3"));
            sb.Append("Near: ").Append(s.nearActive ? "On" : "Off")
                .Append("  Far: ").AppendLine(s.farActive ? "On" : "Off");
            sb.Append("Pinch/Select: ").AppendLine(s.selectActive ? "Active" : "Idle");
            sb.Append("Target: ").AppendLine(s.targetName);
            sb.Append("Ray length: ").Append(s.rayLength.ToString("F2")).AppendLine(" m");
            sb.Append("Default in cast mask: ").AppendLine(s.hitIsSpatialOrFloor ? "Yes (bad)" : "No");
        }

        private struct HandSnapshot
        {
            public bool tracked;
            public bool aimValid;
            public Vector3 aimPosition;
            public Vector3 aimForward;
            public bool nearActive;
            public bool farActive;
            public bool selectActive;
            public string targetName;
            public float rayLength;
            public bool hitIsSpatialOrFloor;
        }
    }
}
