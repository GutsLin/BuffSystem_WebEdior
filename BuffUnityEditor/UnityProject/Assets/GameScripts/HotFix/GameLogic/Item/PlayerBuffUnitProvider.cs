using GameLogic.Buffs;

namespace GameLogic.Item
{
    public static class PlayerBuffUnitProvider
    {
        private static BuffUnit _playerBuffUnit;

        public static BuffUnit GetPlayerBuffUnit() => _playerBuffUnit;

        public static void SetPlayerBuffUnit(BuffUnit unit) => _playerBuffUnit = unit;
    }
}
