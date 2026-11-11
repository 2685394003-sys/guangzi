using System;
using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 简易下拉列表（dropdown list）：点一下展开选项，选完自动收起。
    ///
    /// 为什么不用 Unity 自带的 Dropdown：
    /// 那个要一整套 Template / Viewport / Content / Item 结构，手搭容易缺件，
    /// 而且它的弹出层会被父级的遮罩裁掉。这里就三个选项，自己拼一个面板更可控。
    ///
    /// 结构：
    ///   TriggerButton (点它展开)
    ///   Panel (展开的竖列表，默认关)
    ///     ├ Option_0 / Option_1 / Option_2
    ///   Blocker (全屏透明，点它收起)
    /// </summary>
    public class DropdownList : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Button triggerButton;
        [SerializeField] private Text triggerLabel;
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject blocker;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private Text[] optionLabels;

        [Header("选项")]
        [SerializeField] private string[] optionNames = { "全部", "方块", "非方块" };

        private int _value;

        public int Value => _value;

        public event Action<int> OnValueChanged;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (triggerButton != null) triggerButton.onClick.AddListener(Toggle);

            if (blocker != null)
            {
                var blockBtn = blocker.GetComponent<Button>();
                if (blockBtn != null) blockBtn.onClick.AddListener(Hide);
            }

            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    int index = i;   // 闭包要捕获副本，不能直接捕获循环变量
                    if (optionButtons[i] == null) continue;
                    optionButtons[i].onClick.AddListener(() => Choose(index));
                }
            }

            if (panel != null) panel.SetActive(false);
            if (blocker != null) blocker.SetActive(false);

            RefreshVisual();
        }

        private void OnDestroy()
        {
            if (triggerButton != null) triggerButton.onClick.RemoveListener(Toggle);
        }

        public void SetOptions(string[] names)
        {
            optionNames = names;

            if (optionLabels == null) return;

            for (int i = 0; i < optionLabels.Length; i++)
            {
                if (optionLabels[i] == null) continue;
                optionLabels[i].text = i < names.Length ? names[i] : string.Empty;
            }

            RefreshVisual();
        }

        /// <summary>外部改了值（比如切换面板时重置），不触发回调。</summary>
        public void SetValueWithoutNotify(int value)
        {
            _value = Mathf.Clamp(value, 0, Mathf.Max(0, (optionNames != null ? optionNames.Length : 1) - 1));
            RefreshVisual();
        }

        private void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        private void Show()
        {
            if (panel != null) panel.SetActive(true);
            if (blocker != null) blocker.SetActive(true);
            if (panel != null) panel.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
            if (blocker != null) blocker.SetActive(false);
        }

        private void Choose(int index)
        {
            Hide();

            if (index == _value) return;

            _value = index;
            RefreshVisual();

            OnValueChanged?.Invoke(_value);
        }

        private void RefreshVisual()
        {
            if (triggerLabel == null) return;
            if (optionNames == null || optionNames.Length == 0) return;

            triggerLabel.text = _value >= 0 && _value < optionNames.Length ? optionNames[_value] : optionNames[0];
        }
    }
}
