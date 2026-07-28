using UnityEngine;
using UnityEngine.EventSystems;

namespace Norscope
{
    /// <summary>
    /// Small press-scale feedback for UI buttons.
    /// </summary>
    public sealed class UiButtonPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField]
        private float pressedScale = 0.96f;

        private Vector3 _baseScale;
        private bool _pressed;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            transform.localScale = _baseScale * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            transform.localScale = _baseScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_pressed)
            {
                _pressed = false;
                transform.localScale = _baseScale;
            }
        }
    }
}
