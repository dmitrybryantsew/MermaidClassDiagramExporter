using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

internal static class ProjectTypeUtility
{
    public static List<Type> OrderTypes(IEnumerable<Type> types)
    {
        return types
            .Distinct()
            .OrderBy(type => type.Namespace ?? string.Empty)
            .ThenBy(type => type.Name)
            .ToList();
    }

    public static void TryAddType(Type type, ISet<Type> types)
    {
        if (!ShouldIncludeType(type))
        {
            return;
        }

        types.Add(type);
    }

    public static bool ShouldIncludeType(Type type)
    {
        if (type == null)
        {
            return false;
        }

        if (type.IsGenericTypeDefinition || type.IsNestedPrivate)
        {
            return false;
        }

        Assembly assembly = type.Assembly;
        string assemblyName = assembly.GetName().Name ?? string.Empty;

        if (assemblyName.StartsWith("Unity", StringComparison.Ordinal)
            || assemblyName.StartsWith("System", StringComparison.Ordinal)
            || assemblyName.StartsWith("mscorlib", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
