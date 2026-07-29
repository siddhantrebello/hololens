namespace Norscope.Navigation
{
    /// <summary>How a panel or launcher should behave when opened / followed.</summary>
    public enum PanelBehaviourType
    {
        /// <summary>Always follows the head (Home Launcher).</summary>
        HeadFollowing = 0,

        /// <summary>On open request, spawn or bring existing instance in front of the user.</summary>
        RecenterOnOpen = 1,

        /// <summary>Opens once; stays at last world pose until moved manually.</summary>
        WorldLocked = 2
    }
}
