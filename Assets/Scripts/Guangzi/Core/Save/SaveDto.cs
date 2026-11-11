using System;

namespace Guangzi.Core
{
    /// <summary>
    /// 存档 DTO。**纯数据，只用 public 字段 + 数组**，因为 Runtime 层要用 Unity 的
    /// JsonUtility 直接序列化它——JsonUtility 不支持 Dictionary、属性、泛型嵌套。
    /// 改结构时记得同步 version，读档侧要能识别旧版本。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int width;
        public int height;
        public PlacedObjectSave[] objects;
        public ResourceSave[] resources;
    }

    [Serializable]
    public class PlacedObjectSave
    {
        public int id;
        public string defId;
        public int x;
        public int z;
        public int hp;
    }

    [Serializable]
    public class ResourceSave
    {
        public string resourceId;
        public int amount;
    }
}
