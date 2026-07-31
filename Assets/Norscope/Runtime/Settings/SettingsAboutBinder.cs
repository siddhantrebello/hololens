using UnityEngine;

namespace Norscope.Settings
{
    /// <summary>
    /// Read-only About page value resolution. No persistence, no UI construction.
    /// </summary>
    public static class SettingsAboutBinder
    {
        public const string ApplicationName = "NORSCOPE";
        public const string PrototypeBuild = "Prototype Build";
        public const string Copyright = "© NORSCOPE";
        public const string CompanyFallback = "NORSCOPE";

        public static string Version =>
            string.IsNullOrWhiteSpace(Application.version)
                ? "0.0.0"
                : Application.version;

        public static string Build => PrototypeBuild;

        public static string Platform
        {
            get
            {
#if UNITY_EDITOR
                return "Windows Editor";
#elif UNITY_WSA
                return "Microsoft HoloLens 2";
#else
                return Application.platform.ToString();
#endif
            }
        }

        public static string UnityVersion =>
            string.IsNullOrWhiteSpace(Application.unityVersion)
                ? "Unknown"
                : Application.unityVersion;

        public static string Company =>
            string.IsNullOrWhiteSpace(Application.companyName)
                ? CompanyFallback
                : Application.companyName;
    }
}
