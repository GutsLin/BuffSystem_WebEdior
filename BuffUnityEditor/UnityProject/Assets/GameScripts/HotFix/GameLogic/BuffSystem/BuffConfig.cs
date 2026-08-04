namespace GameLogic.Buffs
{
    public static class BuffConfig
    {
        public static int MaxActionDepth { get; set; } = 16;
        public static float AuraRefreshInterval { get; set; } = 0.1f;
        public static float MinAuraRefreshInterval { get; set; } = 0.02f;
        public static int MaxThinkAttemptsPerTick { get; set; } = 10;
        public static float StatusResistanceCap { get; set; } = 0.95f;
        public static int InitialInstancePoolCapacity { get; set; } = 64;
    }
}
