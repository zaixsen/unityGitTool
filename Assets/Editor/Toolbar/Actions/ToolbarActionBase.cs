using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Toolbar
{
    // 所有工具栏动作的基础类：提供统一按钮创建、通用对话框、场景保存/文件存在等公共能力
    public abstract class ToolbarActionBase
    {
        // 运行中已创建的按钮实例，用于外部刷新其显隐
        private static readonly Dictionary<string, ToolbarButton> s_buttons = new Dictionary<string, ToolbarButton>();

        // 按钮的名称（用于查询与避免重复注入）
        protected abstract string ButtonName { get; }
        // 按钮显示文本
        protected abstract string ButtonText { get; }
        // 按钮提示文本（可选）
        protected virtual string ButtonTooltip => string.Empty;

        // 默认是否显示（可在具体 Action 中覆写），也可通过 EditorPrefs 进行持久化覆盖
        protected virtual bool DefaultVisible => true;

        // 每个 Action 可单独控制在 Toolbar 中的位置
        public enum ToolbarPlacement
        {
            NearPlayLeft = 0,   // 靠近播放区（其父容器）左侧
            NearPlayRight = 1,  // 靠近播放区（其父容器）右侧
            LeftAlignStart = 2, // 左对齐区开始
            LeftAlignEnd = 3,   // 左对齐区末尾
            RightAlignStart = 4,// 右对齐区开始
            RightAlignEnd = 5   // 右对齐区末尾
        }
        // 默认位置（可在具体 Action 中覆写），也可通过 EditorPrefs 进行持久化覆盖
        protected virtual ToolbarPlacement DefaultPlacement => ToolbarPlacement.NearPlayLeft;
        // 读取 EditorPrefs 覆盖后的实际位置（键：Seawar.Toolbar.Placement.{ButtonName}）
        public ToolbarPlacement GetPlacement()
        {
            var key = $"Seawar.Toolbar.Placement.{ButtonName}";
            var val = EditorPrefs.GetInt(key, (int)DefaultPlacement);
            return (ToolbarPlacement)val;
        }
        // 公开：获取按钮名称（供外部系统查询/显示）
        public string GetButtonName()
        {
            return ButtonName;
        }

        // 公开：读取/设置显隐（持久化到 EditorPrefs，键：Seawar.Toolbar.Visible.{ButtonName}）
        public bool IsVisible()
        {
            var key = $"Seawar.Toolbar.Visible.{ButtonName}";
            return EditorPrefs.GetBool(key, DefaultVisible);
        }

        public void SetVisible(bool visible)
        {
            var key = $"Seawar.Toolbar.Visible.{ButtonName}";
            EditorPrefs.SetBool(key, visible);
            ApplyVisibility(ButtonName);
        }

        // 创建一个标准化的 ToolbarButton，并绑定到 Execute()
        public virtual ToolbarButton CreateButton()
        {
            var btn = new ToolbarButton(() => Execute())
            {
                text = ButtonText,
                tooltip = ButtonTooltip
            };
            btn.name = ButtonName;
            btn.style.marginLeft = 4;
            btn.style.marginRight = 4;
            btn.style.flexShrink = 0;
            btn.style.alignSelf = Align.Center;
            // 注册并应用显隐
            s_buttons[ButtonName] = btn;
            btn.style.display = IsVisible() ? DisplayStyle.Flex : DisplayStyle.None;
            return btn;
        }

        // 执行动作的主体逻辑
        public abstract void Execute();

        // 公共：信息窗口（可滚动，行内完整显示，含时间戳）
        protected void ShowInfo(string title, string message)
        {
            ToolbarInfoWindow.Show(title, message);
        }

        // 公共：显示进度条（EditorUtility.DisplayProgressBar）
        protected void ShowProgress(string title, string info, float progress)
        {
            EditorUtility.DisplayProgressBar(title, info ?? string.Empty, Mathf.Clamp01(progress));
        }

        // 公共：清除进度条
        protected void ClearProgress()
        {
            EditorUtility.ClearProgressBar();
        }

        // 公共：进度条作用域（using 自动清理）
        protected ProgressScope BeginProgress(string title, string info = "", float progress = 0f)
        {
            EditorUtility.DisplayProgressBar(title, info ?? string.Empty, Mathf.Clamp01(progress));
            return new ProgressScope(title);
        }

        // 进度条作用域类型：支持 Update()，Dispose() 自动清除
        protected sealed class ProgressScope : System.IDisposable
        {
            private readonly string _title;

            internal ProgressScope(string title)
            {
                _title = title;
            }

            public void Update(string info, float progress)
            {
                EditorUtility.DisplayProgressBar(_title, info ?? string.Empty, Mathf.Clamp01(progress));
            }

            public void Dispose()
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // 公共：保存当前修改的场景（带用户确认）
        protected bool ConfirmSaveModifiedScenes()
        {
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        // 公共：根据 AssetPath 判断文件是否存在（适用于 Assets/ 开头路径）
        protected static bool FileExistsByAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;
            if (assetPath.StartsWith("Assets/"))
            {
                var pathUnderAssets = assetPath.Substring("Assets/".Length);
                var full = Path.Combine(Application.dataPath, pathUnderAssets.Replace("/", "\\"));
                return File.Exists(full);
            }
            var fullPath = Path.Combine(Application.dataPath, assetPath.Replace("/", "\\"));
            return File.Exists(fullPath);
        }

        // 公开静态：根据 EditorPrefs 立即应用指定按钮显隐（若实例已创建）
        public static void ApplyVisibility(string buttonName)
        {
            if (string.IsNullOrEmpty(buttonName)) return;
            var key = $"Seawar.Toolbar.Visible.{buttonName}";
            var visible = EditorPrefs.GetBool(key, true);
            var display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            // 首选：直接更新已注册的按钮实例
            if (s_buttons.TryGetValue(buttonName, out var btn) && btn != null)
            {
                btn.style.display = display;
            }

            // 回退：如果未注册或引用丢失，尝试在主工具栏树中查找真实实例并应用样式
            if (ToolbarPlacementUtility.GetToolbarZones(out var root, out _, out _, out _, out _))
            {
                var ve = root.Q(buttonName);
                if (ve != null)
                {
                    ve.style.display = display;
                }
            }
        }
    }
}