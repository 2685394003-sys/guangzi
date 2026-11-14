using System;
using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 物品表加载器。把 Resources 里的 JSON 读成 Core 层的 ItemDef。
    ///
    /// 为什么不直接用 ScriptableObject：计划表里定过，配置要出成 JSON
    /// 给天工引擎复用。Unity 这边只负责解析，换引擎时 JSON 一行都不用改。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class ItemDatabase : MonoBehaviour
    {
        [Tooltip("Resources 下的相对路径，不带扩展名")]
        [SerializeField] private string resourcePath = "Data/items";

        [Tooltip("载入失败时打印详细原因，关掉就只在错误时提示")]
        [SerializeField] private bool verboseLog = true;

        public ItemCatalog Catalog { get; private set; }

        public static ItemDatabase Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Catalog = new ItemCatalog();
            Load();
        }

        private void Load()
        {
            var asset = Resources.Load<TextAsset>(resourcePath);

            if (asset == null)
            {
                Debug.LogError("[ItemDatabase] 找不到配置：Resources/" + resourcePath + ".json");
                return;
            }

            ItemFile file = null;
            try
            {
                file = JsonUtility.FromJson<ItemFile>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError("[ItemDatabase] JSON 解析失败：" + e.Message);
                return;
            }

            if (file == null || file.items == null)
            {
                Debug.LogError("[ItemDatabase] 配置里没有 items 数组。");
                return;
            }

            int loaded = 0;
            int skipped = 0;

            for (int i = 0; i < file.items.Length; i++)
            {
                var json = file.items[i];
                if (json == null || string.IsNullOrEmpty(json.id)) { skipped++; continue; }

                ItemCategory category;
                if (!Enum.TryParse(json.category, true, out category)) category = ItemCategory.Misc;

                var def = new ItemDef(
                    json.id,
                    json.displayName,
                    category,
                    json.maxStack,
                    json.color,
                    json.nutrition,
                    json.sellPrice,
                    json.toolTier,
                    json.miningBonus,
                    json.icon);

                if (Catalog.Register(def)) loaded++;
                else
                {
                    skipped++;
                    Debug.LogWarning("[ItemDatabase] 重复的物品 id 被忽略：" + json.id);
                }
            }

            if (verboseLog)
            {
                Debug.Log("[ItemDatabase] 载入 " + loaded + " 个物品" +
                          (skipped > 0 ? "，跳过 " + skipped + " 个" : string.Empty));
            }
        }

        public ItemDef Get(string id)
        {
            return Catalog != null ? Catalog.Get(id) : null;
        }

        // ---------- JSON DTO ----------

        [Serializable]
        private sealed class ItemFile
        {
            public ItemJson[] items;
        }

        [Serializable]
        private sealed class ItemJson
        {
            public string id;
            public string displayName;
            public string category;
            public string color;
            public int maxStack;
            public int nutrition;
            public int sellPrice;
            public int toolTier;
            public float miningBonus;

            /// <summary>图标资源名，对应 Resources/Icons/Items/&lt;icon&gt;.png</summary>
            public string icon;
        }
    }
}
