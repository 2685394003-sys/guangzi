using System;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>当前输入归属哪一套控制器。</summary>
    public enum InputMode
    {
        /// <summary>世界操作：建造、拖相机、以后的角色移动。</summary>
        World = 0,

        /// <summary>菜单操作：只允许 UI 响应，世界侧控制器一律停手。</summary>
        Menu = 1,
    }

    /// <summary>
    /// 输入模式服务（input mode service）——**两套控制系统的唯一开关**。
    ///
    /// 为什么要它：ESC 菜单打开后，世界侧控制器（建造、相机拖拽、存档快捷键，以后的角色移动）
    /// 必须整体失效，否则会出现「点菜单的同时把墙盖到地图上」。
    /// 让每个控制器自己去判断 UI 状态，等于把同一个判断写 N 遍，还会漏。
    ///
    /// 用法：控制器在 Update / HandleInput 开头写一行
    ///     if (!InputModeService.IsWorld) return;
    /// 就自动被隔离开。新增角色控制器也照这个来，不需要改这里。
    ///
    /// 刻意做成静态类而不是 MonoBehaviour：它没有生命周期，不该出现在层级里，
    /// 也不该有「场景里忘了放」这种失败模式。
    /// </summary>
    public static class InputModeService
    {
        public static InputMode Current { get; private set; } = InputMode.World;

        /// <summary>模式切换时触发。给需要「进入/退出某模式时做一次事」的控制器用。</summary>
        public static event Action<InputMode> Changed;

        public static bool IsWorld => Current == InputMode.World;

        public static bool IsMenu => Current == InputMode.Menu;

        public static void Set(InputMode mode)
        {
            if (Current == mode) return;

            Current = mode;
            if (Changed != null) Changed(mode);
        }

        /// <summary>
        /// 关闭 Domain Reload 时静态状态会跨 Play 残留，这里强制回到世界模式，
        /// 避免「上次退出时停在菜单里，这次一进 Play 所有操作全失效」。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Current = InputMode.World;
            Changed = null;
        }
    }
}
