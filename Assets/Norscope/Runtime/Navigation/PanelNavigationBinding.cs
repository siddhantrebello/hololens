using Norscope.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.Navigation
{
    /// <summary>
    /// Per-panel navigation metadata. Runtime code only wires the authored Back button.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PanelNavigationBinding : MonoBehaviour
    {
        [SerializeField] private PanelType panelType = PanelType.None;
        [SerializeField] private Button backButton;
        [SerializeField] private PanelType fallbackParent = PanelType.None;

        public PanelType PanelType => panelType;
        public PanelType FallbackParent => fallbackParent;
        public Button BackButton => backButton;

        public void Configure(
            PanelType currentPanel,
            PanelType optionalFallbackParent,
            Button authoredBackButton)
        {
            panelType = currentPanel;
            fallbackParent = optionalFallbackParent;
            backButton = authoredBackButton;
            WireButton();
        }

        private void Awake()
        {
            WireButton();
        }

        private void OnDestroy()
        {
            if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
        }

        private void WireButton()
        {
            if (backButton == null) return;
            backButton.onClick.RemoveListener(OnBackClicked);
            backButton.onClick.AddListener(OnBackClicked);
        }

        private void OnBackClicked()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);
            PanelNavigationService service = PanelNavigationService.Instance;
            if (service == null)
            {
                Debug.LogWarning("[NAV] Back ignored: PanelNavigationService is unavailable.");
                return;
            }

            service.Back(panelType, fallbackParent);
        }
    }
}
