using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Toolbar
{
    // 通过 EditorWindow 管理 ToolbarActionBase 派生动作：
    // - 自动发现所有动作
    // - 为每个动作设置/保存位置
    // - 生成新的动作脚本模板
    public class ActionManagerWindow : EditorWindow
    {
        [MenuItem("Tools/Toolbar/Action Manager", priority = 2200)]
        public static void Open()
        {
            var win = GetWindow<ActionManagerWindow>(true, "Toolbar Action Manager");
            win.minSize = new Vector2(580, 380);
            win.Show();
        }

        private class ActionInfo
        {
            public Type type;
            public ToolbarActionBase instance;
            public string buttonName;
            public string buttonText;
            public ToolbarActionBase.ToolbarPlacement placement;
            public ToolbarActionBase.ToolbarPlacement editedPlacement;
            public bool visible;
            public bool editedVisible;
        }

        private readonly List<ActionInfo> _actions = new List<ActionInfo>();
        private Vector2 _scroll;
        private string _filter = string.Empty;
        // 检测重复的 ButtonName（重复会导致显隐联动）
        private bool _hasDuplicateButtonNames = false;
        private List<string> _duplicateButtonNames = new List<string>();

        // 创建脚本面板数据
        private string _newClassName = "NewToolbar";
        private string _newButtonName = "NewToolbarAction";
        private string _newButtonText = "New Action";
        private string _newTooltip = string.Empty;

        private ToolbarActionBase.ToolbarPlacement _newDefaultPlacement =
            ToolbarActionBase.ToolbarPlacement.NearPlayLeft;

        private void OnEnable()
        {
            RefreshActionList();
        }

        private void RefreshActionList()
        {
            _actions.Clear();
            var types = TypeCache.GetTypesDerivedFrom<ToolbarActionBase>();
            foreach (var t in types)
            {
                if (t.IsAbstract) continue;
                var ctor = t.GetConstructor(Type.EmptyTypes);
                if (ctor == null) continue;
                var instance = (ToolbarActionBase)Activator.CreateInstance(t);

                // 通过基类公开方法直接拿按钮名；按钮文本通过 CreateButton() 获取
                string btnName = SafeGetButtonName(instance);
                string btnText = SafeGetButtonText(instance);
                var placement = instance.GetPlacement();
                var visible = instance.IsVisible();

                _actions.Add(new ActionInfo
                {
                    type = t,
                    instance = instance,
                    buttonName = btnName,
                    buttonText = btnText,
                    placement = placement,
                    editedPlacement = placement,
                    visible = visible,
                    editedVisible = visible
                });
            }

            // 统计重复的 ButtonName
            _duplicateButtonNames = _actions
                .GroupBy(a => a.buttonName)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            _hasDuplicateButtonNames = _duplicateButtonNames.Count > 0;
        }

        private static string SafeGetButtonName(ToolbarActionBase action)
        {
            try
            {
                return action.GetButtonName();
            }
            catch
            {
            }

            try
            {
                var b = action.CreateButton();
                return b?.name ?? string.Empty;
            }
            catch
            {
            }

            return action?.GetType().Name ?? string.Empty;
        }

        private static string SafeGetButtonText(ToolbarActionBase action)
        {
            try
            {
                var b = action.CreateButton();
                return string.IsNullOrEmpty(b?.text) ? action.GetType().Name : b.text;
            }
            catch
            {
            }

            return action?.GetType().Name ?? string.Empty;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawHeader();
                EditorGUILayout.Space(6);
                DrawActionList();
                EditorGUILayout.Space(8);
                DrawCreateScriptPanel();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Toolbar Action 管理器", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("自动识别所有继承 ToolbarActionBase 的按钮，并可在此设置每个按钮在主工具栏中的位置。\n修改后立即生效（无需重启 Editor）。",
                MessageType.Info);

            // 如果存在重复 ButtonName，提示用户修复，否则显隐会联动到同名按钮
            if (_hasDuplicateButtonNames)
            {
                EditorGUILayout.HelpBox(
                    "检测到重复的 ButtonName：\n" + string.Join(", ", _duplicateButtonNames) +
                    "\n这些按钮在显隐控制时会一起变化。请确保每个 ToolbarActionBase 的 ButtonName 唯一。",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                // 过滤框
                EditorGUILayout.LabelField("筛选", GUILayout.Width(36));
                _filter = EditorGUILayout.TextField(_filter);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("刷新列表", GUILayout.Width(100)))
                {
                    RefreshActionList();
                }
            }
        }

        private void DrawActionList()
        {
            EditorGUILayout.LabelField("已发现的动作（可编辑位置）", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
            try
            {
                foreach (var a in _actions)
                {
                    if (!string.IsNullOrEmpty(_filter))
                    {
                        var f = _filter.Trim();
                        if (!a.buttonName.Contains(f, StringComparison.OrdinalIgnoreCase) &&
                            !a.buttonText.Contains(f, StringComparison.OrdinalIgnoreCase) &&
                            !a.type.Name.Contains(f, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUILayout.LabelField($"{a.type.Name}", EditorStyles.boldLabel);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("ButtonName:", GUILayout.Width(90));
                            EditorGUILayout.SelectableLabel(a.buttonName, GUILayout.Height(16));
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("ButtonText:", GUILayout.Width(90));
                            EditorGUILayout.SelectableLabel(a.buttonText, GUILayout.Height(16));
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Visible:", GUILayout.Width(90));
                            a.editedVisible = EditorGUILayout.Toggle(a.editedVisible);
                            // 切换后立即应用，无需额外按钮
                            if (a.editedVisible != a.visible)
                            {
                                ApplyVisibility(a);
                            }
                            GUILayout.FlexibleSpace();
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Placement:", GUILayout.Width(90));
                            a.editedPlacement =
                                (ToolbarActionBase.ToolbarPlacement)EditorGUILayout.EnumPopup(a.editedPlacement);
                            GUILayout.FlexibleSpace();
                            if (a.editedPlacement != a.placement)
                            {
                                if (GUILayout.Button("应用位置", GUILayout.Width(100)))
                                {
                                    ApplyPlacement(a);
                                }
                            }

                            if (GUILayout.Button("重新定位", GUILayout.Width(100)))
                            {
                                // 不改变配置，仅根据现有配置重新摆放
                                ToolbarPlacementUtility.PlaceOrRepositionButton(a.instance);
                            }
                        }
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("应用全部修改", GUILayout.Width(140)))
                {
                    foreach (var a in _actions)
                    {
                        if (a.editedPlacement != a.placement)
                        {
                            ApplyPlacement(a);
                        }
                        if (a.editedVisible != a.visible)
                        {
                            ApplyVisibility(a);
                        }
                    }
                }
            }
        }

        private void ApplyPlacement(ActionInfo a)
        {
            var key = $"Seawar.Toolbar.Placement.{a.buttonName}";
            EditorPrefs.SetInt(key, (int)a.editedPlacement);
            a.placement = a.editedPlacement;
            // 即时重新放置
            ToolbarPlacementUtility.PlaceOrRepositionButton(a.instance);
        }

        private void ApplyVisibility(ActionInfo a)
        {
            a.instance.SetVisible(a.editedVisible);
            a.visible = a.editedVisible;
            // 即时应用到已创建的按钮
            ToolbarActionBase.ApplyVisibility(a.buttonName);
        }

        private void DrawCreateScriptPanel()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("创建新的 Toolbar Action 脚本", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "在 Assets/Editor/Toolbar/Actions 下生成派生自 ToolbarActionBase 的脚本模板。编译完成后即会被自动识别，可在上方列表中调整位置。",
                MessageType.None);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                _newClassName = EditorGUILayout.TextField("Class 名称", _newClassName);
                _newButtonName = EditorGUILayout.TextField("ButtonName", _newButtonName);
                _newButtonText = EditorGUILayout.TextField("ButtonText", _newButtonText);
                _newTooltip = EditorGUILayout.TextField("Tooltip", _newTooltip);
                _newDefaultPlacement =
                    (ToolbarActionBase.ToolbarPlacement)EditorGUILayout.EnumPopup("默认位置", _newDefaultPlacement);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("创建脚本", GUILayout.Width(120)))
                    {
                        CreateNewActionScript();
                    }

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("打开脚本目录", GUILayout.Width(120)))
                    {
                        EditorUtility.RevealInFinder(Path.Combine(Application.dataPath, "Editor/Toolbar/Actions"));
                    }
                }
            }
        }

        private void CreateNewActionScript()
        {
            if (string.IsNullOrWhiteSpace(_newClassName))
            {
                EditorUtility.DisplayDialog("错误", "Class 名称不能为空", "确定");
                return;
            }

            if (string.IsNullOrWhiteSpace(_newButtonName))
            {
                EditorUtility.DisplayDialog("错误", "ButtonName 不能为空", "确定");
                return;
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            if (_newClassName.Any(c => invalidChars.Contains(c)))
            {
                EditorUtility.DisplayDialog("错误", "Class 名称包含非法字符", "确定");
                return;
            }

            var relDir = "Assets/Editor/Toolbar/Actions";
            var absDir = Path.Combine(Application.dataPath, "Editor/Toolbar/Actions");
            if (!Directory.Exists(absDir)) Directory.CreateDirectory(absDir);

            var relPath = Path.Combine(relDir, _newClassName + ".cs");
            var absPath = Path.Combine(absDir, _newClassName + ".cs");
            if (File.Exists(absPath))
            {
                EditorUtility.DisplayDialog("提示", "文件已存在：" + relPath, "确定");
                EditorUtility.RevealInFinder(absPath);
                return;
            }

            var content = GetNewActionTemplate(_newClassName, _newButtonName, _newButtonText, _newTooltip,
                _newDefaultPlacement);
            File.WriteAllText(absPath, content);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("成功", "已创建脚本：" + relPath + "\n等待编译完成后在上方列表中可见。", "确定");

            // 尝试高亮并打开（如果可用）
            var asset = AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(relPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                AssetDatabase.OpenAsset(asset);
            }
        }

        private static string EscapeForCSharpLiteral(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string GetNewActionTemplate(string className, string buttonName, string buttonText,
            string tooltip, ToolbarActionBase.ToolbarPlacement placement)
        {
            var bn = EscapeForCSharpLiteral(buttonName);
            var bt = EscapeForCSharpLiteral(buttonText);
            var tt = EscapeForCSharpLiteral(tooltip);

            return $"using UnityEngine;\n" +
                   $"\n" +
                   $"namespace Toolbar\n" +
                   $"{{\n" +
                   $"    public class {className}Action : ToolbarActionBase\n" +
                   $"    {{\n" +
                   $"        protected override string ButtonName => \"{bn}Button\";\n" +
                   $"        protected override string ButtonText => \"{bt}\";\n" +
                   $"        protected override string ButtonTooltip => \"{tt}\";\n" +
                   $"        protected override ToolbarActionBase.ToolbarPlacement DefaultPlacement => ToolbarActionBase.ToolbarPlacement.{placement};\n\n" +
                   $"        public override void Execute()\n" +
                   $"        {{\n" +
                   $"            Debug.Log(\"{className} executed\");\n" +
                   $"            // TODO: 在此实现你的逻辑\n" +
                   $"        }}\n" +
                   $"    }}\n" +
                   $"}}" +
                   $"\n";
        }
    }
}