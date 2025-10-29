using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Toolbar
{
    /// <summary>
    /// 工具类：发现并获取 Unity 主工具栏的各区域，提供统一的按钮放置/移动接口。
    /// </summary>
    public static class ToolbarPlacementUtility
    {
        public static bool GetToolbarZones(out VisualElement root, out VisualElement leftZone, out VisualElement playZone, out VisualElement rightZone, out VisualElement unityToolbar)
        {
            root = leftZone = playZone = rightZone = unityToolbar = null;

            var editorAssembly = typeof(Editor).Assembly;
            var toolbarType = editorAssembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null)
                return false;

            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            if (toolbars == null || toolbars.Length == 0)
                return false;

            var toolbar = toolbars[0];
            root = GetToolbarRoot(toolbarType, toolbar);
            if (root == null)
                return false;

            leftZone = root.Q("ToolbarZoneLeftAlign");
            playZone = root.Q("ToolbarZonePlayMode");
            rightZone = root.Q("ToolbarZoneRightAlign");
            unityToolbar = root.Q(className: "unity-toolbar") ?? root;
            return true;
        }

        public static bool TryPlaceButton(VisualElement leftZone, VisualElement playZone, VisualElement rightZone, VisualElement fallback, VisualElement button, ToolbarActionBase.ToolbarPlacement placement)
        {
            switch (placement)
            {
                case ToolbarActionBase.ToolbarPlacement.NearPlayLeft:
                    if (playZone?.parent != null)
                    {
                        var parent = playZone.parent;
                        var idx = parent.IndexOf(playZone);
                        parent.Insert(idx, button);
                        return true;
                    }
                    break;
                case ToolbarActionBase.ToolbarPlacement.NearPlayRight:
                    if (playZone?.parent != null)
                    {
                        var parent = playZone.parent;
                        var idx = parent.IndexOf(playZone);
                        parent.Insert(idx + 1, button);
                        return true;
                    }
                    break;
                case ToolbarActionBase.ToolbarPlacement.LeftAlignStart:
                    if (leftZone != null)
                    {
                        leftZone.Insert(0, button);
                        return true;
                    }
                    break;
                case ToolbarActionBase.ToolbarPlacement.LeftAlignEnd:
                    if (leftZone != null)
                    {
                        leftZone.Insert(leftZone.childCount, button);
                        return true;
                    }
                    break;
                case ToolbarActionBase.ToolbarPlacement.RightAlignStart:
                    if (rightZone != null)
                    {
                        rightZone.Insert(0, button);
                        return true;
                    }
                    break;
                case ToolbarActionBase.ToolbarPlacement.RightAlignEnd:
                    if (rightZone != null)
                    {
                        rightZone.Insert(rightZone.childCount, button);
                        return true;
                    }
                    break;
            }
            fallback.Add(button);
            return false;
        }

        public static void PlaceOrRepositionButton(ToolbarActionBase action)
        {
            if (!GetToolbarZones(out var root, out var leftZone, out var playZone, out var rightZone, out var unityToolbar))
                return;

            var button = root.Q(action.GetButtonNameSafe()) ?? action.CreateButton();
            // 若已存在，先移除再重新定位
            button.RemoveFromHierarchy();

            var placement = action.GetPlacement();
            TryPlaceButton(leftZone, playZone, rightZone, unityToolbar, button, placement);
        }

        private static VisualElement GetToolbarRoot(System.Type toolbarType, object toolbar)
        {
            VisualElement root = null;
            var rootProp = toolbarType.GetProperty("rootVisualElement", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (rootProp != null)
                root = rootProp.GetValue(toolbar) as VisualElement;
            if (root == null)
            {
                var altRootProp = toolbarType.GetProperty("root", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (altRootProp != null)
                    root = altRootProp.GetValue(toolbar) as VisualElement;
            }
            if (root == null)
            {
                var mRootField = toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
                if (mRootField != null)
                    root = mRootField.GetValue(toolbar) as VisualElement;
            }
            return root;
        }
    }

    // 扩展：让基类可以安全获取其 ButtonName（用于查询现有元素）
    public static class ToolbarActionExtensions
    {
        public static string GetButtonNameSafe(this ToolbarActionBase action)
        {
            try
            {
                // 直接使用公开的按钮名，避免为了取名而创建临时按钮导致状态污染
                return action.GetButtonName() ?? string.Empty;
            }
            catch { return string.Empty; }
        }
    }
}