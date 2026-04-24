using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class SelectionTypeCollector
{
    public static List<Type> CollectSelectedTypes()
    {
        HashSet<Type> types = new HashSet<Type>();
        foreach (UnityEngine.Object selectedObject in Selection.objects)
        {
            if (selectedObject is MonoScript monoScript)
            {
                ProjectTypeUtility.TryAddType(monoScript.GetClass(), types);
                continue;
            }

            if (selectedObject is GameObject gameObject)
            {
                foreach (Component component in gameObject.GetComponents<Component>())
                {
                    if (component != null)
                    {
                        ProjectTypeUtility.TryAddType(component.GetType(), types);
                    }
                }

                continue;
            }

            if (selectedObject is Component componentSelection)
            {
                ProjectTypeUtility.TryAddType(componentSelection.GetType(), types);
                continue;
            }

            if (selectedObject is ScriptableObject scriptableObject)
            {
                ProjectTypeUtility.TryAddType(scriptableObject.GetType(), types);
            }
        }

        return ProjectTypeUtility.OrderTypes(types);
    }
}
