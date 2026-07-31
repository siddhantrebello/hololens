using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Norscope.Audio;
using Norscope.UI;
using Norscope.Localization;

namespace Norscope.TaskLibrary
{
    /// <summary>
    /// Binds one authored TaskCard prefab to task data and observes pointer state.
    /// It never creates UI, changes RectTransforms, or modifies LayoutGroups.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskCardView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [Header("Content Targets")]
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text durationText;
        [SerializeField] private TMP_Text stepCountText;
        [SerializeField] private Image difficultyBadgeBackground;
        [SerializeField] private TMP_Text difficultyBadgeText;
        [SerializeField] private Image selectedAccent;
        [SerializeField] private Image chevron;
        [SerializeField] private Button cardButton;

        [Header("Authored Visual States")]
        [SerializeField] private Image surface;
        [SerializeField] private Image border;
        [SerializeField] private Color normalSurfaceColor =
            new Color(0.094f, 0.129f, 0.169f, 1f);
        [SerializeField] private Color hoverSurfaceColor =
            new Color(0.11f, 0.153f, 0.212f, 1f);
        [SerializeField] private Color selectedSurfaceColor =
            new Color(0.098f, 0.141f, 0.22f, 1f);
        [SerializeField] private Color pressedSurfaceColor =
            new Color(0.082f, 0.118f, 0.157f, 1f);
        [SerializeField] private Color normalBorderColor =
            new Color(0.176f, 0.231f, 0.286f, 1f);
        [SerializeField] private Color hoverBorderColor =
            new Color(0.118f, 0.784f, 0.961f, 0.30f);
        [SerializeField] private Color selectedBorderColor =
            new Color(0.118f, 0.784f, 0.961f, 1f);
        [SerializeField] private Color pressedBorderColor =
            new Color(0.118f, 0.784f, 0.961f, 0.22f);
        [SerializeField] private Color normalChevronColor =
            new Color(0.29f, 0.365f, 0.431f, 1f);
        [SerializeField] private Color selectedChevronColor =
            new Color(0.118f, 0.784f, 0.961f, 1f);

        [Header("Difficulty Accent Source")]
        [SerializeField] private NorscopeTheme theme;

        [Header("Fallback Icon")]
        [SerializeField] private Sprite fallbackIcon;

        private bool _isHovering;
        private bool _isPressed;
        private bool _isSelected;
        private Action<TaskCardView> _selectionRequested;

        public static event Action<TaskDefinition> TaskOpened;
        public TaskData Data { get; private set; }

        private void Awake()
        {
            if (cardButton != null)
            {
                cardButton.onClick.AddListener(OnCardClicked);
            }
        }

        private void OnDestroy()
        {
            if (cardButton != null)
            {
                cardButton.onClick.RemoveListener(OnCardClicked);
            }
        }

        public void SetSelectionHandler(Action<TaskCardView> handler)
        {
            _selectionRequested = handler;
        }

        public void Configure(TaskData data)
        {
            Data = data;

            if (icon != null)
            {
                Sprite iconSprite = data.icon != null ? data.icon : fallbackIcon;
                icon.sprite = iconSprite;
                icon.enabled = iconSprite != null;
            }

            if (titleText != null) titleText.text = data.taskName;
            if (descriptionText != null) descriptionText.text = data.description;
            if (categoryText != null) categoryText.text = data.category;
            if (durationText != null) durationText.text = data.estimatedDuration;
            if (stepCountText != null)
            {
                int count = data.steps != null ? data.steps.Count : 0;
                stepCountText.text = LocalizationService.Get(
                    count == 1 ? "task.steps.one" : "task.steps.many", count);
            }

            ApplyDifficulty(data.difficulty);
            ApplyVisualState();
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            ApplyVisualState();
        }

        public void OpenTask()
        {
            if (Data == null) return;

            UIAudioManager.Play(UIAudioEvent.TaskOpened);
            Debug.Log("Open task: " + Data.taskName);
            if (Data.sourceDefinition == null)
            {
                Debug.LogError(
                    "[TaskLibrary] Selected task has no source TaskDefinition.");
                return;
            }

            TaskOpened?.Invoke(Data.sourceDefinition);
        }

        private void OnCardClicked()
        {
            if (Data == null) return;

            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            if (_selectionRequested != null)
            {
                _selectionRequested(this);
            }
            else
            {
                // Backward-compatible behavior for any card used independently.
                OpenTask();
            }
        }

        private void ApplyDifficulty(TaskDifficulty difficulty)
        {
            if (difficultyBadgeText != null)
                difficultyBadgeText.text = DifficultyLabel(difficulty);

            Color accent = Color.gray;
            if (theme != null)
            {
                switch (difficulty)
                {
                    case TaskDifficulty.Beginner:
                        accent = theme.success;
                        break;
                    case TaskDifficulty.Intermediate:
                        accent = theme.warning;
                        break;
                    case TaskDifficulty.Advanced:
                        accent = theme.error;
                        break;
                    default:
                        accent = theme.secondaryAccent;
                        break;
                }
            }

            if (difficultyBadgeBackground != null)
            {
                difficultyBadgeBackground.color =
                    new Color(accent.r, accent.g, accent.b, 0.10f);
            }
            if (difficultyBadgeText != null)
                difficultyBadgeText.color = accent;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
            ApplyVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            _isPressed = false;
            ApplyVisualState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            ApplyVisualState();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            Color surfaceColor = _isPressed
                ? pressedSurfaceColor
                : (_isSelected
                    ? selectedSurfaceColor
                    : (_isHovering
                        ? hoverSurfaceColor
                        : normalSurfaceColor));
            Color borderColor = _isPressed
                ? pressedBorderColor
                : (_isSelected
                    ? selectedBorderColor
                    : (_isHovering
                        ? hoverBorderColor
                        : normalBorderColor));

            if (surface != null) surface.color = surfaceColor;
            if (border != null) border.color = borderColor;
            if (selectedAccent != null)
                selectedAccent.gameObject.SetActive(_isSelected);
            if (chevron != null)
            {
                chevron.color = _isSelected
                    ? selectedChevronColor
                    : normalChevronColor;
            }
        }

        private static string DifficultyLabel(TaskDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TaskDifficulty.Beginner: return LocalizationService.Get("difficulty.beginner");
                case TaskDifficulty.Intermediate: return LocalizationService.Get("difficulty.intermediate");
                case TaskDifficulty.Advanced: return LocalizationService.Get("difficulty.advanced");
                default: return difficulty.ToString();
            }
        }
    }
}
