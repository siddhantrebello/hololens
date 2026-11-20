using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Norscope.Audio;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Shared show / hide / fade / visual-state transitions for AR holograms.
    /// Last requested transition wins; stale coroutines are cancelled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArVisibilityController : MonoBehaviour
    {
        public const float DefaultFadeOutSeconds = 0.18f;
        public const float DefaultGapSeconds = 0.05f;
        public const float DefaultFadeInSeconds = 0.25f;

        [SerializeField] private float fadeOutSeconds = DefaultFadeOutSeconds;
        [SerializeField] private float transitionGapSeconds = DefaultGapSeconds;
        [SerializeField] private float fadeInSeconds = DefaultFadeInSeconds;
        [SerializeField] private bool useUnscaledTime = true;

        private Coroutine _routine;
        private int _token;

        public void CancelPending()
        {
            _token++;
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        public void HideAllImmediate(IList<ArHologramVisualBase> visuals)
        {
            CancelPending();
            if (visuals == null)
                return;
            for (int i = 0; i < visuals.Count; i++)
            {
                ArHologramVisualBase v = visuals[i];
                if (v == null)
                    continue;
                v.SetOpacity(0f);
                v.SetVisible(false);
            }
        }

        /// <summary>
        /// Fade out previous, then fade in next with optional per-entry state/label.
        /// </summary>
        public void Transition(
            IList<ArHologramVisualBase> hide,
            IList<PendingShow> show,
            bool playAudio,
            UIAudioEvent audioEvent = UIAudioEvent.Notification)
        {
            CancelPending();
            int token = _token;
            _routine = StartCoroutine(TransitionRoutine(hide, show, playAudio, audioEvent, token));
        }

        private IEnumerator TransitionRoutine(
            IList<ArHologramVisualBase> hide,
            IList<PendingShow> show,
            bool playAudio,
            UIAudioEvent audioEvent,
            int token)
        {
            // Fade out
            if (hide != null && hide.Count > 0)
            {
                float duration = Mathf.Max(0.01f, fadeOutSeconds);
                float t = 0f;
                var startOpacities = CaptureOpacities(hide);
                while (t < duration)
                {
                    if (token != _token)
                        yield break;
                    t += Delta();
                    float u = Mathf.Clamp01(t / duration);
                    for (int i = 0; i < hide.Count; i++)
                    {
                        if (hide[i] == null)
                            continue;
                        hide[i].SetOpacity(Mathf.Lerp(startOpacities[i], 0f, u));
                    }

                    yield return null;
                }

                for (int i = 0; i < hide.Count; i++)
                {
                    if (hide[i] == null)
                        continue;
                    hide[i].SetOpacity(0f);
                    hide[i].SetVisible(false);
                }
            }

            if (transitionGapSeconds > 0f)
            {
                float gap = 0f;
                while (gap < transitionGapSeconds)
                {
                    if (token != _token)
                        yield break;
                    gap += Delta();
                    yield return null;
                }
            }

            if (playAudio)
                UIAudioManager.Play(audioEvent);

            // Prepare show targets
            if (show != null)
            {
                for (int i = 0; i < show.Count; i++)
                {
                    PendingShow p = show[i];
                    if (p.Visual == null || !p.Visible)
                        continue;
                    if (!string.IsNullOrEmpty(p.LabelOverride))
                        p.Visual.SetLabelText(p.LabelOverride);
                    p.Visual.SetVisualState(p.State);
                    p.Visual.SetOpacity(0f);
                    p.Visual.SetVisible(true);
                }

                float duration = Mathf.Max(0.01f, fadeInSeconds);
                float t = 0f;
                while (t < duration)
                {
                    if (token != _token)
                        yield break;
                    t += Delta();
                    float u = Mathf.Clamp01(t / duration);
                    for (int i = 0; i < show.Count; i++)
                    {
                        PendingShow p = show[i];
                        if (p.Visual == null || !p.Visible)
                            continue;
                        p.Visual.SetOpacity(Mathf.Lerp(0f, p.TargetOpacity, u));
                    }

                    yield return null;
                }

                for (int i = 0; i < show.Count; i++)
                {
                    PendingShow p = show[i];
                    if (p.Visual == null || !p.Visible)
                        continue;
                    p.Visual.SetOpacity(p.TargetOpacity);
                }
            }

            if (token == _token)
                _routine = null;
        }

        private float Delta() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        private static float[] CaptureOpacities(IList<ArHologramVisualBase> visuals)
        {
            var result = new float[visuals.Count];
            for (int i = 0; i < visuals.Count; i++)
                result[i] = visuals[i] != null ? visuals[i].Opacity : 0f;
            return result;
        }

        public struct PendingShow
        {
            public ArHologramVisualBase Visual;
            public bool Visible;
            public ArHologramVisualState State;
            public string LabelOverride;
            public float TargetOpacity;

            public PendingShow(
                ArHologramVisualBase visual,
                bool visible,
                ArHologramVisualState state,
                string labelOverride,
                float targetOpacity = 0.85f)
            {
                Visual = visual;
                Visible = visible;
                State = state;
                LabelOverride = labelOverride;
                TargetOpacity = targetOpacity;
            }
        }
    }
}
