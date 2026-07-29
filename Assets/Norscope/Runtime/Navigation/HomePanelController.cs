using UnityEngine;

namespace Norscope.Navigation
{
    /// <summary>
    /// Marker / wiring helper for HomePanel. Cards are authored in the prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomePanelController : MonoBehaviour
    {
        [SerializeField] private HomePanelCard[] cards;

        public HomePanelCard[] Cards => cards;

        public void SetCards(HomePanelCard[] panelCards)
        {
            cards = panelCards;
        }
    }
}
