using UnityEngine;

namespace Norscope.Audio
{
    /// <summary>
    /// Single entry point for every UI sound. Panels/controls call
    /// UIAudioManager.Play(UIAudioEvent.X) — never an AudioClip or AudioSource directly.
    /// Fully functional with no UIAudioTheme assigned and with every clip slot empty: both
    /// are silent no-ops, never an exception, never per-event log spam.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIAudioManager : MonoBehaviour
    {
        public static UIAudioManager Instance { get; private set; }

        [SerializeField] private UIAudioTheme theme;
        [SerializeField] private AudioSource audioSource;

        private bool _loggedMissingTheme;

        public void SetTheme(UIAudioTheme newTheme)
        {
            theme = newTheme;
            _loggedMissingTheme = false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[UIAudio] Duplicate UIAudioManager destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Plays the clip assigned to this event on the active theme, if any. Safe to
        /// call with no UIAudioManager in the scene, no theme assigned, or no clip assigned for
        /// this specific event — every one of those is a silent no-op, not an error.</summary>
        public static void Play(UIAudioEvent audioEvent)
        {
            UIAudioManager instance = Instance;
            if (instance == null)
            {
                return;
            }

            instance.PlayOnThisInstance(audioEvent);
        }

        /// <summary>Plays on this specific instance, bypassing the static Instance singleton
        /// routing. Real callers should use the static Play(); this exists as a directly
        /// testable seam (e.g. for a throwaway probe instance in editor validation).</summary>
        public void PlayOnThisInstance(UIAudioEvent audioEvent)
        {
            if (theme == null)
            {
                if (!_loggedMissingTheme)
                {
                    Debug.LogWarning("[UIAudio] No UIAudioTheme assigned — UI audio is silent.");
                    _loggedMissingTheme = true;
                }

                return;
            }

            AudioClip clip = theme.GetClip(audioEvent);
            if (clip == null || audioSource == null)
            {
                // No clip assigned for this event yet (the normal, expected state before sounds
                // are imported) — do nothing, no log, no exception.
                return;
            }

            audioSource.PlayOneShot(clip);
        }
    }
}
