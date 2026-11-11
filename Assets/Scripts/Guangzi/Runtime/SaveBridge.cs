using System.IO;
using Guangzi.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 存 / 读档桥接（save bridge）：Core 出 DTO，这里用 JsonUtility 落盘。
    ///
    /// 为什么序列化放 Runtime 而不是 Core：JsonUtility 是 UnityEngine 的东西，
    /// Core 必须保持零引擎引用，否则单元测试就跑不出编辑器了。
    ///
    /// 存档落在 Application.persistentDataPath，开发期够用；
    /// 正式版要换成 玩家指定的档位 + 原子写入（先写 .tmp 再替换，避免写一半断电）。
    /// </summary>
    public class SaveBridge : MonoBehaviour
    {
        [Header("调试快捷键")]
        [SerializeField] private bool enableHotkeys = true;

        [Tooltip("F5 存档 / F9 读档")]
        [SerializeField] private string fileName = "guangzi_save.json";

        [Header("日志")]
        [SerializeField] private bool logToConsole = true;

        private GridWorld _world;

        public string SavePath => Path.Combine(Application.persistentDataPath, fileName);

        private void Start()
        {
            _world = GridWorld.Instance;
            if (_world == null)
            {
                Debug.LogError("[SaveBridge] 场景里找不到 GridWorld。");
            }
        }

        private void Update()
        {
            if (!enableHotkeys) return;

            // 菜单里的 F5 / F9 不该触发存读档
            if (!InputModeService.IsWorld) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f5Key.wasPressedThisFrame) Save();
            if (kb.f9Key.wasPressedThisFrame) Load();
        }

        public void Save()
        {
            if (_world == null) _world = GridWorld.Instance;
            if (_world == null) return;

            var data = _world.SaveToData();
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);

            if (logToConsole)
            {
                int count = data.objects != null ? data.objects.Length : 0;
                Debug.Log("[SaveBridge] 已存档：" + count + " 个物块 → " + SavePath);
            }
        }

        public void Load()
        {
            if (_world == null) _world = GridWorld.Instance;
            if (_world == null) return;

            if (!File.Exists(SavePath))
            {
                Debug.LogWarning("[SaveBridge] 没有找到存档：" + SavePath);
                return;
            }

            string json = File.ReadAllText(SavePath);
            var data = JsonUtility.FromJson<SaveData>(json);

            if (data == null)
            {
                Debug.LogError("[SaveBridge] 存档解析失败：" + SavePath);
                return;
            }

            if (data.version != 1)
            {
                Debug.LogWarning("[SaveBridge] 存档版本是 " + data.version + "，当前代码只认 1，可能读不全。");
            }

            _world.LoadFromData(data);

            if (logToConsole)
            {
                int count = data.objects != null ? data.objects.Length : 0;
                Debug.Log("[SaveBridge] 已读档：" + count + " 个物块 ← " + SavePath);
            }
        }

        public void DeleteSave()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                if (logToConsole) Debug.Log("[SaveBridge] 已删除存档。");
            }
        }
    }
}
