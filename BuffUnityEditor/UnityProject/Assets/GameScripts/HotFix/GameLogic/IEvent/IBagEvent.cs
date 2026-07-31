using TEngine;

namespace GameLogic
{
    [EventInterface(EEventGroup.GroupUI)]
    public interface IBagEvent
    {
        void OnItemAdded(string buffKey, int count);

        void OnItemUsed(string buffKey);

        void OnPlayerAttributeChanged();

        void OnBagOpened();

        void OnBagClosed();
    }
}
