using UnityEngine;

namespace Norscope.TaskLibrary
{
    /// <summary>
    /// View glue for the Task Library panel. Card appearance is entirely owned by
    /// TaskCard.prefab, edited by hand in Prefab Mode — this class only ever instantiates
    /// copies of it and binds data; it never creates, resizes, or positions anything itself.
    /// Task content comes entirely from the assigned TaskLibraryDatabase asset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskLibraryPanel : MonoBehaviour
    {
        [SerializeField] private TaskLibraryDatabase taskDatabase;
        [SerializeField] private Transform cardContent;
        [SerializeField] private TaskCardView cardPrefab;
        private bool _populated;

        /// <summary>Populates on every activation (not just the first) — SpatialWindowManager
        /// reactivates an existing window instance on reopen rather than re-instantiating it,
        /// and Awake only ever fires once per GameObject lifetime, so OnEnable is what makes
        /// reopening the panel reflect any TaskLibraryDatabase edits made while it was closed.
        /// Still just "instantiate + bind data" — never touches a RectTransform or LayoutGroup.</summary>
        private void OnEnable()
        {
            if (!_populated)
            {
                _populated = PopulateCards();
            }
        }

        private bool PopulateCards()
        {
            if (cardContent == null || cardPrefab == null)
            {
                // Not wired yet — an Editor builder can activate a freshly AddComponent'd
                // instance before it finishes assigning serialized fields. Nothing to do yet.
                return false;
            }

            for (int i = cardContent.childCount - 1; i >= 0; i--)
            {
                Destroy(cardContent.GetChild(i).gameObject);
            }

            var repository = new ScriptableObjectTaskRepository(taskDatabase);
            foreach (TaskData data in repository.GetAll())
            {
                TaskCardView card = Instantiate(cardPrefab, cardContent);
                card.Configure(data);
            }

            return true;
        }
    }
}
