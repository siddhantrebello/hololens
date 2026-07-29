using System;
using UnityEngine;

namespace Norscope.TaskLibrary
{
    /// <summary>One step of a task's procedure. Warning/verification/image are optional —
    /// leave them empty/null when a step doesn't need them.</summary>
    [Serializable]
    public sealed class TaskStep
    {
        public string stepTitle = string.Empty;
        [TextArea(2, 5)] public string instruction = string.Empty;
        public Sprite image;
        [TextArea(1, 3)] public string warning = string.Empty;
        [TextArea(1, 3)] public string verification = string.Empty;
    }
}
