using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class FolderTypeCollector
{
    public static List<Type> CollectTypesFromFolder(string folderPath)
    {
        HashSet<Type> types = new HashSet<Type>();
        foreach (string assetPath in EnumerateScriptAssetPaths(folderPath))
        {
            MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (monoScript == null)
            {
                continue;
            }

            ProjectTypeUtility.TryAddType(monoScript.GetClass(), types);
        }

        return ProjectTypeUtility.OrderTypes(types);
    }

    public static string GetSelectedProjectFolderPath()
    {
        foreach (UnityEngine.Object selectedObject in Selection.GetFiltered(typeof(DefaultAsset), SelectionMode.Assets))
        {
            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.IsValidFolder(assetPath))
            {
                return assetPath;
            }
        }

        return string.Empty;
    }

    public static bool TryGetProjectRelativePath(string path, out string projectRelativePath)
    {
        string fullPath = Path.GetFullPath(path);
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string normalizedProjectRoot = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
        {
            projectRelativePath = string.Empty;
            return false;
        }

        projectRelativePath = fullPath.Substring(normalizedProjectRoot.Length)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        return true;
    }

    private static IEnumerable<string> EnumerateScriptAssetPaths(string folderPath)
    {
        string absoluteFolderPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(absoluteFolderPath))
        {
            yield break;
        }

        foreach (string filePath in Directory.EnumerateFiles(absoluteFolderPath, "*.cs", SearchOption.AllDirectories))
        {
            if (TryGetProjectRelativePath(filePath, out string assetPath))
            {
                yield return assetPath;
            }
        }
    }
}
