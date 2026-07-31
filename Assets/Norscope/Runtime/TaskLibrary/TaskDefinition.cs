using System.Collections.Generic;
using UnityEngine;
using Norscope.Localization;

namespace Norscope.TaskLibrary
{
    [CreateAssetMenu(fileName = "TaskDefinition", menuName = "Norscope/Task Library/Task Definition", order = 30)]
    public sealed class TaskDefinition : ScriptableObject
    {
        [SerializeField] private string taskId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField, TextArea(2, 4)] private string description = string.Empty;
        [SerializeField] private Sprite icon;
        [SerializeField] private string estimatedDuration = string.Empty;
        [SerializeField] private TaskDifficulty difficulty = TaskDifficulty.Beginner;
        [SerializeField] private string category = "Maintenance";
        [SerializeField] private bool offlineAvailable;
        [SerializeField] private string lastUpdated = string.Empty;
        [SerializeField] private bool isEnabled = true;
        [SerializeField] private List<TaskStep> steps = new List<TaskStep>();
        [SerializeField] private List<LocalizedTaskContent> localizedContent =
            new List<LocalizedTaskContent>();

        public string TaskId => taskId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public string EstimatedDuration => estimatedDuration;
        public TaskDifficulty Difficulty => difficulty;
        public string Category => category;
        public bool OfflineAvailable => offlineAvailable;
        public string LastUpdated => lastUpdated;
        public bool IsEnabled => isEnabled;
        public IReadOnlyList<TaskStep> Steps => steps;
        public IReadOnlyList<LocalizedTaskContent> LocalizedContent => localizedContent;
    }
}
