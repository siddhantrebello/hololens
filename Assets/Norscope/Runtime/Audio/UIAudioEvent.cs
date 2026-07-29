namespace Norscope.Audio
{
    /// <summary>
    /// Semantic UI sound events. Panels/controls trigger these by name via UIAudioManager —
    /// never an AudioClip or AudioSource directly. Add a new event here + a matching field on
    /// UIAudioTheme when a new sound category is needed; nothing else changes.
    /// </summary>
    public enum UIAudioEvent
    {
        ButtonClick,
        PanelOpen,
        PanelClose,
        PanelMoveStart,
        PanelMoveEnd,
        PanelResizeStart,
        PanelResizeEnd,
        WindowSnap,
        SearchFocused,
        SearchCleared,
        SearchResult,
        TaskSelected,
        TaskOpened,
        ToggleChanged,
        SliderChanged,
        DropdownOpened,
        DropdownSelected,
        Notification,
        Success,
        Warning,
        Error,
        LoadingStart,
        LoadingComplete,
        AIThinking,
        AIResponse,
        VoiceStartListening,
        VoiceStopListening,
        VoiceRecognized,
        ReportGenerated,
        FileDownloaded,
        FileUploaded,
        StorageOperation
    }
}
