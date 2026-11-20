using System.IO;
using UnityEditor;
using UnityEngine;

namespace Norscope.Editor
{
    public static class PhaseHandRayFidelityBatch
    {
        public static void CaptureApplyValidate()
        {
            try
            {
                PhaseHandRayFidelityConfigurator.CaptureBaselineMenu();
                PhaseHandRayFidelityConfigurator.ApplyMenu();
                PhaseHandRayFidelityConfigurator.ValidateMenu();
                Debug.Log("[Norscope Hand Ray Fidelity] BATCH Capture+Apply+Validate finished.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Norscope Hand Ray Fidelity] BATCH FAILED: " + ex);
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }
    }
}
