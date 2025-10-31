using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
/// <summary>
/// 更换节日资源步骤：
/// 1、添加配置
/// 2、指定文件夹根目录（需要进行节日过滤的文件夹）
/// 3、在根目录创建Festival 文件夹
/// 4、所有需要更换的节日文件都在此文件夹内
/// 5、更新配置
/// </summary>
[CreateAssetMenu(fileName = "FestivalConfigList", menuName = "Coolfish/Create Festival Config List", order = 2)]
public class FestivalConfigListAsset : ScriptableObject
{
    [SerializeField]
    public List<FestivalEntry> entries = new List<FestivalEntry>();
    [SerializeField]
    public bool useFestivalResources = true;

    /// <summary>
    /// 获取所有当前选中的 Festival 子文件夹路径（Unity 资源路径）
    /// </summary>
    public string[] GetAllSelectedPaths()
    {
        var paths = new List<string>();
        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.selectedSubfolderPath))
            {
                paths.Add(entry.selectedSubfolderPath);
            }
        }
        return paths.ToArray();
    }

    /// <summary>
    /// 获取所有选中的子文件夹名称（如 Christmas, Halloween）
    /// </summary>
    public string[] GetAllSelectedFolderNames()
    {
        var names = new List<string>();
        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.selectedSubfolderPath))
            {
                names.Add(System.IO.Path.GetFileName(entry.selectedSubfolderPath));
            }
        }
        return names.ToArray();
    }

    /// <summary>
    /// 获取所有选中的子文件夹对应的 Object（可用于加载资源）
    /// </summary>
    public Object[] GetAllSelectedFoldersAsObject()
    {
        var objects = new List<Object>();
        foreach (var path in GetAllSelectedPaths())
        {
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (folder != null) objects.Add(folder);
        }
        return objects.ToArray();
    }
}
[System.Serializable]
public class FestivalEntry
{
    [Tooltip("指定的文件需要有Festival文件夹")]
    public Object rootFolder; // 拖拽的根文件夹
    public string selectedSubfolderPath = ""; // 当前选中的 Festival/xxx 路径

    public string RootPath => rootFolder != null ? AssetDatabase.GetAssetPath(rootFolder) : "";
    public string DisplayName => rootFolder != null ? System.IO.Path.GetFileName(RootPath) : "<未设置>";
}
