using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Norscope.Audio;
using Norscope.Localization;

namespace Norscope.TaskLibrary
{
    /// <summary>
    /// Task Library behavior for the authored Figma composition. Runtime work is
    /// limited to instantiating TaskCard prefabs, binding data, filtering existing
    /// cards, and wiring authored controls. It never creates or edits visual layout.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskLibraryPanel : MonoBehaviour
    {
        [Serializable]
        private sealed class CategoryFilter
        {
            public string category = string.Empty;
            public Button button;
        }

        [Header("Data and Card Prefab")]
        [SerializeField] private TaskLibraryDatabase taskDatabase;
        [SerializeField] private Transform cardContent;
        [SerializeField] private TaskCardView cardPrefab;

        [Header("Authored Controls")]
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private CategoryFilter[] categoryFilters;
        [SerializeField] private TaskLibraryDetailView detailView;
        [SerializeField] private ScrollRect taskScrollRect;
        [SerializeField] private TMP_Text emptyStateText;

        private readonly List<TaskCardView> _cards =
            new List<TaskCardView>();
        private readonly Dictionary<Button, UnityAction> _filterActions =
            new Dictionary<Button, UnityAction>();
        private bool _populated;
        private bool _listenersWired;
        private string _activeCategory = string.Empty;
        private TaskCardView _selectedCard;

        private void OnEnable()
        {
            LocalizationService.LanguageChanged -= OnLanguageChanged;
            LocalizationService.LanguageChanged += OnLanguageChanged;
            WireListeners();
            if (!_populated)
                _populated = PopulateCards();
            ApplyFilter();
        }

        private void OnDestroy()
        {
            LocalizationService.LanguageChanged -= OnLanguageChanged;
            UnwireListeners();
        }

        private void OnLanguageChanged()
        {
            float scroll = taskScrollRect != null
                ? taskScrollRect.verticalNormalizedPosition : 1f;
            _populated = PopulateCards();
            ApplyFilter();
            if (taskScrollRect != null)
                taskScrollRect.verticalNormalizedPosition = scroll;
        }

        private void WireListeners()
        {
            if (_listenersWired) return;

            if (searchInput != null)
            {
                searchInput.onValueChanged.AddListener(OnSearchChanged);
                searchInput.onSelect.AddListener(OnSearchFocused);
            }

            if (categoryFilters != null)
            {
                foreach (CategoryFilter filter in categoryFilters)
                {
                    if (filter == null || filter.button == null) continue;
                    string category = filter.category;
                    UnityAction action = () => SetCategory(category);
                    filter.button.onClick.AddListener(action);
                    _filterActions[filter.button] = action;
                }
            }

            if (detailView != null)
            {
                if (detailView.StartButton != null)
                    detailView.StartButton.onClick.AddListener(OpenSelectedTask);
                if (detailView.DetailsButton != null)
                    detailView.DetailsButton.onClick.AddListener(LogDetails);
            }

            _listenersWired = true;
        }

        private void UnwireListeners()
        {
            if (!_listenersWired) return;

            if (searchInput != null)
            {
                searchInput.onValueChanged.RemoveListener(OnSearchChanged);
                searchInput.onSelect.RemoveListener(OnSearchFocused);
            }

            foreach (KeyValuePair<Button, UnityAction> pair in _filterActions)
            {
                if (pair.Key != null)
                    pair.Key.onClick.RemoveListener(pair.Value);
            }
            _filterActions.Clear();

            if (detailView != null)
            {
                if (detailView.StartButton != null)
                    detailView.StartButton.onClick.RemoveListener(
                        OpenSelectedTask);
                if (detailView.DetailsButton != null)
                    detailView.DetailsButton.onClick.RemoveListener(LogDetails);
            }

            _listenersWired = false;
        }

        private bool PopulateCards()
        {
            if (cardContent == null || cardPrefab == null)
                return false;

            for (int i = cardContent.childCount - 1; i >= 0; i--)
                Destroy(cardContent.GetChild(i).gameObject);
            _cards.Clear();
            _selectedCard = null;

            var repository = new ScriptableObjectTaskRepository(taskDatabase);
            foreach (TaskData data in repository.GetAll())
            {
                TaskCardView card = Instantiate(cardPrefab, cardContent);
                card.Configure(data);
                if (detailView != null)
                    card.SetSelectionHandler(SelectCard);
                _cards.Add(card);
            }

            if (detailView != null && _cards.Count > 0)
                SelectCard(_cards[0]);
            return true;
        }

        private void SelectCard(TaskCardView selected)
        {
            if (selected == null) return;

            _selectedCard = selected;
            foreach (TaskCardView card in _cards)
                card.SetSelected(card == selected);
            if (detailView != null)
                detailView.Bind(selected.Data);
        }

        private void OpenSelectedTask()
        {
            if (_selectedCard == null) return;
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            _selectedCard.OpenTask();
        }

        private void LogDetails()
        {
            if (_selectedCard == null || _selectedCard.Data == null) return;
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            Debug.Log("Task details: " + _selectedCard.Data.taskName);
        }

        private void OnSearchFocused(string value)
        {
            UIAudioManager.Play(UIAudioEvent.SearchFocused);
        }

        private void OnSearchChanged(string value)
        {
            UIAudioManager.Play(string.IsNullOrWhiteSpace(value)
                ? UIAudioEvent.SearchCleared
                : UIAudioEvent.SearchResult);
            ApplyFilter();
        }

        private void SetCategory(string category)
        {
            _activeCategory = string.Equals(
                category, "All", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : category ?? string.Empty;
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string query = searchInput != null
                ? searchInput.text.Trim()
                : string.Empty;
            int visibleCount = 0;
            TaskCardView firstVisible = null;

            foreach (TaskCardView card in _cards)
            {
                TaskData data = card != null ? card.Data : null;
                bool categoryMatch = string.IsNullOrEmpty(_activeCategory) ||
                    (data != null && string.Equals(
                        data.category, _activeCategory,
                        StringComparison.OrdinalIgnoreCase));
                bool queryMatch = string.IsNullOrEmpty(query) ||
                    (data != null &&
                     ((!string.IsNullOrEmpty(data.taskName) &&
                       data.taskName.IndexOf(
                           query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                      (!string.IsNullOrEmpty(data.description) &&
                       data.description.IndexOf(
                           query, StringComparison.OrdinalIgnoreCase) >= 0)));
                bool visible = categoryMatch && queryMatch;
                if (card != null) card.gameObject.SetActive(visible);
                if (!visible) continue;
                visibleCount++;
                if (firstVisible == null) firstVisible = card;
            }

            if (emptyStateText != null)
                emptyStateText.gameObject.SetActive(visibleCount == 0);

            if (_selectedCard == null ||
                !_selectedCard.gameObject.activeSelf)
            {
                if (firstVisible != null)
                    SelectCard(firstVisible);
            }

            if (taskScrollRect != null)
                taskScrollRect.verticalNormalizedPosition = 1f;
        }
    }
}
