using System;
using System.Collections.Generic;
using UnityEngine;

namespace Norscope.Localization
{
    [Serializable]
    public sealed class LocalizedTaskStepContent
    {
        public string stepTitle = string.Empty;
        [TextArea] public string instruction = string.Empty;
        [TextArea] public string warning = string.Empty;
        [TextArea] public string verification = string.Empty;
    }

    [Serializable]
    public sealed class LocalizedTaskContent
    {
        public string locale = string.Empty;
        public string displayName = string.Empty;
        [TextArea] public string description = string.Empty;
        public List<LocalizedTaskStepContent> steps = new List<LocalizedTaskStepContent>();
    }
}
