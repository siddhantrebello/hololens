using Norscope.Navigation;
using Norscope.TaskLibrary;
using Norscope.Windows;
using UnityEngine;

namespace Norscope.TaskExecution
{
    /// <summary>Bridges Task Library selection into the existing spatial-window system.</summary>
    [DisallowMultipleComponent]
    public sealed class ActiveTaskLauncher : MonoBehaviour
    {
        [SerializeField] private ActiveTaskSession session;
        [SerializeField] private PanelRegistry panelRegistry;
        [SerializeField] private SpatialWindowManager windowManager;

        public void SetReferences(
            ActiveTaskSession activeSession,
            PanelRegistry registry,
            SpatialWindowManager manager)
        {
            session = activeSession;
            panelRegistry = registry;
            windowManager = manager;
        }

        private void OnEnable()
        {
            TaskCardView.TaskOpened -= OnTaskSelected;
            TaskCardView.TaskOpened += OnTaskSelected;
        }

        private void OnDisable()
        {
            TaskCardView.TaskOpened -= OnTaskSelected;
        }

        private void OnTaskSelected(TaskDefinition task)
        {
            if (session == null || panelRegistry == null || windowManager == null)
            {
                Debug.LogError("[ActiveTask] Launcher references are not wired.");
                return;
            }

            if (!session.Begin(task)) return;

            PanelNavigationService navigation = PanelNavigationService.Instance;
            if (navigation == null)
            {
                Debug.LogError("[ActiveTask] PanelNavigationService is unavailable.");
                return;
            }

            SpatialWindow window = navigation.Navigate(
                PanelType.TaskLibrary,
                PanelType.TaskExecution);
            if (window == null)
            {
                Debug.LogError("[ActiveTask] Active Task panel could not be opened.");
                return;
            }

            ActiveTaskPanel panel = window.GetComponentInChildren<ActiveTaskPanel>(true);
            if (panel == null)
            {
                Debug.LogError("[ActiveTask] ActiveTaskPanel component missing from prefab.");
                return;
            }

            panel.Bind(session, panelRegistry, windowManager);
        }
    }
}
