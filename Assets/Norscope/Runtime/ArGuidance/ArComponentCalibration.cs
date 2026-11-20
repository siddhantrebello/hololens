using System;
using UnityEngine;

namespace Norscope.ArGuidance
{
    [Serializable]
    public sealed class ArComponentCalibration
    {
        public string componentId = string.Empty;
        public ArVisualType visualType = ArVisualType.None;
        public Vector3 localPosition = Vector3.zero;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
        public string updatedUtc = string.Empty;

        public void Sanitize()
        {
            if (componentId == null)
                componentId = string.Empty;
            if (localScale == Vector3.zero)
                localScale = Vector3.one;
            if (localRotation.Equals(default) ||
                (localRotation.x == 0f && localRotation.y == 0f &&
                 localRotation.z == 0f && localRotation.w == 0f))
                localRotation = Quaternion.identity;
        }
    }

    [Serializable]
    public sealed class ArGuidanceDefinition
    {
        public string componentId = string.Empty;
        public ArVisualType visualType = ArVisualType.None;
        public ArHologramVisualState state = ArHologramVisualState.Default;
    }

    [Serializable]
    public sealed class ArCalibrationFile
    {
        public int schemaVersion = 1;
        public string equipmentId = "dell_precision_3570";
        public string trackingProviderType = ManualTestOriginProvider.ProviderType;
        public string markerPayload = "NORSCOPE:DELL3570:DEMO01";
        public ArComponentCalibration[] components = Array.Empty<ArComponentCalibration>();
    }
}
