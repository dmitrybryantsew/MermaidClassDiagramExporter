using System;
using System.Collections.Generic;
using UnityEditor;

internal static class TypeAssetPathResolver
{
    private static readonly Dictionary<Type, string> AssetPathByType = new Dictionary<Type, string>();
    private static bool cacheBuilt;

    public static string GetAssetPath(Type type)
    {
        if (type == null)
        {
            return string.Empty;
        }

        EnsureCache();
        return AssetPathByType.TryGetValue(type, out string assetPath) ? assetPath : string.Empty;
    }

    public static void InvalidateCache()
    {
        cacheBuilt = false;
        AssetPathByType.Clear();
    }

    private static void EnsureCache()
    {
        if (cacheBuilt)
        {
            return;
        }

        AssetPathByType.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:MonoScript"))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            Type scriptType = script != null ? script.GetClass() : null;
            if (scriptType == null || AssetPathByType.ContainsKey(scriptType))
            {
                continue;
            }

            AssetPathByType.Add(scriptType, assetPath);
        }

        cacheBuilt = true;
    }
}
