using System.Collections;
using UnityEngine;

namespace Norscope.Settings.Motion
{
    /// <summary>
    /// Animates CanvasGroup alpha and optional anchored position. Presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsPageTransition : MonoBehaviour
    {
        public enum Mode
        {
            DetailFadeUp,
            HorizontalSlide
        }

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private float detailDuration = 0.22f;
        [SerializeField] private float shellDuration = 0.36f;
        [SerializeField] private float detailOffsetY = 10f;
        [SerializeField] private float shellOffsetX = 56f;

        private Coroutine _routine;
        private Vector2 _restAnchored;
        private bool _restCaptured;
        private int _token;

        public CanvasGroup CanvasGroup => canvasGroup;
        public bool IsAnimating => _routine != null;

        public void Configure(CanvasGroup group, RectTransform rect)
        {
            canvasGroup = group;
            rectTransform = rect;
            CaptureRest();
        }

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (rectTransform == null)
                rectTransform = transform as RectTransform;
            CaptureRest();
        }

        private void OnDisable()
        {
            StopActive();
        }

        private void OnDestroy()
        {
            StopActive();
        }

        public void SnapVisible(bool visible, bool raycastWhenVisible = true)
        {
            StopActive();
            EnsureRest();
            if (rectTransform != null)
                rectTransform.anchoredPosition = _restAnchored;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible && raycastWhenVisible;
            }

            if (visible)
                gameObject.SetActive(true);
        }

        public void SnapHidden()
        {
            SnapVisible(false);
            gameObject.SetActive(false);
        }

        public int PlayEnter(Mode mode, int direction, bool motionEnabled, System.Action onComplete = null)
        {
            gameObject.SetActive(true);
            int token = ++_token;
            StopActive(false);
            _routine = StartCoroutine(EnterRoutine(mode, direction, motionEnabled, token, onComplete));
            return token;
        }

        public int PlayExit(Mode mode, int direction, bool motionEnabled, System.Action onComplete = null)
        {
            int token = ++_token;
            StopActive(false);
            _routine = StartCoroutine(ExitRoutine(mode, direction, motionEnabled, token, onComplete));
            return token;
        }

        private IEnumerator EnterRoutine(
            Mode mode, int direction, bool motionEnabled, int token, System.Action onComplete)
        {
            EnsureRest();
            float duration = mode == Mode.DetailFadeUp ? detailDuration : shellDuration;
            Vector2 from = _restAnchored;
            if (mode == Mode.DetailFadeUp)
                from.y = _restAnchored.y - detailOffsetY;
            else
                from.x = _restAnchored.x + direction * shellOffsetX;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (rectTransform != null)
                rectTransform.anchoredPosition = from;

            if (!motionEnabled || duration <= 0f)
            {
                FinishEnter(token, onComplete);
                yield break;
            }

            float elapsed = 0f;
            AnimationCurve curve = SettingsMotionEasing.FigmaEaseOut;
            while (elapsed < duration)
            {
                if (token != _token) yield break;
                float t = SettingsMotionEasing.Evaluate(curve, elapsed / duration);
                if (canvasGroup != null)
                    canvasGroup.alpha = t;
                if (rectTransform != null)
                    rectTransform.anchoredPosition = Vector2.LerpUnclamped(from, _restAnchored, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            FinishEnter(token, onComplete);
        }

        private IEnumerator ExitRoutine(
            Mode mode, int direction, bool motionEnabled, int token, System.Action onComplete)
        {
            EnsureRest();
            float duration = mode == Mode.DetailFadeUp ? 0.16f : shellDuration;
            Vector2 to = _restAnchored;
            if (mode == Mode.DetailFadeUp)
                to.y = _restAnchored.y - detailOffsetY;
            else
                to.x = _restAnchored.x + direction * shellOffsetX;

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            Vector2 fromPos = rectTransform != null ? rectTransform.anchoredPosition : _restAnchored;
            float fromAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

            if (!motionEnabled || duration <= 0f)
            {
                FinishExit(token, onComplete);
                yield break;
            }

            float elapsed = 0f;
            AnimationCurve curve = SettingsMotionEasing.FigmaEaseOut;
            while (elapsed < duration)
            {
                if (token != _token) yield break;
                float t = SettingsMotionEasing.Evaluate(curve, elapsed / duration);
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, 0f, t);
                if (rectTransform != null)
                    rectTransform.anchoredPosition = Vector2.LerpUnclamped(fromPos, to, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            FinishExit(token, onComplete);
        }

        private void FinishEnter(int token, System.Action onComplete)
        {
            if (token != _token) return;
            EnsureRest();
            if (rectTransform != null)
                rectTransform.anchoredPosition = _restAnchored;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            _routine = null;
            onComplete?.Invoke();
        }

        private void FinishExit(int token, System.Action onComplete)
        {
            if (token != _token) return;
            EnsureRest();
            if (rectTransform != null)
                rectTransform.anchoredPosition = _restAnchored;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            _routine = null;
            onComplete?.Invoke();
        }

        private void StopActive(bool bumpToken = true)
        {
            if (bumpToken)
                _token++;
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private void CaptureRest()
        {
            if (rectTransform == null)
                rectTransform = transform as RectTransform;
            if (rectTransform != null)
            {
                _restAnchored = rectTransform.anchoredPosition;
                _restCaptured = true;
            }
        }

        private void EnsureRest()
        {
            if (!_restCaptured)
                CaptureRest();
        }
    }
}
