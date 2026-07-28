using UnityEditor;
using System.IO;

namespace Norscope.Editor
{
    /// <summary>
    /// Creates design-system prefabs once if missing after script compile.
    /// Does not overwrite existing prefabs.
    /// </summary>
    [InitializeOnLoad]
    internal static class NorscopeUiDesignSystemBootstrap
    {
        private const string PanelPrefab = "Assets/Norscope/UI/Panels/InteractionTestPanel.prefab";
        private const string FlagKey = "Norscope.UI.DesignSystem.BootstrapAttempted";

        static NorscopeUiDesignSystemBootstrap()
        {
            EditorApplication.delayCall += TryBuildOnce;
        }

        private static void TryBuildOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (File.Exists(PanelPrefab))
            {
                return;
            }

            if (SessionState.GetBool(FlagKey, false))
            {
                return;
            }

            SessionState.SetBool(FlagKey, true);
            try
            {
                NorscopeUiDesignSystemBuilder.Build();
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("[Norscope UI] Auto-build failed:\n" + ex);
            }
        }
    }
}
