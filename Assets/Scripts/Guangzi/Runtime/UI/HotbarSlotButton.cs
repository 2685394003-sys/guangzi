using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 快捷栏格子的点击处理器。挂在每个 Slot 上，把点击转发给 HotbarUI。
    ///
    /// 为什么要单独一个脚本：Button 的 onClick 是 UnityEvent，
    /// 想在运行时给 9 个格子各绑一个带参数的监听，用代码设序列化引用很别扭
    /// （需要 UnityEventTools 之类的编辑器 API）。一个小 MonoBehaviour 更直接，
    /// 索引写在 Inspector 里，一眼能看到每个格子绑的是第几个。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HotbarSlotButton : MonoBehaviour
    {
        [SerializeField] private HotbarUI hotbar;
        [SerializeField] private int index;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null) _button.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClicked);
        }

        private void OnClicked()
        {
            if (hotbar == null) hotbar = FindFirstObjectByType<HotbarUI>();
            if (hotbar != null) hotbar.Select(index);
        }

        /// <summary>给编辑器工具用：一次性设好归属和序号。</summary>
        public void Initialize(HotbarUI owner, int slotIndex)
        {
            hotbar = owner;
            index = slotIndex;
        }
    }
}
