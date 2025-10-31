using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace Toolbar
{
    // 负责把绘制出来的按钮组注入到 Unity 主工具栏
    [InitializeOnLoad]
    public static class MainToolbarInjector
    {
        private static bool _initialized;

        static MainToolbarInjector()
        {
            // Editor UI 就绪后尝试注入，多次尝试以提高成功率
            EditorApplication.update += TryInit;
            EditorApplication.delayCall += TryInit;
        }

        private static void TryInit()
        {
            if (_initialized)
                return;

            var editorAssembly = typeof(Editor).Assembly;
            var toolbarType = editorAssembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null)
            {
                Debug.LogWarning("[MainToolbarInjector] UnityEditor.Toolbar type not found; editor version may not expose toolbar visual tree.");
                return;
            }

            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            if (toolbars == null || toolbars.Length == 0)
            {
                // 等 Toolbar 创建后再试（下一帧）
                return;
            }

            var toolbar = toolbars[0];

            // 拿到 root VisualElement（不同版本成员名不同，做兼容）
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
            if (root == null)
                return;

            // 常见区域名称（Unity 官方 ToolbarZone 命名）
            VisualElement leftZone = root.Q("ToolbarZoneLeftAlign");
            VisualElement playZone = root.Q("ToolbarZonePlayMode");
            VisualElement rightZone = root.Q("ToolbarZoneRightAlign");
            VisualElement unityToolbar = root.Q(className: "unity-toolbar");
            VisualElement targetZone = leftZone ?? playZone ?? rightZone ?? unityToolbar ?? root;

            // 通过 TypeCache 自动发现所有继承 ToolbarActionBase 的 Action
            var actionTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<ToolbarActionBase>();
            foreach (var t in actionTypes)
            {
                if (t.IsAbstract) continue;
                // 只支持无参构造
                var ctor = t.GetConstructor(System.Type.EmptyTypes);
                if (ctor == null) continue;

                var action = (ToolbarActionBase)System.Activator.CreateInstance(t);
                // 放置或重新定位按钮
                ToolbarPlacementUtility.PlaceOrRepositionButton(action);
            }

            _initialized = true;
            EditorApplication.update -= TryInit;
            EditorApplication.delayCall -= TryInit;
        }
    }
}