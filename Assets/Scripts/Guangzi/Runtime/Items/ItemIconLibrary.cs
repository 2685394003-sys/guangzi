using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 物品图标表（item icon library）：把 ItemDef 里的图标名映射成 Sprite。
    ///
    /// 放在 Runtime 层而不是 Core，因为 Core 不碰 Unity 资源。
    /// 资源约定：Resources/Icons/Items/&lt;icon&gt;.png，名字和 ItemDef.icon 对上。
    /// 找不到就当没有图标，调用方退回色块——不能让缺图把格子搞成空白。
    /// </summary>
    [DefaultExecutionOrder(-190)]
    public class ItemIconLibrary : MonoBehaviour
    {
        [Tooltip("Resources 下的图标目录，不带扩展名")]
        [SerializeField] private string resourcePath = "Icons/Items";

        private readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();

        public static ItemIconLibrary Instance { get; private set; }

        public int Count => _icons.Count;

        private void Awake()
        {
            Instance = this;
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Load()
        {
            _icons.Clear();

            var sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogWarning("[ItemIconLibrary] 没在 Resources/" + resourcePath + " 找到任何 Sprite。");
                return;
            }

            for (int i = 0; i < sprites.Length; i++)
            {
                var sprite = sprites[i];
                if (sprite == null || string.IsNullOrEmpty(sprite.name)) continue;
                _icons[sprite.name] = sprite;
            }

            Debug.Log("[ItemIconLibrary] 载入 " + _icons.Count + " 个物品图标");
        }

        public Sprite Get(string iconKey)
        {
            if (string.IsNullOrEmpty(iconKey)) return null;

            Sprite sprite;
            return _icons.TryGetValue(iconKey, out sprite) ? sprite : null;
        }

        /// <summary>给某个物品取图标。没有就返回 null，调用方自己退回色块。</summary>
        public static Sprite Resolve(ItemDef def)
        {
            if (def == null || !def.HasIcon) return null;
            if (Instance == null) return null;

            return Instance.Get(def.icon);
        }
    }
}
