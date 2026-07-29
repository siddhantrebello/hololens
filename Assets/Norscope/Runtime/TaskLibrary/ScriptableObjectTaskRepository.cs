using System.Collections.Generic;
using UnityEngine;

namespace Norscope.TaskLibrary
{
    public sealed class ScriptableObjectTaskRepository : ITaskRepository
    {
        private readonly TaskLibraryDatabase _database;
        private bool _loggedMissingDatabase;

        public ScriptableObjectTaskRepository(TaskLibraryDatabase database)
        {
            _database = database;
        }

        public IReadOnlyList<TaskData> GetAll()
        {
            if (_database == null)
            {
                if (!_loggedMissingDatabase)
                {
                    Debug.LogWarning("[TaskLibrary] No TaskLibraryDatabase assigned — returning empty task list.");
                    _loggedMissingDatabase = true;
                }

                return System.Array.Empty<TaskData>();
            }

            var result = new List<TaskData>();
            foreach (TaskDefinition def in _database.Tasks)
            {
                if (def == null || !def.IsEnabled)
                {
                    continue;
                }

                result.Add(new TaskData
                {
                    sourceDefinition = def,
                    id = def.TaskId,
                    taskName = def.DisplayName,
                    description = def.Description,
                    icon = def.Icon,
                    estimatedDuration = def.EstimatedDuration,
                    difficulty = def.Difficulty,
                    steps = new List<TaskStep>(def.Steps)
                });
            }

            return result;
        }
    }
}
