using System;
using UnityEngine;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Component selection, dirty-state gate, and Prev/Next navigation.
    /// Presentation stays in the panel; this owns selection rules only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArCalibrationSelectionController : MonoBehaviour
    {
        public enum PendingNav
        {
            None = 0,
            Previous = 1,
            Next = 2,
            Index = 3
        }

        [SerializeField] private ArCalibrationController calibration;

        private string[] _order = Array.Empty<string>();
        private int _pendingIndex = -1;
        private PendingNav _pending = PendingNav.None;
        private Action<string> _applySelection;

        public bool IsDirty { get; private set; }
        public PendingNav PendingNavigation => _pending;
        public event Action DirtyChanged;
        public event Action SelectionChanged;
        public event Action PendingNavChanged;

        /// <summary>componentOrder must be the exact same list (by content) used to build the
        /// dropdown's visible options, so index i here always means the same component as
        /// dropdown option i. onApply is the single authoritative "make this the shown
        /// component" entry point — every path that changes selection (dropdown, Prev/Next,
        /// confirm-save-and-continue, confirm-discard-and-continue) funnels through it, so
        /// there is exactly one place that decides what "select and show" means.</summary>
        public void Bind(ArCalibrationController controller, string[] componentOrder, Action<string> onApply)
        {
            calibration = controller;
            _order = componentOrder ?? Array.Empty<string>();
            _applySelection = onApply;
            _pending = PendingNav.None;
            _pendingIndex = -1;
        }

        public void SetDirty(bool dirty)
        {
            if (IsDirty == dirty)
                return;
            IsDirty = dirty;
            DirtyChanged?.Invoke();
        }

        public void ClearDirty() => SetDirty(false);

        public int CurrentIndex
        {
            get
            {
                if (calibration == null || _order.Length == 0)
                    return 0;
                for (int i = 0; i < _order.Length; i++)
                {
                    if (_order[i] == calibration.SelectedComponentId)
                        return i;
                }

                return 0;
            }
        }

        public bool CanGoPrevious => CurrentIndex > 0;
        public bool CanGoNext => CurrentIndex < _order.Length - 1;

        public bool RequestPrevious()
        {
            if (!CanGoPrevious)
                return false;
            return RequestIndex(CurrentIndex - 1, PendingNav.Previous);
        }

        public bool RequestNext()
        {
            if (!CanGoNext)
                return false;
            return RequestIndex(CurrentIndex + 1, PendingNav.Next);
        }

        public bool RequestIndex(int index)
        {
            return RequestIndex(index, PendingNav.Index);
        }

        private bool RequestIndex(int index, PendingNav kind)
        {
            if (_order.Length == 0 || calibration == null)
                return false;
            index = Mathf.Clamp(index, 0, _order.Length - 1);
            if (index == CurrentIndex)
                return false;

            if (IsDirty)
            {
                _pending = kind;
                _pendingIndex = index;
                PendingNavChanged?.Invoke();
                return false;
            }

            ApplyIndex(index);
            return true;
        }

        public void ConfirmSaveAndContinue()
        {
            if (_pending == PendingNav.None || _pendingIndex < 0)
                return;
            calibration?.SaveSelected();
            ClearDirty();
            int idx = _pendingIndex;
            ClearPending();
            ApplyIndex(idx);
        }

        public void ConfirmDiscardAndContinue()
        {
            if (_pending == PendingNav.None || _pendingIndex < 0)
                return;
            calibration?.RestoreSaved();
            ClearDirty();
            int idx = _pendingIndex;
            ClearPending();
            ApplyIndex(idx);
        }

        public void CancelPendingNavigation()
        {
            ClearPending();
        }

        private void ClearPending()
        {
            _pending = PendingNav.None;
            _pendingIndex = -1;
            PendingNavChanged?.Invoke();
        }

        private void ApplyIndex(int index)
        {
            if (calibration == null || _order.Length == 0)
                return;
            index = Mathf.Clamp(index, 0, _order.Length - 1);
            string componentId = _order[index];
            if (_applySelection != null)
            {
                _applySelection(componentId);
            }
            else
            {
                // Fallback only — Bind() should always supply this.
                calibration.SelectComponent(componentId);
                calibration.ShowSelected(true);
            }

            ClearDirty();
            SelectionChanged?.Invoke();
        }
    }
}
