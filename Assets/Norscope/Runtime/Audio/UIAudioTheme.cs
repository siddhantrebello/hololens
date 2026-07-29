using UnityEngine;

namespace Norscope.Audio
{
    /// <summary>
    /// Data-only sound bank: one AudioClip slot per UIAudioEvent. Every slot starts empty —
    /// the system is fully functional with no clips assigned at all. Assign clips later in the
    /// Inspector; no code changes are ever required to add sounds. Swap the whole theme by
    /// assigning a different UIAudioTheme asset to UIAudioManager.
    /// </summary>
    [CreateAssetMenu(fileName = "UIAudioTheme", menuName = "Norscope/Audio/UI Audio Theme", order = 40)]
    public sealed class UIAudioTheme : ScriptableObject
    {
        [Header("Buttons & Panels")]
        [SerializeField] private AudioClip buttonClick;
        [SerializeField] private AudioClip panelOpen;
        [SerializeField] private AudioClip panelClose;
        [SerializeField] private AudioClip panelMoveStart;
        [SerializeField] private AudioClip panelMoveEnd;
        [SerializeField] private AudioClip panelResizeStart;
        [SerializeField] private AudioClip panelResizeEnd;
        [SerializeField] private AudioClip windowSnap;

        [Header("Search")]
        [SerializeField] private AudioClip searchFocused;
        [SerializeField] private AudioClip searchCleared;
        [SerializeField] private AudioClip searchResult;

        [Header("Task Library")]
        [SerializeField] private AudioClip taskSelected;
        [SerializeField] private AudioClip taskOpened;

        [Header("Controls")]
        [SerializeField] private AudioClip toggleChanged;
        [SerializeField] private AudioClip sliderChanged;
        [SerializeField] private AudioClip dropdownOpened;
        [SerializeField] private AudioClip dropdownSelected;

        [Header("Notifications")]
        [SerializeField] private AudioClip notification;
        [SerializeField] private AudioClip success;
        [SerializeField] private AudioClip warning;
        [SerializeField] private AudioClip error;

        [Header("Loading")]
        [SerializeField] private AudioClip loadingStart;
        [SerializeField] private AudioClip loadingComplete;

        [Header("AI")]
        [SerializeField] private AudioClip aiThinking;
        [SerializeField] private AudioClip aiResponse;

        [Header("Voice")]
        [SerializeField] private AudioClip voiceStartListening;
        [SerializeField] private AudioClip voiceStopListening;
        [SerializeField] private AudioClip voiceRecognized;

        [Header("Files & Reports")]
        [SerializeField] private AudioClip reportGenerated;
        [SerializeField] private AudioClip fileDownloaded;
        [SerializeField] private AudioClip fileUploaded;
        [SerializeField] private AudioClip storageOperation;

        /// <summary>May return null — that is the normal, expected state for any event whose
        /// clip has not been assigned yet. Callers must treat null as "play nothing."</summary>
        public AudioClip GetClip(UIAudioEvent audioEvent)
        {
            switch (audioEvent)
            {
                case UIAudioEvent.ButtonClick: return buttonClick;
                case UIAudioEvent.PanelOpen: return panelOpen;
                case UIAudioEvent.PanelClose: return panelClose;
                case UIAudioEvent.PanelMoveStart: return panelMoveStart;
                case UIAudioEvent.PanelMoveEnd: return panelMoveEnd;
                case UIAudioEvent.PanelResizeStart: return panelResizeStart;
                case UIAudioEvent.PanelResizeEnd: return panelResizeEnd;
                case UIAudioEvent.WindowSnap: return windowSnap;
                case UIAudioEvent.SearchFocused: return searchFocused;
                case UIAudioEvent.SearchCleared: return searchCleared;
                case UIAudioEvent.SearchResult: return searchResult;
                case UIAudioEvent.TaskSelected: return taskSelected;
                case UIAudioEvent.TaskOpened: return taskOpened;
                case UIAudioEvent.ToggleChanged: return toggleChanged;
                case UIAudioEvent.SliderChanged: return sliderChanged;
                case UIAudioEvent.DropdownOpened: return dropdownOpened;
                case UIAudioEvent.DropdownSelected: return dropdownSelected;
                case UIAudioEvent.Notification: return notification;
                case UIAudioEvent.Success: return success;
                case UIAudioEvent.Warning: return warning;
                case UIAudioEvent.Error: return error;
                case UIAudioEvent.LoadingStart: return loadingStart;
                case UIAudioEvent.LoadingComplete: return loadingComplete;
                case UIAudioEvent.AIThinking: return aiThinking;
                case UIAudioEvent.AIResponse: return aiResponse;
                case UIAudioEvent.VoiceStartListening: return voiceStartListening;
                case UIAudioEvent.VoiceStopListening: return voiceStopListening;
                case UIAudioEvent.VoiceRecognized: return voiceRecognized;
                case UIAudioEvent.ReportGenerated: return reportGenerated;
                case UIAudioEvent.FileDownloaded: return fileDownloaded;
                case UIAudioEvent.FileUploaded: return fileUploaded;
                case UIAudioEvent.StorageOperation: return storageOperation;
                default: return null;
            }
        }
    }
}
