using GameLogic.Item;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    [Window(UILayer.UI, "BagItemWidget")]
    public class BagItemWidget : UIWidget
    {
        private Image _imgIcon;
        private Text _textName;
        private Text _textCount;
        private Button _btnUse;

        private BagItemData _data;
        private int _index;

        protected override void ScriptGenerator()
        {
            _imgIcon = FindChildComponent<Image>("m_img_Icon");
            _textName = FindChildComponent<Text>("m_text_Name");
            _textCount = FindChildComponent<Text>("m_text_Count");
            _btnUse = FindChildComponent<Button>("m_btn_Use");
            _btnUse.onClick.AddListener(OnUseClicked);
        }

        protected override void RegisterEvent()
        {
            AddUIEvent<string>(IBagEvent_Event.OnItemUsed, OnItemUsed);
        }

        public void SetData(BagItemData data, int index)
        {
            _data = data;
            _index = index;
            RefreshView();
        }

        private void RefreshView()
        {
            if (_data == null)
            {
                return;
            }

            _textName.text = _data.DisplayName;
            _textCount.text = _data.Count > 1 ? $"x{_data.Count}" : string.Empty;
            _imgIcon.SetSprite(_data.IconLocation, setNativeSize: true);
        }

        private void OnUseClicked()
        {
            if (_data == null)
            {
                return;
            }

            BagSystem.Instance.UseItem(_index);
        }

        private void OnItemUsed(string buffKey)
        {
            if (_data != null && _data.BuffKey == buffKey)
            {
                RefreshView();
            }
        }

        protected override void OnDestroy()
        {
            if (_btnUse != null)
            {
                _btnUse.onClick.RemoveListener(OnUseClicked);
            }
        }
    }
}
