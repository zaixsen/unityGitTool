using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

[CustomEditor(typeof(FestivalConfigListAsset))]
public class FestivalConfigListAssetEditor : Editor
{
    private FestivalConfigListAsset config;
    [SerializeField]
    private bool useFestivalResources = true;
    [SerializeField]
    private bool _lastToogleState = false;
    private void OnEnable()
    {
        config = (FestivalConfigListAsset)target;
        _lastToogleState = config.useFestivalResources;
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("Festival 配置列表", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        config.useFestivalResources = EditorGUILayout.ToggleLeft(" 启用节日资源", config.useFestivalResources);
        EditorGUILayout.LabelField(config.useFestivalResources ? "优先使用节日资源" : "不使用节日资源");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (_lastToogleState != config.useFestivalResources)
        {
            RefreshAllEntries();
            YooAssetFilterManager.RefreshFestivalResourceSet(config);
            _lastToogleState = config.useFestivalResources;
        }
        if (GUILayout.Button("更新配置"))
        {
            RefreshAllEntries();
            YooAssetFilterManager.RefreshFestivalResourceSet(config);
        }

        EditorGUILayout.Space();

        bool changed = false;

        for (int i = 0; i < config.entries.Count; i++)
        {
            var entry = config.entries[i];

            EditorGUILayout.BeginVertical("Box");

            // 显示文件夹名作为标题
            string displayName = entry.DisplayName;
            EditorGUILayout.LabelField($" {displayName}", EditorStyles.miniBoldLabel);

            // 拖拽根文件夹
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("根目录");
            Object newFolder = EditorGUILayout.ObjectField(entry.rootFolder, typeof(DefaultAsset), false, GUILayout.Height(18));

            if (newFolder != entry.rootFolder)
            {
                // 防止重复添加
                if (config.entries.Exists(e => e.rootFolder == newFolder))
                {
                    Debug.LogWarning($"已存在该文件夹配置：{AssetDatabase.GetAssetPath(newFolder)}");
                }
                else
                {
                    entry.rootFolder = newFolder;
                    RefreshEntry(entry);
                    changed = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            // 显示可选的 Festival 子文件夹
            if (entry.rootFolder != null)
            {
                string festivalPath = System.IO.Path.Combine(entry.RootPath, "Festival").Replace("\\", "/");
                string guid = AssetDatabase.AssetPathToGUID(festivalPath);

                if (string.IsNullOrEmpty(guid))
                {
                    EditorGUILayout.HelpBox("Festival 文件夹不存在,请创建此文件夹", MessageType.Warning);
                }
                else
                {
                    // 获取直接子文件夹
                    var subfolders = GetImmediateSubfolders(festivalPath);
                    var names = subfolders.Select(p => System.IO.Path.GetFileName(p)).ToArray();

                    int selectedIndex = Mathf.Max(0, System.Array.IndexOf(subfolders.ToArray(), entry.selectedSubfolderPath));
                    selectedIndex = EditorGUILayout.Popup("选择子文件夹", selectedIndex, names);

                    if (selectedIndex >= 0 && selectedIndex < subfolders.Count)
                    {
                        string newPath = subfolders[selectedIndex];
                        if (entry.selectedSubfolderPath != newPath)
                        {
                            entry.selectedSubfolderPath = newPath;
                            changed = true;
                        }
                    }
                }
            }

            // 删除按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                config.entries.RemoveAt(i);
                changed = true;
                break; // 防止越界
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        // 添加新项
        if (GUILayout.Button("添加配置"))
        {
            config.entries.Add(new FestivalEntry());
            changed = true;
        }

        // 显示结果
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("当前选中的 Festival 子文件夹：", EditorStyles.boldLabel);
        var selectedPaths = config.GetAllSelectedPaths();
        if (selectedPaths.Length == 0)
        {
            EditorGUILayout.LabelField("（无）", EditorStyles.miniLabel);
        }
        else
        {
            foreach (var path in selectedPaths)
            {
                string folderName = System.IO.Path.GetFileName(path);
                EditorGUILayout.LabelField($"• {folderName} → {path}", EditorStyles.miniLabel);
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(config);
            Repaint();
        }
    }

    // 获取指定文件夹下的直接子文件夹（Unity 资源路径）
    private List<string> GetImmediateSubfolders(string parentPath)
    {
        var guids = AssetDatabase.FindAssets("", new[] { parentPath });
        var result = new HashSet<string>(); // 去重

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Directory.Exists(path)) // 确保是文件夹
            {
                string parentDir = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                if (parentDir == parentPath)
                {
                    result.Add(path);
                }
            }
        }

        return new List<string>(result.OrderBy(x => x)); // 排序
    }

    // 刷新单个条目
    private void RefreshEntry(FestivalEntry entry)
    {
        if (entry.rootFolder == null) return;

        string festivalPath = System.IO.Path.Combine(entry.RootPath, "Festival").Replace("\\", "/");
        string guid = AssetDatabase.AssetPathToGUID(festivalPath);
        if (string.IsNullOrEmpty(guid)) return;

        var subfolders = GetImmediateSubfolders(festivalPath);
        if (subfolders.Count == 0) return;

        // 如果当前选择无效，则设为第一个
        if (!subfolders.Contains(entry.selectedSubfolderPath))
        {
            entry.selectedSubfolderPath = subfolders[0];
        }
    }

    // 刷新所有
    private void RefreshAllEntries()
    {
        foreach (var entry in config.entries)
        {
            RefreshEntry(entry);
        }
        EditorUtility.SetDirty(config);
        Repaint();
    }
}