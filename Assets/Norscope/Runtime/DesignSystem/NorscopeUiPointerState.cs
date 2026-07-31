using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Norscope.DesignSystem
{
    /// <summary>
    /// XR UI often drops OnPointerExit / OnDeselect when the ray leaves empty space.
    /// These helpers clear stale hover/focus so highlights cannot stick.
    /// </summary>
    public static class NorscopeUiPointerState
    {
        public const float HoverStaleSeconds = 0.12f;

        public static bool IsStillSelected(Transform host, Selectable selectable)
        {
            EventSystem es = EventSystem.current;
            if (es == null || host == null)
                return false;

            GameObject selected = es.currentSelectedGameObject;
            if (selected == null)
                return false;

            if (selectable != null && selected == selectable.gameObject)
                return true;
            if (selected == host.gameObject)
                return true;

            Transform selectedTransform = selected.transform;
            return selectedTransform.IsChildOf(host) || host.IsChildOf(selectedTransform);
        }

        public static bool IsHoverFresh(float lastHoverSignalUnscaledTime)
        {
            return Time.unscaledTime - lastHoverSignalUnscaledTime <= HoverStaleSeconds;
        }

        public static void ClearSelectionIfOurs(Transform host, Selectable selectable)
        {
            EventSystem es = EventSystem.current;
            if (es == null || host == null)
                return;

            GameObject selected = es.currentSelectedGameObject;
            if (selected == null)
                return;

            bool ours = (selectable != null && selected == selectable.gameObject) ||
                        selected == host.gameObject;
            if (ours)
                es.SetSelectedGameObject(null);
        }
    }
}
