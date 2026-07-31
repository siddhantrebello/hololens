using System.Collections.Generic;
using UnityEngine;
using Norscope.Localization;
using Norscope.Settings;

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

                string name = def.DisplayName;
                string description = def.Description;
                var steps = new List<TaskStep>(def.Steps);
                if (LocalizationService.Instance != null &&
                    LocalizationService.Instance.CurrentLanguage == NorscopeLanguage.German)
                {
                    LocalizedTaskContent localized = null;
                    foreach (LocalizedTaskContent candidate in def.LocalizedContent)
                        if (candidate != null &&
                            string.Equals(candidate.locale, "de",
                                System.StringComparison.OrdinalIgnoreCase))
                        { localized = candidate; break; }
                    if (localized != null)
                    {
                        if (!string.IsNullOrWhiteSpace(localized.displayName))
                            name = localized.displayName;
                        if (!string.IsNullOrWhiteSpace(localized.description))
                            description = localized.description;
                        for (int i = 0; i < steps.Count && i < localized.steps.Count; i++)
                        {
                            LocalizedTaskStepContent source = localized.steps[i];
                            if (source == null) continue;
                            TaskStep fallback = steps[i];
                            steps[i] = new TaskStep
                            {
                                stepTitle = string.IsNullOrWhiteSpace(source.stepTitle) ? fallback.stepTitle : source.stepTitle,
                                instruction = string.IsNullOrWhiteSpace(source.instruction) ? fallback.instruction : source.instruction,
                                warning = string.IsNullOrWhiteSpace(source.warning) ? fallback.warning : source.warning,
                                verification = string.IsNullOrWhiteSpace(source.verification) ? fallback.verification : source.verification,
                                image = fallback.image
                            };
                        }
                    }
                }

                result.Add(new TaskData
                {
                    sourceDefinition = def,
                    id = def.TaskId,
                    taskName = name,
                    description = description,
                    icon = def.Icon,
                    estimatedDuration = def.EstimatedDuration,
                    difficulty = def.Difficulty,
                    category = def.Category,
                    offlineAvailable = def.OfflineAvailable,
                    lastUpdated = def.LastUpdated,
                    steps = steps
                });
            }

            return result;
        }
    }
}
