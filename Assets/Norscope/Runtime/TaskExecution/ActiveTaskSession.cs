using System;
using Norscope.TaskLibrary;
using UnityEngine;

namespace Norscope.TaskExecution
{
    /// <summary>Stores task execution state only; never touches UI or layout.</summary>
    [DisallowMultipleComponent]
    public sealed class ActiveTaskSession : MonoBehaviour
    {
        public TaskDefinition SelectedTask { get; private set; }
        public int CurrentStepIndex { get; private set; }
        public bool IsComplete { get; private set; }

        public event Action TaskBegan;
        public event Action StepChanged;
        public event Action TaskFinished;
        public event Action TaskCancelled;

        public int StepCount => SelectedTask != null && SelectedTask.Steps != null
            ? SelectedTask.Steps.Count
            : 0;

        public TaskStep CurrentStep => CurrentStepIndex >= 0 && CurrentStepIndex < StepCount
            ? SelectedTask.Steps[CurrentStepIndex]
            : null;

        public string CurrentGuidanceKey =>
            CurrentStep != null ? CurrentStep.guidanceKey ?? string.Empty : string.Empty;

        public bool CanGoPrevious => !IsComplete && CurrentStepIndex > 0;
        public bool CanGoNext => !IsComplete && CurrentStepIndex < StepCount - 1;
        public bool IsOnLastStep => !IsComplete && StepCount > 0 && CurrentStepIndex == StepCount - 1;

        public bool Begin(TaskDefinition task)
        {
            if (task == null)
            {
                Debug.LogError("[ActiveTask] Cannot begin a null task.");
                return false;
            }

            if (task.Steps == null || task.Steps.Count == 0)
            {
                Debug.LogError("[ActiveTask] Task has no steps: " + task.DisplayName);
                return false;
            }

            SelectedTask = task;
            CurrentStepIndex = 0;
            IsComplete = false;
            TaskBegan?.Invoke();
            StepChanged?.Invoke();
            return true;
        }

        public bool Previous()
        {
            if (!CanGoPrevious) return false;
            CurrentStepIndex--;
            StepChanged?.Invoke();
            return true;
        }

        public bool Next()
        {
            if (!CanGoNext) return false;
            CurrentStepIndex++;
            StepChanged?.Invoke();
            return true;
        }

        public bool Finish()
        {
            if (!IsOnLastStep) return false;
            IsComplete = true;
            TaskFinished?.Invoke();
            return true;
        }

        public bool Cancel()
        {
            if (SelectedTask == null) return false;
            SelectedTask = null;
            CurrentStepIndex = 0;
            IsComplete = false;
            TaskCancelled?.Invoke();
            return true;
        }
    }
}
