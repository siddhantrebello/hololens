using System;
using System.Collections.Generic;
using UnityEngine;

namespace Norscope.TaskLibrary
{
    /// <summary>Plain task record the view layer binds to. Every field except <see cref="icon"/>
    /// and the steps' <see cref="TaskStep.image"/> is JsonUtility-compatible, so a future JSON-
    /// backed ITaskRepository could deserialize directly into this type without redesign.</summary>
    [Serializable]
    public sealed class TaskData
    {
        public TaskDefinition sourceDefinition;
        public string id = string.Empty;
        public string taskName = string.Empty;
        public string description = string.Empty;
        public string estimatedDuration = string.Empty;
        public TaskDifficulty difficulty = TaskDifficulty.Beginner;
        public string category = string.Empty;
        public bool offlineAvailable;
        public string lastUpdated = string.Empty;
        public Sprite icon;
        public List<TaskStep> steps = new List<TaskStep>();
    }
}
