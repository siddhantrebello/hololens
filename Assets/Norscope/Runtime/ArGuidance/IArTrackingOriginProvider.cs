using System;
using UnityEngine;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Tracking-origin seam. Phase 1 uses ManualTest; QR provider swaps in later
    /// without rewriting calibration or holograms.
    /// </summary>
    public interface IArTrackingOriginProvider
    {
        ArTrackingState TrackingState { get; }
        Transform OriginTransform { get; }
        bool IsValid { get; }
        string ProviderTypeId { get; }
        event Action<ArTrackingState> TrackingStateChanged;

        void Activate();
        void Deactivate();
        void ResetOrigin();

        /// <summary>
        /// Force a fresh origin acquisition without destroying the origin transform
        /// (keeps hologram roots parented). QR: resume scanning; Manual: re-place pose.
        /// </summary>
        void RequestRescan();
    }
}
