using System;

namespace GameLogic.Item
{
    [Serializable]
    public sealed class BagItemData
    {
        public string BuffKey;
        public string DisplayName;
        public string Description;
        public string IconLocation;
        public int Count;

        public BagItemData Clone()
        {
            return new BagItemData
            {
                BuffKey = BuffKey,
                DisplayName = DisplayName,
                Description = Description,
                IconLocation = IconLocation,
                Count = Count,
            };
        }
    }
}
