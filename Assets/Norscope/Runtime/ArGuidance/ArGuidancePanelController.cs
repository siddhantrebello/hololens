using System.Collections.Generic;
using Norscope.Audio;
using Norscope.TaskExecution;
using Norscope.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.ArGuidance
{
    public enum ArGuidancePanelMode
    {
        Preview = 0,
        Calibration = 1
    }

    /// <summary>
    /// Prefab-authored AR Guidance panel controller. Preview wiring + thin calibration bind.
    /// Precision nudge / hold / dirty UX lives in ArCalibrationNudgeController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArGuidancePanelController : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text equipmentText;
        [SerializeField] private TMP_Text trackingStatusText;

        [Header("Mode roots")]
        [SerializeField] private GameObject previewView;
        [SerializeField] private GameObject calibrationView;
        [SerializeField] private Button previewModeButton;
        [SerializeField] private Button calibrationModeButton;
        [SerializeField] private GameObject previewTabUnderline;
        [SerializeField] private GameObject calibrationTabUnderline;

        [Header("Preview toggles")]
        [SerializeField] private Toggle toggleLaptopOutline;
        [SerializeField] private Toggle toggleLiftEdge;
        [SerializeField] private Toggle toggleBatteryConnector;
        [SerializeField] private Toggle toggleCoolingFan;
        [SerializeField] private Toggle toggleFanAirflow;
        [SerializeField] private Button hideAllButton;

        [Header("Preview toggle state labels (\"Show\" / \"Visible\")")]
        [SerializeField] private TMP_Text toggleLaptopOutlineLabel;
        [SerializeField] private TMP_Text toggleLiftEdgeLabel;
        [SerializeField] private TMP_Text toggleBatteryConnectorLabel;
        [SerializeField] private TMP_Text toggleCoolingFanLabel;
        [SerializeField] private TMP_Text toggleFanAirflowLabel;

        [Header("Preview toggles — H04 screws (9 independent components)")]
        [SerializeField] private Toggle[] h04ScrewToggles = new Toggle[9];
        [SerializeField] private TMP_Text[] h04ScrewToggleLabels = new TMP_Text[9];

        [Header("Preview Guidance (no task required)")]
        [SerializeField] private Button previewGuidanceStartButton;
        [SerializeField] private Button previewGuidancePrevButton;
        [SerializeField] private Button previewGuidanceNextButton;
        [SerializeField] private Button previewGuidanceStopButton;
        [SerializeField] private TMP_Text previewGuidanceStatusText;

        [Header("Calibration (shared)")]
        [SerializeField] private TMP_Dropdown componentDropdown;
        [SerializeField] private Button showSelectedButton;
        [SerializeField] private ArCalibrationNudgeController nudgeController;
        [SerializeField] private ArCalibrationSelectionController selectionController;
        [SerializeField] private NorscopeBadgeControl calBadge;
        [SerializeField] private TMP_Text calibratingNameText;
        [SerializeField] private TMP_Text selectedComponentInfoText;

        [Header("System")]
        [SerializeField] private ArGuidanceSystem guidanceSystem;
        [SerializeField] private ArGuidanceProfile profile;

        private ArGuidancePanelMode _mode = ArGuidancePanelMode.Preview;
        private bool _ignoreDropdown;

        /// <summary>The single authoritative index-to-componentId mapping for componentDropdown.
        /// Built exactly once per rebuild, from exactly one source (BuildCalibrationComponentOrder),
        /// and used for both the dropdown's visible labels and its onValueChanged resolution — so
        /// dropdown index i can never mean a different component than list index i.</summary>
        private readonly List<string> calibrationDropdownIds = new List<string>();

        private void Awake()
        {
            EnsureUiRaycastReady();
            if (selectionController == null)
                selectionController = GetComponent<ArCalibrationSelectionController>();
            if (nudgeController == null)
                nudgeController = GetComponent<ArCalibrationNudgeController>();
            WireUi();
        }

        private void EnsureUiRaycastReady()
        {
            GraphicRaycaster[] raycasters = GetComponents<GraphicRaycaster>();
            for (int i = 0; i < raycasters.Length; i++)
            {
                if (raycasters[i] == null)
                    continue;
                raycasters[i].enabled = false;
                Destroy(raycasters[i]);
            }

            Canvas nested = GetComponent<Canvas>();
            if (nested != null)
            {
                nested.enabled = false;
                Destroy(nested);
            }

            Toggle[] toggles =
            {
                toggleLaptopOutline, toggleLiftEdge,
                toggleBatteryConnector, toggleCoolingFan, toggleFanAirflow
            };
            for (int i = 0; i < toggles.Length; i++)
                EnsureToggleHit(toggles[i]);
            if (h04ScrewToggles != null)
                for (int i = 0; i < h04ScrewToggles.Length; i++)
                    EnsureToggleHit(h04ScrewToggles[i]);

            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                    continue;
                Image img = button.GetComponent<Image>();
                if (img == null)
                    continue;
                img.raycastTarget = true;
                button.targetGraphic = img;
            }
        }

        private static void EnsureToggleHit(Toggle toggle)
        {
            if (toggle == null)
                return;
            Image hit = toggle.GetComponent<Image>();
            if (hit == null)
            {
                hit = toggle.gameObject.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0.01f);
            }

            hit.raycastTarget = true;
            toggle.targetGraphic = hit;
        }

        private void OnEnable()
        {
            EnsureSystem();
            if (guidanceSystem != null)
                guidanceSystem.BeginSession();
            // Fresh QR scan every time Developer opens.
            guidanceSystem?.RequestOriginRescan();
            // Phase-1 leftovers / broken prefabs must never stay visible on open.
            guidanceSystem?.HideAllHolograms();
            SyncPreviewToggles(false);
            RefreshHeader();
            BindCalibrationControllers();
            SetMode(ArGuidancePanelMode.Preview);
        }

        private void OnDisable()
        {
            nudgeController?.ForceRestoreIfMinimized();
            nudgeController?.Unbind();
            guidanceSystem?.Calibration?.HidePreview();
            guidanceSystem?.TaskGuidance?.SetCalibrationPause(false);

            // Always close every hologram when Developer closes (even mid-task).
            guidanceSystem?.HideAllHolograms();
            guidanceSystem?.EndSession();
        }

        public void Configure(ArGuidanceSystem system, ArGuidanceProfile equipmentProfile)
        {
            guidanceSystem = system;
            profile = equipmentProfile;
            if (guidanceSystem != null && profile != null)
                guidanceSystem.SetProfile(profile);
        }

        private void EnsureSystem()
        {
            if (guidanceSystem == null)
                guidanceSystem = ArGuidanceSystem.Instance;
            if (guidanceSystem == null)
            {
                var go = new GameObject("ArGuidanceSystem");
                guidanceSystem = go.AddComponent<ArGuidanceSystem>();
            }

            if (profile != null)
                guidanceSystem.SetProfile(profile);
        }

        private void BindCalibrationControllers()
        {
            if (guidanceSystem?.Calibration == null)
                return;
            if (selectionController == null)
                selectionController = gameObject.AddComponent<ArCalibrationSelectionController>();
            if (nudgeController == null)
                nudgeController = gameObject.AddComponent<ArCalibrationNudgeController>();

            guidanceSystem.Calibration.SelectionResolved -= OnSelectionResolved;
            guidanceSystem.Calibration.SelectionResolved += OnSelectionResolved;

            RebuildCalibrationDropdown();
            nudgeController.Bind(guidanceSystem.Calibration, selectionController);
            SyncDropdownFromSelection();
            RefreshCalBadge();
        }

        private string[] BuildCalibrationComponentOrder()
        {
            ArHologramLibrary library = guidanceSystem != null && guidanceSystem.Profile != null
                ? guidanceSystem.Profile.hologramLibrary
                : null;
            return ArCalibrationIds.BuildLibraryLeafOrder(library);
        }

        /// <summary>The one place calibrationDropdownIds is (re)populated. Dropdown labels are
        /// built from this exact list — never from a second, separately-computed order — and
        /// selectionController is bound to the exact same list, so its index-based navigation
        /// (Prev/Next, pending-nav-confirm) can never disagree with the dropdown's own indices.</summary>
        private void RebuildCalibrationDropdown()
        {
            calibrationDropdownIds.Clear();
            calibrationDropdownIds.AddRange(BuildCalibrationComponentOrder());

            if (componentDropdown != null)
            {
                _ignoreDropdown = true;
                componentDropdown.ClearOptions();
                var labels = new List<string>(calibrationDropdownIds.Count);
                for (int i = 0; i < calibrationDropdownIds.Count; i++)
                    labels.Add(ArCalibrationIds.DisplayName(calibrationDropdownIds[i]));
                componentDropdown.AddOptions(labels);
                componentDropdown.value = 0;
                _ignoreDropdown = false;
            }

            if (selectionController != null)
                selectionController.Bind(guidanceSystem?.Calibration, calibrationDropdownIds.ToArray(), SelectAndShowComponent);
        }

        /// <summary>The single authoritative "make this the shown, manipulable calibration
        /// component" entry point. Every selection-changing path (dropdown, Prev/Next via
        /// selectionController, pending-nav confirm/discard) funnels through this — never
        /// through a duplicate inline copy of the same steps.</summary>
        private void SelectAndShowComponent(string componentId)
        {
            ArCalibrationController calibration = guidanceSystem != null ? guidanceSystem.Calibration : null;
            if (calibration == null || string.IsNullOrEmpty(componentId))
                return;

            calibration.SelectComponent(componentId);
            calibration.ShowSelected(true);
            nudgeController?.RefreshAll();
            SyncDropdownFromSelection();
            RefreshCalBadge();
        }

        /// <summary>Drives the Developer panel's Saved/Unsaved Changes/Never Calibrated badge
        /// from the same repository signal the calibration system itself uses — never a
        /// fabricated state. Called after every selection change and on every dirty-state
        /// change (subscribed via selectionController.DirtyChanged in WireUi).</summary>
        private void RefreshCalBadge()
        {
            if (calBadge == null)
                return;
            ArCalibrationController calibration = guidanceSystem != null ? guidanceSystem.Calibration : null;
            string componentId = calibration != null ? calibration.SelectedComponentId : null;
            if (calibration == null || string.IsNullOrEmpty(componentId))
                return;

            if (!calibration.HasSavedCalibration(componentId))
            {
                calBadge.Kind = NorscopeBadgeControl.BadgeKind.Neutral;
                calBadge.Text = "Never Calibrated";
            }
            else if (calibration.IsDirtyVersusSaved())
            {
                calBadge.Kind = NorscopeBadgeControl.BadgeKind.Warning;
                calBadge.Text = "Unsaved Changes";
            }
            else
            {
                calBadge.Kind = NorscopeBadgeControl.BadgeKind.Ready;
                calBadge.Text = "Saved";
            }
        }

        private void WireUi()
        {
            Wire(previewModeButton, () => SetMode(ArGuidancePanelMode.Preview));
            Wire(calibrationModeButton, () => SetMode(ArGuidancePanelMode.Calibration));
            Wire(hideAllButton, () =>
            {
                guidanceSystem?.Renderer?.HideAll();
                SyncPreviewToggles(false);
            });

            WireToggle(toggleLaptopOutline, "h03_back_panel_outline", toggleLaptopOutlineLabel);
            WireToggle(toggleLiftEdge, "h05_lift_here", toggleLiftEdgeLabel);
            WireToggle(toggleBatteryConnector, "h18_battery_connector", toggleBatteryConnectorLabel);
            WireToggle(toggleCoolingFan, "cooling_fan", toggleCoolingFanLabel);
            WireToggle(toggleFanAirflow, "fan_airflow", toggleFanAirflowLabel);
            if (h04ScrewToggles != null)
            {
                for (int i = 0; i < h04ScrewToggles.Length; i++)
                {
                    string componentId = "h04_screw_" + (i + 1).ToString("00");
                    TMP_Text label = h04ScrewToggleLabels != null && i < h04ScrewToggleLabels.Length
                        ? h04ScrewToggleLabels[i]
                        : null;
                    WireToggle(h04ScrewToggles[i], componentId, label);
                }
            }

            // Options are populated later by RebuildCalibrationDropdown (called from
            // BindCalibrationControllers, once BeginSession has actually resolved which
            // components exist) — building them here too, before that, would only produce a
            // second, stale copy of the option list that gets thrown away. Listener wiring is
            // still done here, once, in Awake.
            if (componentDropdown != null)
            {
                componentDropdown.onValueChanged.RemoveListener(OnComponentDropdown);
                componentDropdown.onValueChanged.AddListener(OnComponentDropdown);
            }

            Wire(showSelectedButton, () =>
                guidanceSystem?.Calibration?.ShowSelected(true));

            Wire(previewGuidanceStartButton, () =>
            {
                guidanceSystem?.TaskGuidance?.EnterPreview();
                RefreshPreviewGuidanceStatus();
            });
            Wire(previewGuidancePrevButton, () =>
            {
                guidanceSystem?.TaskGuidance?.PreviewPrevious();
                RefreshPreviewGuidanceStatus();
            });
            Wire(previewGuidanceNextButton, () =>
            {
                guidanceSystem?.TaskGuidance?.PreviewNext();
                RefreshPreviewGuidanceStatus();
            });
            Wire(previewGuidanceStopButton, () =>
            {
                guidanceSystem?.TaskGuidance?.ExitPreview();
                RefreshPreviewGuidanceStatus();
            });

            if (selectionController != null)
            {
                selectionController.SelectionChanged += SyncDropdownFromSelection;
                selectionController.DirtyChanged += RefreshCalBadge;
            }
        }

        private void RefreshPreviewGuidanceStatus()
        {
            if (previewGuidanceStatusText == null)
                return;
            ArTaskGuidanceController tg = guidanceSystem != null ? guidanceSystem.TaskGuidance : null;
            if (tg == null || !tg.IsPreviewMode)
            {
                previewGuidanceStatusText.text = "Preview Guidance: Off · SIMULATION ORIGIN";
                return;
            }

            ArTaskGuidanceStage stage = tg.Map != null
                ? tg.Map.GetStageByDisplayOrder(tg.PreviewStageIndex)
                : null;
            string key = stage != null ? stage.guidanceKey : "?";
            previewGuidanceStatusText.text =
                "Preview Guidance: " + (tg.PreviewStageIndex + 1) + "/" +
                (tg.Map != null ? tg.Map.StageCount : 0) + " · " + key +
                " · SIMULATION ORIGIN";
        }

        private void SetMode(ArGuidancePanelMode mode)
        {
            _mode = mode;
            if (previewView != null)
                previewView.SetActive(mode == ArGuidancePanelMode.Preview);
            if (calibrationView != null)
                calibrationView.SetActive(mode == ArGuidancePanelMode.Calibration);
            if (previewTabUnderline != null)
                previewTabUnderline.SetActive(mode == ArGuidancePanelMode.Preview);
            if (calibrationTabUnderline != null)
                calibrationTabUnderline.SetActive(mode == ArGuidancePanelMode.Calibration);

            if (mode == ArGuidancePanelMode.Preview)
            {
                guidanceSystem?.TaskGuidance?.SetCalibrationPause(false);
                nudgeController?.ForceRestoreIfMinimized();
                guidanceSystem?.Calibration?.HidePreview();
                RefreshPreviewGuidanceStatus();
            }
            else
            {
                // Task stage show/hide must not fight the single selected calibration hologram.
                guidanceSystem?.TaskGuidance?.SetCalibrationPause(true);
                guidanceSystem?.TaskGuidance?.ExitPreview();
                BindCalibrationControllers();
                guidanceSystem?.Calibration?.ShowSelected(true);
                if (selectionController != null && guidanceSystem?.Calibration != null)
                    selectionController.SetDirty(guidanceSystem.Calibration.IsDirtyVersusSaved());
                nudgeController?.RefreshAll();
                SyncDropdownFromSelection();
            }
        }

        private void RefreshHeader()
        {
            if (titleText != null)
                titleText.text = "Developer Mode";
            if (equipmentText != null)
                equipmentText.text = profile != null ? profile.displayName : "Dell Precision 3570";
            if (trackingStatusText != null)
                trackingStatusText.text = "Manual Test Origin · SIMULATION ORIGIN";
        }

        private void OnComponentDropdown(int index)
        {
            if (_ignoreDropdown)
                return;

            if (index < 0 || index >= calibrationDropdownIds.Count)
            {
                Debug.LogWarning("[AR Guidance][Calibration] Dropdown index " + index +
                                  " is out of range (" + calibrationDropdownIds.Count + " ids registered).");
                return;
            }

            string componentId = calibrationDropdownIds[index];
            string previousId = guidanceSystem != null && guidanceSystem.Calibration != null
                ? guidanceSystem.Calibration.SelectedComponentId
                : "(none)";
            string visibleLabel = componentDropdown != null && index < componentDropdown.options.Count
                ? componentDropdown.options[index].text
                : "?";

            Debug.Log("[AR Guidance][Calibration] Dropdown changed -> index=" + index +
                      " label=\"" + visibleLabel + "\" id=\"" + componentId +
                      "\" previous=\"" + previousId + "\"");

            if (selectionController == null)
            {
                SelectAndShowComponent(componentId);
                return;
            }

            // RequestIndex resolves the same index against the exact same list (bound in
            // RebuildCalibrationDropdown) and, if not blocked by an unsaved-changes gate, calls
            // SelectAndShowComponent internally via the onApply callback — one authoritative path.
            if (!selectionController.RequestIndex(index))
            {
                // Unsaved-changes gate blocked the switch — snap the dropdown back and
                // refresh so the Discard/Save banner becomes visible.
                SyncDropdownFromSelection();
                nudgeController?.RefreshAll();
            }
        }

        /// <summary>Fires after every ShowSelected attempt (from any path — dropdown, Prev/Next,
        /// Reset, Restore Saved). error is null on success; on failure this is the only place a
        /// "prefab could not be resolved" condition becomes visible to the user, not just the
        /// console.</summary>
        private void OnSelectionResolved(string componentId, string error)
        {
            if (error != null)
            {
                UIAudioManager.Play(UIAudioEvent.Error);
                if (trackingStatusText != null)
                    trackingStatusText.text = "Calibration visual unavailable for " + componentId;
                Debug.LogError("[AR Guidance][Calibration] Resolution failed for \"" + componentId + "\": " + error);
            }
            else
            {
                RefreshHeader();
            }
        }

        private void SyncDropdownFromSelection()
        {
            if (componentDropdown == null || selectionController == null)
                return;
            _ignoreDropdown = true;
            componentDropdown.value = selectionController.CurrentIndex;
            componentDropdown.RefreshShownValue();
            _ignoreDropdown = false;
            RefreshCalibratingName();
        }

        /// <summary>Keeps the "Currently Calibrating" card and Selected Component info card
        /// in sync with the same authoritative selection index used by the dropdown.</summary>
        private void RefreshCalibratingName()
        {
            if (selectionController == null || calibrationDropdownIds.Count == 0)
                return;
            int index = Mathf.Clamp(selectionController.CurrentIndex, 0, calibrationDropdownIds.Count - 1);
            string name = ArCalibrationIds.DisplayName(calibrationDropdownIds[index]);
            if (calibratingNameText != null)
                calibratingNameText.text = name;
            if (selectedComponentInfoText != null)
                selectedComponentInfoText.text = name;
        }

        private void WireToggle(Toggle toggle, string componentId, TMP_Text stateLabel)
        {
            if (toggle == null)
                return;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(on =>
            {
                UIAudioManager.Play(UIAudioEvent.ToggleChanged);
                guidanceSystem?.Renderer?.SetVisible(componentId, on);
                if (stateLabel != null)
                    stateLabel.text = on ? "Visible" : "Show";
            });
            if (stateLabel != null)
                stateLabel.text = toggle.isOn ? "Visible" : "Show";
        }

        private void SyncPreviewToggles(bool value)
        {
            SetToggle(toggleLaptopOutline, toggleLaptopOutlineLabel, value);
            SetToggle(toggleLiftEdge, toggleLiftEdgeLabel, value);
            SetToggle(toggleBatteryConnector, toggleBatteryConnectorLabel, value);
            SetToggle(toggleCoolingFan, toggleCoolingFanLabel, value);
            SetToggle(toggleFanAirflow, toggleFanAirflowLabel, value);
            if (h04ScrewToggles != null)
            {
                for (int i = 0; i < h04ScrewToggles.Length; i++)
                {
                    TMP_Text label = h04ScrewToggleLabels != null && i < h04ScrewToggleLabels.Length
                        ? h04ScrewToggleLabels[i]
                        : null;
                    SetToggle(h04ScrewToggles[i], label, value);
                }
            }
        }

        private static void SetToggle(Toggle toggle, TMP_Text stateLabel, bool value)
        {
            if (stateLabel != null)
                stateLabel.text = value ? "Visible" : "Show";
            if (toggle != null)
                toggle.SetIsOnWithoutNotify(value);
        }

        private static void Wire(Button button, System.Action action)
        {
            if (button == null || action == null)
                return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                UIAudioManager.Play(UIAudioEvent.ButtonClick);
                action();
            });
        }
    }
}
