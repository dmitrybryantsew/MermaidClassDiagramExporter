using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class MermaidClassDiagramExporter
{
    private const string ExportDirectoryName = "Docs/Mermaid";
    private const string ExportBaseFileName = "SelectedClasses";
    private const string LastFolderPathKey = "MermaidClassDiagramExporter.LastFolderPath";
    private const string LastMmdPathKey = "MermaidClassDiagramExporter.LastMmdPath";
    private const string LastMarkdownPathKey = "MermaidClassDiagramExporter.LastMarkdownPath";

    [MenuItem("Tools/Mermaid/Export Selected Classes")]
    private static void ExportSelectedClasses()
    {
        List<Type> selectedTypes = SelectionTypeCollector.CollectSelectedTypes();
        if (selectedTypes.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Mermaid Export",
                "Select one or more scripts, components, ScriptableObjects, or GameObjects with project-defined components first.",
                "OK");
            return;
        }

        ExportTypes(selectedTypes, "Selected Classes", ExportBaseFileName);
    }

    [MenuItem("Tools/Mermaid/Export Selected Classes", true)]
    private static bool CanExportSelectedClasses()
    {
        return SelectionTypeCollector.CollectSelectedTypes().Count > 0;
    }

    [MenuItem("Tools/Mermaid/Export Classes From Folder...")]
    private static void ExportClassesFromFolder()
    {
        string initialDirectory = GetInitialFolderDirectory();
        string selectedFolder = EditorUtility.OpenFolderPanel("Select Folder To Export Classes From", initialDirectory, string.Empty);
        if (string.IsNullOrEmpty(selectedFolder))
        {
            return;
        }

        if (!FolderTypeCollector.TryGetProjectRelativePath(selectedFolder, out string projectRelativeFolder))
        {
            EditorUtility.DisplayDialog(
                "Mermaid Export",
                "Please choose a folder inside this Unity project.",
                "OK");
            return;
        }

        ExportClassesFromFolderPath(projectRelativeFolder);
    }

    [MenuItem("Tools/Mermaid/Export Classes From Selected Project Folder")]
    private static void ExportClassesFromSelectedProjectFolder()
    {
        string selectedFolder = FolderTypeCollector.GetSelectedProjectFolderPath();
        if (string.IsNullOrEmpty(selectedFolder))
        {
            EditorUtility.DisplayDialog(
                "Mermaid Export",
                "Select a folder in the Project window first, or use Tools > Mermaid > Export Classes From Folder....",
                "OK");
            return;
        }

        ExportClassesFromFolderPath(selectedFolder);
    }

    [MenuItem("Tools/Mermaid/Export Classes From Selected Project Folder", true)]
    private static bool CanExportClassesFromSelectedProjectFolder()
    {
        return !string.IsNullOrEmpty(FolderTypeCollector.GetSelectedProjectFolderPath());
    }

    [MenuItem("Tools/Mermaid/Reveal Last Export")]
    private static void RevealLastExport()
    {
        string path = EditorPrefs.GetString(LastMmdPathKey, string.Empty);
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            EditorUtility.RevealInFinder(path);
            return;
        }

        EditorUtility.DisplayDialog("Mermaid Export", "No exported Mermaid file was found yet.", "OK");
    }

    [MenuItem("Tools/Mermaid/Reveal Last Export", true)]
    private static bool CanRevealLastExport()
    {
        string path = EditorPrefs.GetString(LastMmdPathKey, string.Empty);
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    [MenuItem("Tools/Mermaid/Copy Last Export To Clipboard")]
    private static void CopyLastExportToClipboard()
    {
        string path = EditorPrefs.GetString(LastMmdPathKey, string.Empty);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            EditorUtility.DisplayDialog("Mermaid Export", "No exported Mermaid file was found yet.", "OK");
            return;
        }

        EditorGUIUtility.systemCopyBuffer = File.ReadAllText(path);
        Debug.Log("Copied Mermaid diagram text to the clipboard from: " + path);
    }

    [MenuItem("Tools/Mermaid/Copy Last Export To Clipboard", true)]
    private static bool CanCopyLastExportToClipboard()
    {
        string path = EditorPrefs.GetString(LastMmdPathKey, string.Empty);
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    [MenuItem("Tools/Mermaid/Open Mermaid Live Editor")]
    private static void OpenMermaidLiveEditor()
    {
        Application.OpenURL("https://mermaid.live/");
    }

    private static void ExportClassesFromFolderPath(string folderPath)
    {
        List<Type> folderTypes = FolderTypeCollector.CollectTypesFromFolder(folderPath);
        if (folderTypes.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Mermaid Export",
                "No loadable project-defined classes were found in that folder.",
                "OK");
            return;
        }

        EditorPrefs.SetString(LastFolderPathKey, folderPath);

        string title = "Classes in " + folderPath;
        string exportBaseName = "Folder_" + SanitizeFileName(folderPath);
        ExportTypes(folderTypes, title, exportBaseName);
    }

    private static void ExportTypes(IReadOnlyList<Type> types, string title, string exportBaseName)
    {
        GraphSourceKind sourceKind = exportBaseName == ExportBaseFileName ? GraphSourceKind.Selection : GraphSourceKind.Folder;
        TypeGraph graph = TypeGraphBuilder.BuildGraph(types, title, sourceKind, title);
        string diagram = MermaidGraphExporter.BuildDiagram(graph);
        string exportDirectory = GetExportDirectory();
        Directory.CreateDirectory(exportDirectory);

        string mmdPath = Path.Combine(exportDirectory, exportBaseName + ".mmd");
        string markdownPath = Path.Combine(exportDirectory, exportBaseName + ".md");

        File.WriteAllText(mmdPath, diagram);
        File.WriteAllText(markdownPath, BuildMarkdownWrapper(title, diagram));

        EditorPrefs.SetString(LastMmdPathKey, mmdPath);
        EditorPrefs.SetString(LastMarkdownPathKey, markdownPath);
        EditorGUIUtility.systemCopyBuffer = diagram;

        EditorUtility.RevealInFinder(mmdPath);
        Debug.Log(
            "Exported Mermaid class diagram for "
            + graph.Nodes.Count
            + " type(s).\nMMD: "
            + mmdPath
            + "\nMarkdown: "
            + markdownPath
            + "\nThe Mermaid text was also copied to the clipboard.");
    }

    private static string BuildMarkdownWrapper(string title, string mermaidDiagram)
    {
        return "# " + title + "\n\n```mermaid\n" + mermaidDiagram + "\n```\n";
    }

    private static string GetInitialFolderDirectory()
    {
        string lastFolderPath = EditorPrefs.GetString(LastFolderPathKey, string.Empty);
        if (!string.IsNullOrEmpty(lastFolderPath) && Directory.Exists(Path.GetFullPath(lastFolderPath)))
        {
            return Path.GetFullPath(lastFolderPath);
        }

        return Application.dataPath;
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        return new string(value
            .Select(character => invalidCharacters.Contains(character) || character == '/' || character == '\\' ? '_' : character)
            .ToArray());
    }

    private static string GetExportDirectory()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ExportDirectoryName));
    }
}
