using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Norscope.Audio;
using Norscope.UI;

namespace Norscope.TaskLibrary
{
    /// <summary>
    /// Data-binding and interaction wiring for one task card. Every visual property — position,
    /// size, spacing, color, font — lives on this prefab's own components, authored by hand in
    /// Prefab Mode. This script never creates, resizes, or repositions anything; Configure()
    /// only ever writes text/sprite content and a difficulty-driven accent color. The whole card
    /// is the clickable element — there is no separate Open button.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Content Targets — wire to this card's own children")]
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text durationText;
        [SerializeField] private Image difficultyBadgeBackground;
        [SerializeField] private TMP_Text difficultyBadgeText;
        [SerializeField] private Button cardButton;

        [Header("Border — hover/press feedback")]
        [SerializeField] private Image border;
        [SerializeField] private Color normalBorderColor = new Color(0.149f, 0.188f, 0.255f, 1f);
        [SerializeField] private Color hoverBorderColor = new Color(0.133f, 0.827f, 0.933f, 1f);
        [SerializeField] private Color pressedBorderColor = new Color(0.098f, 0.6f, 0.686f, 1f);

        [Header("Difficulty Accent Source")]
        [SerializeField] private NorscopeTheme theme;

        [Header("Fallback Icon (used when a task has none)")]
        [SerializeField] private Sprite fallbackIcon;

        private bool _isHovering;

        /// <summary>Placeholder seam for a future Task Execution system — no subscribers yet.</summary>
        public static event Action<TaskDefinition> TaskOpened;

        public TaskData Data { get; private set; }

        private void Awake()
        {
            if (cardButton != null)
            {
                cardButton.onClick.AddListener(OnCardClicked);
            }
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

            if (titleText != null)
            {
                titleText.text = data.taskName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = data.description;
            }

            if (durationText != null)
            {
                durationText.text = data.estimatedDuration;
            }

            ApplyDifficulty(data.difficulty);

            if (border != null)
            {
                border.color = normalBorderColor;
            }
        }

        private void ApplyDifficulty(TaskDifficulty difficulty)
        {
            if (difficultyBadgeText != null)
            {
                difficultyBadgeText.text = DifficultyLabel(difficulty);
            }

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
                difficultyBadgeBackground.color = new Color(accent.r, accent.g, accent.b, 0.18f);
            }

            if (difficultyBadgeText != null)
            {
                difficultyBadgeText.color = accent;
            }
        }

        private void OnCardClicked()
        {
            if (Data == null)
            {
                return;
            }

            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            UIAudioManager.Play(UIAudioEvent.TaskOpened);
            Debug.Log("Open task: " + Data.taskName);
            if (Data.sourceDefinition == null)
            {
                Debug.LogError("[TaskLibrary] Selected task has no source TaskDefinition.");
                return;
            }

            TaskOpened?.Invoke(Data.sourceDefinition);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
            UIAudioManager.Play(UIAudioEvent.TaskSelected);
            ApplyBorderColor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            ApplyBorderColor();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (border != null)
            {
                border.color = pressedBorderColor;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ApplyBorderColor();
        }

        private void ApplyBorderColor()
        {
            if (border == null)
            {
                return;
            }

            border.color = _isHovering ? hoverBorderColor : normalBorderColor;
        }

        private static string DifficultyLabel(TaskDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TaskDifficulty.Beginner: return "Beginner";
                case TaskDifficulty.Intermediate: return "Intermediate";
                case TaskDifficulty.Advanced: return "Advanced";
                default: return difficulty.ToString();
            }
        }
    }
}
