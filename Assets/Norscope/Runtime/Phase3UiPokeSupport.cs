using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Norscope
{
    /// <summary>
    /// Phase 3 helper: poke depth + optional canvas facing.
            /// Far-ray enable/disable is owned exclusively by PhaseHandRayNearFarArbiter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Phase3UiPokeSupport : MonoBehaviour
    {
        [SerializeField] private float pokeDepthMeters = 0.15f;
        [SerializeField] private bool faceCanvasesTowardCamera = true;
        [SerializeField] private XRPokeInteractor leftPoke;
        [SerializeField] private XRPokeInteractor rightPoke;

        private void Awake()
        {
            AutoBindPokesIfNeeded();
            ConfigurePokeDepth();
            if (faceCanvasesTowardCamera)
                FaceCanvases();
        }

        /// <summary>Optional sensor for tools/tests — does not drive far casting.</summary>
        public bool IsPokeSelectActive(bool rightHand)
        {
            XRPokeInteractor poke = rightHand ? rightPoke : leftPoke;
            return poke != null && poke.isActiveAndEnabled && poke.hasSelection;
        }

        public void Bind(XRPokeInteractor left, XRPokeInteractor right)
        {
            leftPoke = left;
            rightPoke = right;
            ConfigurePokeDepth();
        }

        /// <summary>Call after spawning runtime world-space UI (e.g. SpatialWindow).</summary>
        public void RefreshUiCanvases()
        {
            if (faceCanvasesTowardCamera)
                FaceCanvases();

            PhaseHandRayNearFarArbiter arbiter =
                GetComponent<PhaseHandRayNearFarArbiter>();
            if (arbiter == null)
                arbiter = FindFirstObjectByType<PhaseHandRayNearFarArbiter>();
            if (arbiter != null)
                arbiter.RefreshUiCanvases();
        }

        private void AutoBindPokesIfNeeded()
        {
            if (leftPoke != null && rightPoke != null)
                return;

            XRPokeInteractor[] pokes = GetComponentsInChildren<XRPokeInteractor>(true);
            if (leftPoke == null && pokes != null && pokes.Length > 0)
                leftPoke = pokes[0];
            if (rightPoke == null && pokes != null && pokes.Length > 1)
                rightPoke = pokes[1];
        }

        private void ConfigurePokeDepth()
        {
            if (leftPoke != null)
                leftPoke.pokeDepth = pokeDepthMeters;
            if (rightPoke != null)
                rightPoke.pokeDepth = pokeDepthMeters;
        }

        private void FaceCanvases()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            // Preserve prior named-target list only — do not add new raycasters broadly.
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
                    continue;

                Vector3 toCam = cam.transform.position - go.transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude < 0.0001f)
                    continue;

                go.transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);

                if (go.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                    go.AddComponent<TrackedDeviceGraphicRaycaster>();
            }
        }
    }
}
