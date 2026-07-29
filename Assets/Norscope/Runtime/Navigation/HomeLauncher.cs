using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Norscope;
using Norscope.Audio;

namespace Norscope.Navigation
{
    /// <summary>
    /// Permanent Home Launcher control. Pinch/press opens Home via PanelRegistry.
    /// Never closed, never duplicated, never grabbed/resized.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeLauncher : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private PanelRegistry registry;
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image outline;
        [SerializeField] private Image icon;
        [SerializeField] private Color normalOutline = new Color(0.149f, 0.188f, 0.255f, 0.85f);
        [SerializeField] private Color hoverOutline = new Color(0.133f, 0.827f, 0.933f, 0.95f);
        [SerializeField] private float hoverScale = 1.04f;

        private Vector3 _baseScale;
        private bool _hovered;

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (registry == null)
            {
                registry = FindFirstObjectByType<PanelRegistry>();
            }

            if (button == null)
            {
                button = GetComponentInChildren<Button>(true);
            }

            if (button != null)
            {
                button.onClick.RemoveListener(OnPressed);
                button.onClick.AddListener(OnPressed);
            }

            ApplyOutline(false);

            if (GetComponent<UiButtonPressScale>() == null)
            {
                gameObject.AddComponent<UiButtonPressScale>();
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnPressed);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            ApplyOutline(true);
            transform.localScale = _baseScale * hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            ApplyOutline(false);
            transform.localScale = _baseScale;
        }

        private void OnPressed()
        {
            UIAudioManager.Play(UIAudioEvent.ButtonClick);

            PanelRegistry target = registry != null ? registry : PanelRegistry.Instance;
            if (target == null)
            {
                Debug.LogError("[NAV] HomeLauncher: PanelRegistry missing.");
                return;
            }

            target.Open(PanelType.Home);
        }

        private void ApplyOutline(bool hover)
        {
            if (outline != null)
            {
                outline.color = hover ? hoverOutline : normalOutline;
                outline.enabled = true;
            }
        }
    }
}
