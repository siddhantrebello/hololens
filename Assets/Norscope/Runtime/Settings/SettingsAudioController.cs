using Norscope.Audio;
using Norscope.Settings.Motion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Norscope.Settings
{
    [DisallowMultipleComponent]
    public sealed class SettingsAudioController : MonoBehaviour
    {
        private const int ButtonStep = 5;

        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Button masterDecreaseButton;
        [SerializeField] private Button masterIncreaseButton;
        [SerializeField] private TMP_Text masterValueText;
        [SerializeField] private Button alertSoundsButton;
        [SerializeField] private Image alertToggleTrack;
        [SerializeField] private TMP_Text alertStatusText;
        [SerializeField] private Color alertEnabledColor =
            new Color(0.118f, 0.784f, 0.961f, 1f);
        [SerializeField] private Color alertDisabledColor =
            new Color(0.176f, 0.231f, 0.286f, 1f);

        [SerializeField] private SettingsVolumeFillVisual volumeFillVisual;
        [SerializeField] private SettingsToggleThumbVisual alertToggleVisual;

        private EventTrigger _sliderAudioTrigger;
        private bool _sliderInteractionArmed;
        private bool _visualsReady;

        public void Configure(
            Slider slider,
            Button decrease,
            Button increase,
            TMP_Text valueText,
            Button alertButton,
            Image alertTrack,
            TMP_Text alertStatus)
        {
            masterVolumeSlider = slider;
            masterDecreaseButton = decrease;
            masterIncreaseButton = increase;
            masterValueText = valueText;
            alertSoundsButton = alertButton;
            alertToggleTrack = alertTrack;
            alertStatusText = alertStatus;
        }

        public void ConfigureVisuals(
            SettingsVolumeFillVisual fillVisual,
            SettingsToggleThumbVisual toggleVisual)
        {
            volumeFillVisual = fillVisual;
            alertToggleVisual = toggleVisual;
        }

        private void OnEnable()
        {
            Wire(false);
            Wire(true);
            _visualsReady = false;
            RefreshWithoutNotify();
            _visualsReady = true;
        }

        private void OnDisable()
        {
            Wire(false);
        }

        private void Wire(bool add)
        {
            if (masterVolumeSlider != null)
            {
                if (add) masterVolumeSlider.onValueChanged.AddListener(OnMasterSlider);
                else masterVolumeSlider.onValueChanged.RemoveListener(OnMasterSlider);
            }
            WireSliderInteractionAudio(add);
            WireButton(masterDecreaseButton, DecreaseMaster, add);
            WireButton(masterIncreaseButton, IncreaseMaster, add);
            WireButton(alertSoundsButton, ToggleAlerts, add);
        }

        /// <summary>
        /// Plays SliderChanged once when a drag begins. Value-changed callbacks stay silent
        /// so the click never loops while dragging.
        /// </summary>
        private void WireSliderInteractionAudio(bool add)
        {
            if (masterVolumeSlider == null)
            {
                return;
            }

            if (!add)
            {
                if (_sliderAudioTrigger != null)
                {
                    _sliderAudioTrigger.triggers.Clear();
                }

                _sliderInteractionArmed = false;
                return;
            }

            _sliderAudioTrigger = masterVolumeSlider.GetComponent<EventTrigger>();
            if (_sliderAudioTrigger == null)
            {
                _sliderAudioTrigger = masterVolumeSlider.gameObject.AddComponent<EventTrigger>();
            }

            _sliderAudioTrigger.triggers.Clear();
            AddSliderTrigger(EventTriggerType.PointerDown, OnSliderInteractionBegin);
            AddSliderTrigger(EventTriggerType.PointerUp, OnSliderInteractionEnd);
            AddSliderTrigger(EventTriggerType.PointerExit, OnSliderInteractionEnd);
        }

        private void AddSliderTrigger(EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            _sliderAudioTrigger.triggers.Add(entry);
        }

        private void OnSliderInteractionBegin(BaseEventData _)
        {
            if (_sliderInteractionArmed)
            {
                return;
            }

            _sliderInteractionArmed = true;
            UIAudioManager.Play(UIAudioEvent.SliderChanged);
        }

        private void OnSliderInteractionEnd(BaseEventData _)
        {
            _sliderInteractionArmed = false;
        }

        private static void WireButton(
            Button button, UnityEngine.Events.UnityAction action, bool add)
        {
            if (button == null) return;
            if (add) button.onClick.AddListener(action);
            else button.onClick.RemoveListener(action);
        }

        private void RefreshWithoutNotify()
        {
            NorscopeSettingsData settings = NorscopeSettingsService.Instance != null
                ? NorscopeSettingsService.Instance.Current
                : NorscopeSettingsPersistence.CreateDefaults();
            float normalized = settings.masterVolume / 100f;
            if (masterVolumeSlider != null)
                masterVolumeSlider.SetValueWithoutNotify(normalized);
            UpdateMasterText(settings.masterVolume);
            UpdateStepButtonInteractable(settings.masterVolume);
            NotifyVolumeVisual(normalized, animate: false);
            UpdateAlertVisual(settings.alertSoundsEnabled, animate: false);
        }

        private void OnMasterSlider(float normalized)
        {
            int percent = Mathf.Clamp(Mathf.RoundToInt(normalized * 100f), 0, 100);
            if (NorscopeSettingsService.Instance != null)
                NorscopeSettingsService.Instance.SetMasterVolume(percent);
            UpdateMasterText(percent);
            UpdateStepButtonInteractable(percent);
            NotifyVolumeVisual(percent / 100f, animate: _visualsReady);
        }

        private void DecreaseMaster()
        {
            ChangeMaster(-ButtonStep);
        }

        private void IncreaseMaster()
        {
            ChangeMaster(ButtonStep);
        }

        private void ChangeMaster(int delta)
        {
            int current = NorscopeSettingsService.Instance != null
                ? NorscopeSettingsService.Instance.Current.masterVolume
                : Mathf.RoundToInt(masterVolumeSlider.value * 100f);
            int next = Mathf.Clamp(current + delta, 0, 100);
            if (masterVolumeSlider != null)
                masterVolumeSlider.SetValueWithoutNotify(next / 100f);
            if (NorscopeSettingsService.Instance != null)
                NorscopeSettingsService.Instance.SetMasterVolume(next);
            UpdateMasterText(next);
            UpdateStepButtonInteractable(next);
            NotifyVolumeVisual(next / 100f, animate: _visualsReady);
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
        }

        private void ToggleAlerts()
        {
            bool enabled = NorscopeSettingsService.Instance == null ||
                           !NorscopeSettingsService.Instance.Current.alertSoundsEnabled;
            if (NorscopeSettingsService.Instance != null)
                NorscopeSettingsService.Instance.SetAlertSoundsEnabled(enabled);
            UpdateAlertVisual(enabled, animate: _visualsReady);
            UIAudioManager.Play(UIAudioEvent.ToggleChanged);
        }

        private void UpdateMasterText(int percent)
        {
            if (masterValueText != null)
                masterValueText.text = Mathf.Clamp(percent, 0, 100) + "%";
        }

        private void UpdateStepButtonInteractable(int percent)
        {
            if (masterDecreaseButton != null)
                masterDecreaseButton.interactable = percent > 0;
            if (masterIncreaseButton != null)
                masterIncreaseButton.interactable = percent < 100;
        }

        private void NotifyVolumeVisual(float normalized, bool animate)
        {
            if (volumeFillVisual == null) return;
            if (animate)
                volumeFillVisual.SetNormalized(normalized, true);
            else
                volumeFillVisual.SnapNormalized(normalized);
        }

        private void UpdateAlertVisual(bool enabled, bool animate)
        {
            if (alertToggleVisual != null)
            {
                if (animate)
                    alertToggleVisual.SetOn(enabled, true);
                else
                    alertToggleVisual.SnapOn(enabled);
            }
            else if (alertToggleTrack != null)
            {
                alertToggleTrack.color = enabled
                    ? alertEnabledColor
                    : alertDisabledColor;
            }

            if (alertStatusText != null)
                alertStatusText.text = enabled ? "Enabled" : "Disabled";
        }
    }
}
