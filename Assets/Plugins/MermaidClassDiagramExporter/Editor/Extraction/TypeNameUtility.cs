using System;
using System.Linq;
using System.Reflection;

internal static class TypeNameUtility
{
    public static string BuildTypeId(Type type)
    {
        string source = type.FullName ?? type.Name;
        return "T_" + new string(source.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
    }

    public static string BuildTypeDisplayName(Type type)
    {
        return BuildTypeName(type, true);
    }

    public static string BuildTypeName(Type type, bool useMermaidGenerics)
    {
        if (type == null)
        {
            return "void";
        }

        Type nullableType = Nullable.GetUnderlyingType(type);
        if (nullableType != null)
        {
            return BuildTypeName(nullableType, useMermaidGenerics) + "?";
        }

        if (type.IsByRef || type.IsPointer)
        {
            Type elementType = type.GetElementType();
            return BuildTypeName(elementType, useMermaidGenerics);
        }

        if (type.IsArray)
        {
            Type elementType = type.GetElementType();
            return BuildTypeName(elementType, useMermaidGenerics) + "[]";
        }

        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (type.IsGenericType)
        {
            string genericName = type.Name;
            int tickIndex = genericName.IndexOf('`');
            if (tickIndex >= 0)
            {
                genericName = genericName.Substring(0, tickIndex);
            }

            genericName = genericName.Replace('+', '.');

            string separatorLeft = useMermaidGenerics ? "~" : "<";
            string separatorRight = useMermaidGenerics ? "~" : ">";
            string arguments = string.Join(", ", type.GetGenericArguments().Select(argument => BuildTypeName(argument, useMermaidGenerics)));
            return genericName + separatorLeft + arguments + separatorRight;
        }

        return GetFriendlyTypeName(type) ?? type.Name.Replace('+', '.');
    }

    public static TypeNodeKind BuildNodeKind(Type type)
    {
        if (type.IsInterface)
        {
            return TypeNodeKind.Interface;
        }

        if (type.IsEnum)
        {
            return TypeNodeKind.Enum;
        }

        if (type.IsValueType && !type.IsPrimitive)
        {
            return TypeNodeKind.Struct;
        }

        if (type.IsAbstract && type.IsSealed)
        {
            return TypeNodeKind.StaticClass;
        }

        if (type.IsAbstract)
        {
            return TypeNodeKind.AbstractClass;
        }

        return TypeNodeKind.Class;
    }

    public static TypeVisibility BuildVisibility(MethodBase method)
    {
        if (method.IsPublic)
        {
            return TypeVisibility.Public;
        }

        if (method.IsFamily || method.IsFamilyOrAssembly)
        {
            return TypeVisibility.Protected;
        }

        if (method.IsAssembly)
        {
            return TypeVisibility.Internal;
        }

        return TypeVisibility.Private;
    }

    public static bool ShouldListMember(FieldInfo field)
    {
        return field.IsPublic;
    }

    public static bool ShouldListMember(PropertyInfo property)
    {
        MethodInfo accessor = property.GetMethod ?? property.SetMethod;
        return accessor != null && accessor.IsPublic && property.GetIndexParameters().Length == 0;
    }

    public static bool ShouldListMember(MethodBase method)
    {
        return method.IsPublic || method.IsFamily || method.IsAssembly || method.IsFamilyOrAssembly;
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type == typeof(void))
        {
            return "void";
        }

        if (type == typeof(int))
        {
            return "int";
        }

        if (type == typeof(float))
        {
            return "float";
        }

        if (type == typeof(double))
        {
            return "double";
        }

        if (type == typeof(decimal))
        {
            return "decimal";
        }

        if (type == typeof(bool))
        {
            return "bool";
        }

        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(long))
        {
            return "long";
        }

        if (type == typeof(short))
        {
            return "short";
        }

        if (type == typeof(byte))
        {
            return "byte";
        }

        if (type == typeof(char))
        {
            return "char";
        }

        if (type == typeof(uint))
        {
            return "uint";
        }

        if (type == typeof(ulong))
        {
            return "ulong";
        }

        if (type == typeof(ushort))
        {
            return "ushort";
        }

        if (type == typeof(sbyte))
        {
            return "sbyte";
        }

        if (type == typeof(object))
        {
            return "object";
        }

        return null;
    }
}
