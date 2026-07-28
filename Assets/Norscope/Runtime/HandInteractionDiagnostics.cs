using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Norscope
{
    /// <summary>
    /// Temporary Phase 3 diagnostics for hand interaction readiness.
    /// </summary>
    public sealed class HandInteractionDiagnostics : MonoBehaviour
    {
        [SerializeField]
        public bool showDiagnostics = true;

        [SerializeField]
        public Text statusText;

        private XRHandSubsystem _handSubsystem;

        private void OnEnable()
        {
            TryResolveHandSubsystem();
        }

        private void Update()
        {
            if (statusText == null)
            {
                return;
            }

            if (!showDiagnostics)
            {
                if (statusText.gameObject.activeSelf)
                {
                    statusText.gameObject.SetActive(false);
                }

                return;
            }

            if (!statusText.gameObject.activeSelf)
            {
                statusText.gameObject.SetActive(true);
            }

            if (_handSubsystem == null || !_handSubsystem.running)
            {
                TryResolveHandSubsystem();
            }

            bool leftTracked = _handSubsystem != null && _handSubsystem.leftHand.isTracked;
            bool rightTracked = _handSubsystem != null && _handSubsystem.rightHand.isTracked;

            bool farReady = HasEnabledInteractorOfType<NearFarInteractor>() || HasEnabledInteractorOfType<XRRayInteractor>();
            bool nearReady = HasEnabledInteractorOfType<NearFarInteractor>() || HasEnabledInteractorOfType<XRPokeInteractor>();
            bool pinchDetected = IsPinchLikely(leftTracked, rightTracked);

            statusText.text =
                "Left Hand\n" + (leftTracked ? "Tracked" : "Not Tracked") + "\n\n" +
                "Right Hand\n" + (rightTracked ? "Tracked" : "Not Tracked") + "\n\n" +
                "Far Interaction\n" + (farReady ? "Ready" : "Not Ready") + "\n\n" +
                "Near Interaction\n" + (nearReady ? "Ready" : "Not Ready") + "\n\n" +
                "Pinch\n" + (pinchDetected ? "Detected" : "Not Detected");
        }

        private void TryResolveHandSubsystem()
        {
            var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            _handSubsystem = subsystems.Count > 0 ? subsystems[0] : null;
        }

        private static bool HasEnabledInteractorOfType<T>() where T : MonoBehaviour
        {
            T[] found = FindObjectsByType<T>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].isActiveAndEnabled)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPinchLikely(bool leftTracked, bool rightTracked)
        {
            // Approximate pinch from XR select interactors while a hand is tracked.
            if (!leftTracked && !rightTracked)
            {
                return false;
            }

            XRBaseInputInteractor[] interactors = FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None);
            for (int i = 0; i < interactors.Length; i++)
            {
                XRBaseInputInteractor interactor = interactors[i];
                if (interactor != null && interactor.isActiveAndEnabled && interactor.isSelectActive)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
