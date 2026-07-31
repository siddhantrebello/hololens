using UnityEngine;

namespace Norscope.Audio
{
    public static class AudioMixerVolumeUtility
    {
        public const float MuteDecibels = -80f;

        public static float NormalizedToDecibels(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            return normalized <= 0.0001f
                ? MuteDecibels
                : Mathf.Clamp(20f * Mathf.Log10(normalized), MuteDecibels, 0f);
        }
    }
}
