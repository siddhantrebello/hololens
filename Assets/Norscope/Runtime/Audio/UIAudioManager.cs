using UnityEngine;
using UnityEngine.Audio;
using Norscope.Settings;

namespace Norscope.Audio
{
    /// <summary>
    /// Single entry point for every UI sound. Panels/controls call
    /// UIAudioManager.Play(UIAudioEvent.X) — never an AudioClip or AudioSource directly.
    /// Fully functional with no UIAudioTheme assigned and with every clip slot empty: both
    /// are silent no-ops, never an exception, never per-event log spam.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class UIAudioManager : MonoBehaviour
    {
        public static UIAudioManager Instance { get; private set; }

        [SerializeField] private UIAudioTheme theme;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioSource alertAudioSource;
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string masterVolumeParameter = "MasterVolume";

        private bool _loggedMissingTheme;
        private bool _loggedFirstPlay;
        private bool _mixerVolumeApplied;
        private UIAudioEvent _lastPlayedEvent;
        private AudioClip _lastPlayedClip;
        private float _lastPlayedUnscaledTime = -999f;
        private const float DuplicatePlayWindowSeconds = 0.08f;

        public void SetTheme(UIAudioTheme newTheme)
        {
            theme = newTheme;
            _loggedMissingTheme = false;
            PreloadThemeClips();
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

            EnsureAudioListener();
            EnsureSources();
            PreloadThemeClips();
            BindAndApplyMasterVolume();
        }

        private void Start()
        {
            // Mixer SetFloat can fail during Awake before the mixer graph is live.
            EnsureAudioListener();
            BindAndApplyMasterVolume();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                if (NorscopeSettingsService.Instance != null)
                    NorscopeSettingsService.Instance.MasterVolumeChanged -=
                        ApplyMasterVolume;
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

            bool isAlert = IsAlertEvent(audioEvent);
            if (isAlert && NorscopeSettingsService.Instance != null &&
                !NorscopeSettingsService.Instance.Current.alertSoundsEnabled)
                return;

            AudioSource target = isAlert ? alertAudioSource : audioSource;
            AudioClip clip = theme.GetClip(audioEvent);
            if (clip == null || target == null)
            {
                // No clip assigned for this event yet (the normal, expected state before sounds
                // are imported) — do nothing, no log, no exception.
                return;
            }

            // XR UI often delivers duplicate submits (two rays / press+release). Swallow the
            // second play of the same event or same clip inside a short window.
            float now = Time.unscaledTime;
            if (now - _lastPlayedUnscaledTime < DuplicatePlayWindowSeconds &&
                (audioEvent == _lastPlayedEvent || clip == _lastPlayedClip))
            {
                return;
            }

            if (clip.loadState != AudioDataLoadState.Loaded)
                clip.LoadAudioData();

            target.PlayOneShot(clip);
            _lastPlayedEvent = audioEvent;
            _lastPlayedClip = clip;
            _lastPlayedUnscaledTime = now;

#if UNITY_EDITOR
            if (!_loggedFirstPlay)
            {
                _loggedFirstPlay = true;
                AudioListener listener = FindFirstObjectByType<AudioListener>();
                Debug.Log(
                    $"[UIAudio] First play: {audioEvent} clip='{clip.name}' " +
                    $"len={clip.length:F3}s load={clip.loadState} " +
                    $"srcVol={target.volume} mute={target.mute} " +
                    $"mixerOk={_mixerVolumeApplied} " +
                    $"listener={(listener != null ? listener.name : "NONE")} " +
                    $"editorMute={UnityEditor.EditorUtility.audioMasterMute}");
            }
#endif
        }

        public void ConfigureAudioRouting(
            AudioMixer mixer, AudioSource uiSource, AudioSource alertsSource)
        {
            audioMixer = mixer;
            audioSource = uiSource;
            alertAudioSource = alertsSource;
            EnsureSources();
        }

        public void ApplyMasterVolume(int percent)
        {
            float normalized = Mathf.Clamp(percent, 0, 100) / 100f;
            float db = AudioMixerVolumeUtility.NormalizedToDecibels(normalized);

            _mixerVolumeApplied = false;
            if (audioMixer != null)
            {
                _mixerVolumeApplied = audioMixer.SetFloat(masterVolumeParameter, db);
                if (!_mixerVolumeApplied)
                    _mixerVolumeApplied = audioMixer.SetFloat(masterVolumeParameter, db);
            }

            // Fallback only: keep sources at full when mixer owns MasterVolume so we don't
            // double-attenuate. If the mixer param isn't live yet, drive source gain instead.
            float sourceVolume = _mixerVolumeApplied ? 1f : normalized;
            if (audioSource != null)
                audioSource.volume = sourceVolume;
            if (alertAudioSource != null)
                alertAudioSource.volume = sourceVolume;
        }

        public static bool IsAlertEvent(UIAudioEvent audioEvent)
        {
            return audioEvent == UIAudioEvent.Notification ||
                   audioEvent == UIAudioEvent.Success ||
                   audioEvent == UIAudioEvent.Warning ||
                   audioEvent == UIAudioEvent.Error;
        }

        private void EnsureSources()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.mute = false;

            if (alertAudioSource == null)
            {
                AudioSource[] sources = GetComponents<AudioSource>();
                foreach (AudioSource source in sources)
                {
                    if (source != audioSource)
                    {
                        alertAudioSource = source;
                        break;
                    }
                }
            }

            if (alertAudioSource == null)
                alertAudioSource = gameObject.AddComponent<AudioSource>();

            alertAudioSource.playOnAwake = false;
            alertAudioSource.spatialBlend = 0f;
            alertAudioSource.mute = false;
        }

        private void BindAndApplyMasterVolume()
        {
            if (NorscopeSettingsService.Instance == null)
                return;

            NorscopeSettingsService.Instance.MasterVolumeChanged -= ApplyMasterVolume;
            NorscopeSettingsService.Instance.MasterVolumeChanged += ApplyMasterVolume;
            ApplyMasterVolume(NorscopeSettingsService.Instance.Current.masterVolume);
        }

        private static void EnsureAudioListener()
        {
            AudioListener existing = FindFirstObjectByType<AudioListener>();
            if (existing != null)
            {
                if (!existing.enabled)
                    existing.enabled = true;
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (cameras.Length > 0)
                    cam = cameras[0];
            }

            if (cam != null)
            {
                cam.gameObject.AddComponent<AudioListener>();
                Debug.LogWarning("[UIAudio] No AudioListener found — added one on " + cam.name + ".");
                return;
            }

            Debug.LogWarning("[UIAudio] No AudioListener and no Camera — UI audio cannot be heard.");
        }

        private void PreloadThemeClips()
        {
            if (theme == null)
                return;

            foreach (UIAudioEvent audioEvent in System.Enum.GetValues(typeof(UIAudioEvent)))
            {
                AudioClip clip = theme.GetClip(audioEvent);
                if (clip != null && clip.loadState != AudioDataLoadState.Loaded)
                    clip.LoadAudioData();
            }
        }
    }
}
