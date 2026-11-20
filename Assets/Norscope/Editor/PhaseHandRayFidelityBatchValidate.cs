using UnityEditor;
using UnityEngine;

namespace Norscope.Editor
{
    public static class PhaseHandRayFidelityBatchValidate
    {
        public static void ValidateOnly()
        {
            PhaseHandRayFidelityConfigurator.ValidateMenu();
            EditorApplication.Exit(0);
        }
    }
}
