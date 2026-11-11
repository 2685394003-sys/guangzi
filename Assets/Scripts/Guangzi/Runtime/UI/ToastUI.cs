using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 屏幕提示条（toast）：在屏幕中下方弹一句话，过几秒自己消失。
    ///
    /// 用于「操作没成功」这类即时反馈——比如往随从背包里塞东西但装不下。
    /// 不做队列堆叠，后一条直接覆盖前一条，避免刷屏。
    /// </summary>
    public class ToastUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text label;

        [Tooltip("停留时间（秒）")]
        [SerializeField] private float duration = 2.2f;

        private float _hideAt;
        private static ToastUI _instance;

        private void Awake()
        {
            _instance = this;
            if (root != null) root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (root == null || !root.gameObject.activeSelf) return;

            if (Time.time >= _hideAt) root.SetActive(false);
        }

        public static void Show(string message)
        {
            if (_instance == null || _instance.root == null) return;

            if (_instance.label != null) _instance.label.text = message;

            _instance.root.SetActive(true);
            _instance.root.transform.SetAsLastSibling();
            _instance._hideAt = Time.time + Mathf.Max(0.5f, _instance.duration);
        }
    }
}
