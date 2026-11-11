namespace Guangzi.Runtime
{
    /// <summary>
    /// 相机视角模式。两种视角对应两类操作场景，不是"高级/低级"的关系。
    /// </summary>
    public enum CameraViewMode
    {
        /// <summary>
        /// 上帝视角：自由镜头，WASD 平移、拖拽、滚轮缩放。
        /// 适合规划布局、看整张地图。
        /// </summary>
        God = 0,

        /// <summary>
        /// 角色视角（星露谷式）：镜头跟随角色，WASD 交给角色。
        /// 适合干活、盯着一个人。
        /// </summary>
        Character = 1,
    }
}
