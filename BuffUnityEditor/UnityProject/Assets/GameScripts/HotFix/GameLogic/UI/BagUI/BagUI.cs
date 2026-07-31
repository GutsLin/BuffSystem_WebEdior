using System.Collections.Generic;
using GameLogic.Item;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    [Window(UILayer.Top, "BagUI")]
    public class BagUI : UIWindow
    {
        private Transform _tfGrid;
        private Text _textEmpty;
        private Button _btnClose;

        private readonly List<BagItemWidget> _widgets = new();
        private GameObject _widgetPrefab;

        protected override void ScriptGenerator()
        {
            _tfGrid = FindChild("m_grid_Items");
            _textEmpty = FindChildComponent<Text>("m_text_Empty");
            _btnClose = FindChildComponent<Button>("m_btn_Close");
            _btnClose.onClick.AddListener(OnCloseClicked);
        }

        protected override void RegisterEvent()
        {
            AddUIEvent<string, int>(IBagEvent_Event.OnItemAdded, OnItemAdded);
            AddUIEvent<string>(IBagEvent_Event.OnItemUsed, OnItemUsed);
        }

        protected override async void OnCreate()
        {
            _widgetPrefab = await GameModule.Resource.LoadAssetAsync<GameObject>("BagItemWidget");
            RefreshBag();
        }

        protected override void OnRefresh()
        {
            RefreshBag();
        }

        private void RefreshBag()
        {
            var items = BagSystem.Instance.Items;
            _textEmpty.gameObject.SetActive(items.Count == 0);

            int target = items.Count;
            for (int i = _widgets.Count - 1; i >= target; i--)
            {
                _widgets[i].CallDestroy();
                _widgets.RemoveAt(i);
            }

            for (int i = 0; i < target; i++)
            {
                if (i < _widgets.Count)
                {
                    _widgets[i].SetData(items[i].Clone(), i);
                }
                else
                {
                    var widget = CreateWidgetByPrefab<BagItemWidget>(_widgetPrefab, _tfGrid);
                    widget.SetData(items[i].Clone(), i);
                    _widgets.Add(widget);
                }
            }
        }

        private void OnItemAdded(string buffKey, int count)
        {
            RefreshBag();
        }

        private void OnItemUsed(string buffKey)
        {
            RefreshBag();
        }

        private void OnCloseClicked()
        {
            GameModule.UI.CloseUI<BagUI>();
        }

        protected override void OnDestroy()
        {
            if (_btnClose != null)
            {
                _btnClose.onClick.RemoveListener(OnCloseClicked);
            }
            _widgets.Clear();
        }
    }
}
