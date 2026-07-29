using System.Collections.Generic;
using UnityEngine;

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
        [SerializeField] private bool isEnabled = true;
        [SerializeField] private List<TaskStep> steps = new List<TaskStep>();

        public string TaskId => taskId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public string EstimatedDuration => estimatedDuration;
        public TaskDifficulty Difficulty => difficulty;
        public bool IsEnabled => isEnabled;
        public IReadOnlyList<TaskStep> Steps => steps;
    }
}
