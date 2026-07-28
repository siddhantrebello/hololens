using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Norscope.Editor
{
    /// <summary>
    /// HoloLens-only UWP projects do not need the WindowsMobile Extension SDK.
    /// Unity may still emit that SDKReference; strip it so Master ARM64 builds succeed
    /// on machines without the Mobile SDK installed.
    /// </summary>
    internal sealed class StripWindowsMobileSdkReference : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WSAPlayer)
            {
                return;
            }

            string root = report.summary.outputPath;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return;
            }

            foreach (string vcxproj in Directory.GetFiles(root, "*.vcxproj", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(vcxproj);
                string cleaned = Regex.Replace(
                    text,
                    "\\s*<SDKReference\\s+Include=\"WindowsMobile,[^\"]*\"\\s*/>\\s*",
                    "\n",
                    RegexOptions.IgnoreCase);

                if (cleaned != text)
                {
                    File.WriteAllText(vcxproj, cleaned);
                    Debug.Log("[Norscope] Removed WindowsMobile SDKReference from " + vcxproj);
                }
            }
        }
    }
}
