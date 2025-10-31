using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Toolbar
{
    // 可滚动的信息展示窗口，支持行内完整显示与时间戳
    public class ToolbarInfoWindow : EditorWindow
    {
        private string _titleText;
        private string _content;
        private Vector2 _scroll;

        public static void Show(string title, string message)
        {
            var wnd = GetWindow<ToolbarInfoWindow>(true, "信息", true);
            wnd._titleText = string.IsNullOrEmpty(title) ? "信息" : title;
            wnd._content = ToTimestamped(message);
            wnd.minSize = new Vector2(520, 320);
            wnd.Show();
            wnd.Focus();
        }

        private static string ToTimestamped(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            var sb = new StringBuilder();
            var lines = message.Replace("\r\n", "\n").Replace("\r", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.None);
            var stamp = DateTime.Now.ToString("HH:mm:ss");
            foreach (var line in lines)
            {
                sb.Append('[').Append(stamp).Append(']').Append(' ').AppendLine(line);
            }
            return sb.ToString();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(_titleText ?? "信息", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var style = new GUIStyle(EditorStyles.label)
            {
                richText = false,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            // 顶对齐：不强制撑满高度，让内容从顶部开始渲染
            EditorGUILayout.LabelField(_content ?? string.Empty, style);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("复制到剪贴板"))
            {
                EditorGUIUtility.systemCopyBuffer = _content ?? string.Empty;
            }
            if (GUILayout.Button("关闭"))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}