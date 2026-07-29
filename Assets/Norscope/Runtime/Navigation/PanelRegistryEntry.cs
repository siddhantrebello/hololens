using System;
using UnityEngine;
using Norscope.Windows;

namespace Norscope.Navigation
{
    [Serializable]
    public sealed class PanelRegistryEntry
    {
        public PanelType panelType = PanelType.None;
        public SpatialWindowId windowId = SpatialWindowId.None;
        public PanelBehaviourType behaviour = PanelBehaviourType.WorldLocked;
        public GameObject prefab;
    }
}
