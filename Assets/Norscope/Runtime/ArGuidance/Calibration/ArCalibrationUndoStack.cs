using System;
using UnityEngine;

namespace Norscope.ArGuidance
{
    /// <summary>Bounded undo history for calibration transforms (max 20).</summary>
    public sealed class ArCalibrationUndoStack
    {
        public readonly struct Snapshot
        {
            public readonly string ComponentId;
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;
            public readonly Vector3 LocalScale;

            public Snapshot(string id, Vector3 pos, Quaternion rot, Vector3 scale)
            {
                ComponentId = id ?? string.Empty;
                LocalPosition = pos;
                LocalRotation = rot;
                LocalScale = scale;
            }
        }

        private readonly Snapshot[] _items;
        private int _count;

        public ArCalibrationUndoStack(int capacity = 20)
        {
            _items = new Snapshot[Mathf.Max(1, capacity)];
            _count = 0;
        }

        public int Count => _count;

        public void Clear() => _count = 0;

        public void Push(Snapshot snapshot)
        {
            if (_count < _items.Length)
            {
                _items[_count++] = snapshot;
                return;
            }

            Array.Copy(_items, 1, _items, 0, _items.Length - 1);
            _items[_items.Length - 1] = snapshot;
        }

        public bool TryPop(out Snapshot snapshot)
        {
            if (_count <= 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = _items[--_count];
            return true;
        }
    }
}
