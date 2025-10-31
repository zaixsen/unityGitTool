using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class YooAssetFilterManager
{
    [SerializeField]
    private static FestivalResourceSet _festivals = new FestivalResourceSet();
    private const int TryLoadCount = 0;
    private static int tryLoadConfigCount;

    /// <summary>
    /// 初始化节日资源集合（在构建或启动时调用）
    /// </summary>
    public static void RefreshFestivalResourceSet(FestivalConfigListAsset config)
    {
        _festivals.LoadFromConfig(config);
    }

    /// <summary>
    /// 判断是否应保留该非节日资源（基于文件名匹配）
    /// </summary>
    /// <param name="assetPath">资源路径</param>
    /// <returns>true: 保留；false: 被节日资源覆盖，不应保留</returns>
    public static bool ShouldKeepNonFestivalAsset(string assetPath)
    {
        if (tryLoadConfigCount == TryLoadCount)
        {
            tryLoadConfigCount++;
            _festivals.TryLoadFromConfig();
        }

        string dir = Path.GetFileName(Path.GetDirectoryName(assetPath));
        string fileName = Path.GetFileName(assetPath);
        string combined = Path.Combine(dir, fileName).Replace("\\", "/");
        //是否为节日资源
        bool isFestivalRes = assetPath.Contains("Festival");
        //是否是指定的节日资源
        bool isNeedFetivalRes = _festivals.HasFestivalAsset(combined);

        // 不使用节日资源
        if (!_festivals.HasAny())
            return !isFestivalRes;  //返回基础资源

        //基础资源 && 指定的节日资源含有同名文件
        if (!isFestivalRes && _festivals.HasFestivalFileNameAsset(fileName))
            return false;

        //节日资源
        if (isFestivalRes)
        {
            //是否为指定的节日资源
            return isNeedFetivalRes;
        }

        //基础资源不同名
        return true;
    }
}
/// <summary>
/// 管理所有已激活的节日资源文件名（用于过滤判断）
/// </summary>
[Serializable]
public class FestivalResourceSet
{
    public const string CONFIG_PATH = "Assets/BuildSetting/FestivalFiltered/FestivalConfigList.asset";
    private HashSet<string> _existingFilenames = new HashSet<string>();

    public void TryLoadFromConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<FestivalConfigListAsset>(CONFIG_PATH);
        LoadFromConfig(config);
    }

    /// <summary>
    /// 从配置中加载所有节日资源的文件名（不含扩展名）
    /// </summary>
    public void LoadFromConfig(FestivalConfigListAsset config)
    {
        _existingFilenames.Clear();

        if (config == null || !config.useFestivalResources) return;

        string[] festivalPaths = config.GetAllSelectedPaths();
        if (festivalPaths == null) return;

        foreach (string folderPath in festivalPaths)
        {
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                continue;

            string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string dir = Path.GetFileName(Path.GetDirectoryName(path));
                string fileName = Path.GetFileName(path);
                string combined = Path.Combine(dir, fileName).Replace("\\", "/");
                if (Directory.Exists(path)) continue; // 忽略目录

                _existingFilenames.Add(combined.ToLower());
            }
        }
    }

    /// <summary>
    /// 查询是否存在指定文件夹+文件名的节日资源（不区分大小写）
    /// </summary>
    public bool HasFestivalAsset(string fileName)
    {
        return !string.IsNullOrEmpty(fileName) &&
               _existingFilenames.Contains(fileName.ToLower());
    }
    /// <summary>
    /// 查询是否存在指定文件名的节日资源（不区分大小写）
    /// </summary>
    public bool HasFestivalFileNameAsset(string fileName)
    {
        foreach (var item in _existingFilenames)
        {
            if (item.Contains(fileName.ToLower()))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 判断是否至少有一个节日资源（即：是否非空）
    /// </summary>
    public bool HasAny()
    {
        return _existingFilenames.Count > 0;
    }

    /// <summary>
    /// 获取当前节日资源数量（调试用）
    /// </summary>
    public int Count => _existingFilenames.Count;
}