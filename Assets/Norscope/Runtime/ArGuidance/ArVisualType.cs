namespace Norscope.ArGuidance
{
    public enum ArVisualType
    {
        None = 0,
        LaptopOutline = 1,
        ScrewGroup = 2,
        LiftEdge = 3,
        ConnectorWarning = 4,
        FanHighlight = 5,
        Airflow = 6,

        // Phase 2 reusable primitives
        Outline = 10,
        ScrewMarker = 11,
        Connector = 12,
        Warning = 13,
        Direction = 14,
        Verification = 15,
        IconCue = 16
    }

    public enum ArHologramVisualState
    {
        Default = 0,
        Selected = 1,
        Warning = 2,
        Completed = 3,
        Danger = 4
    }
}
