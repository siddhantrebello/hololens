using System.Collections.Generic;
using UnityEngine;

namespace Norscope.TaskLibrary
{
    [CreateAssetMenu(fileName = "TaskLibraryDatabase", menuName = "Norscope/Task Library/Task Library Database", order = 31)]
    public sealed class TaskLibraryDatabase : ScriptableObject
    {
        [SerializeField] private List<TaskDefinition> tasks = new List<TaskDefinition>();

        public IReadOnlyList<TaskDefinition> Tasks => tasks;
    }
}
