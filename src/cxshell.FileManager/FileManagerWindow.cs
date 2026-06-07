using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace cxshell.FileManager;

public static class FileOperations
{
    public static string GetFileIcon(string extension) => extension.ToLowerInvariant() switch
    {
        ".cs" => "[green]#[/]",
        ".csproj" or ".sln" or ".slnx" => "[blue]P[/]",
        ".json" => "[yellow]{}[/]",
        ".xml" => "[cyan]<>[/]",
        ".md" or ".txt" => "[white]T[/]",
        ".yml" or ".yaml" => "[magenta]Y[/]",
        ".html" or ".htm" => "[red]H[/]",
        ".css" => "[blue]S[/]",
        ".js" or ".ts" => "[yellow]J[/]",
        ".py" => "[blue]Py[/]",
        ".sh" or ".bash" => "[green]$[/]",
        ".png" or ".jpg" or ".gif" or ".svg" => "[magenta]I[/]",
        ".zip" or ".tar" or ".gz" => "[red]Z[/]",
        ".dll" or ".exe" => "[dim]B[/]",
        ".log" => "[dim]L[/]",
        _ => "[dim].[/]"
    };

    public static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    public static void DeleteFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
        else if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    public static void RenameFile(string oldPath, string newName)
    {
        var directory = Path.GetDirectoryName(oldPath)!;
        var newPath = Path.Combine(directory, newName);

        if (File.Exists(oldPath))
            File.Move(oldPath, newPath);
        else if (Directory.Exists(oldPath))
            Directory.Move(oldPath, newPath);
    }

    public static bool HasSubdirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).Any();
        }
        catch
        {
            return false;
        }
    }
}

public static class FileManagerWindow
{
    private const int WindowWidth = 90;
    private const int WindowHeight = 30;
    private const int TreeColumnWidth = 35;
    private const int MaxDirectoryEntries = 50;
    private const int MaxFileEntries = 100;
    private const string LazyPlaceholder = "[dim]Loading...[/]";
    private const string DateFormat = "yyyy-MM-dd HH:mm";

    public static Window CreateWindow(ConsoleWindowSystem windowSystem, string? startPath = null)
    {
        var startDir = startPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!Directory.Exists(startDir))
            startDir = Directory.GetCurrentDirectory();

        var currentDir = startDir;

        var pathBar = Controls.Markup($"[bold] {startDir}[/]")
            .StickyTop()
            .WithName("pathBar")
            .Build();

        var tree = BuildDirectoryTree(startDir);

        var scrollPanel = Controls.ScrollablePanel()
            .AddControl(tree)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        var fileTable = Controls.Table()
            .AddColumn("Name")
            .AddColumn("Size", TextJustification.Right, 10)
            .AddColumn("Modified", TextJustification.Right, 18)
            .NoBorder()
            .WithHeaderColors(Color.White, Color.DarkSlateGray1)
            .StretchHorizontal()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithName("fileTable")
            .Build();

        PopulateFileTable(fileTable, startDir);

        var statusBar = Controls.Markup(FormatStatusText(startDir, fileTable.RowCount))
            .StickyBottom()
            .WithName("statusBar")
            .Build();

        tree.SelectedNodeChanged += (sender, args) =>
        {
            if (args.Node?.Tag is string path && Directory.Exists(path))
            {
                currentDir = path;
                pathBar.SetContent(new List<string> { $"[bold] {path}[/]" });
                PopulateFileTable(fileTable, path);
                statusBar.SetContent(new List<string> { FormatStatusText(path, fileTable.RowCount) });
            }
        };

        tree.NodeExpandCollapse += (sender, args) =>
        {
            if (args.Node is { IsExpanded: true, Tag: string path })
                LoadChildDirectories(args.Node, path);
        };

        fileTable.RowActivated += (sender, rowIndex) =>
        {
            if (rowIndex >= 0 && rowIndex < fileTable.RowCount)
            {
                var row = fileTable.Rows[rowIndex];
                if (row.Tag is string filePath && Directory.Exists(filePath))
                {
                    currentDir = filePath;
                    pathBar.SetContent(new List<string> { $"[bold] {filePath}[/]" });
                    PopulateFileTable(fileTable, filePath);
                    statusBar.SetContent(new List<string> { FormatStatusText(filePath, fileTable.RowCount) });
                }
            }
        };

        var grid = Controls.HorizontalGrid()
            .Column(col => col.Width(TreeColumnWidth).Add(scrollPanel))
            .Column(col => col.Flex().Add(fileTable))
            .WithSplitterAfter(0)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        return new WindowBuilder(windowSystem)
            .WithTitle("File Manager")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .AddControls(pathBar, grid, statusBar)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    windowSystem.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
                else if (e.KeyInfo.Key == ConsoleKey.Backspace)
                {
                    var parent = Path.GetDirectoryName(currentDir);
                    if (parent != null && Directory.Exists(parent))
                    {
                        currentDir = parent;
                        pathBar.SetContent(new List<string> { $"[bold] {parent}[/]" });
                        PopulateFileTable(fileTable, parent);
                        statusBar.SetContent(new List<string> { FormatStatusText(parent, fileTable.RowCount) });
                    }
                    e.Handled = true;
                }
            })
            .Build();
    }

    private static string FormatStatusText(string dirPath, int fileCount)
    {
        return $"[dim]{dirPath} - {fileCount} item{(fileCount == 1 ? "" : "s")}[/]";
    }

    private static TreeControl BuildDirectoryTree(string rootPath)
    {
        var builder = Controls.Tree()
            .WithGuide(TreeGuide.Line)
            .WithName("dirTree")
            .WithVerticalAlignment(VerticalAlignment.Fill);

        var rootName = Path.GetFileName(rootPath);
        if (string.IsNullOrEmpty(rootName))
            rootName = rootPath;

        var rootNode = builder.AddRootNode($"[cyan]{rootName}[/]");
        rootNode.Tag = rootPath;
        rootNode.IsExpanded = true;

        AddLazyChildren(rootNode, rootPath);

        return builder.Build();
    }

    private static void AddLazyChildren(TreeNode parentNode, string dirPath)
    {
        try
        {
            var dirs = Directory.GetDirectories(dirPath)
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                .Take(MaxDirectoryEntries);

            foreach (var dir in dirs)
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith('.'))
                    continue;

                var node = parentNode.AddChild($"[cyan]{name}[/]");
                node.Tag = dir;
                node.IsExpanded = false;

                if (FileOperations.HasSubdirectories(dir))
                    node.AddChild(LazyPlaceholder);
            }
        }
        catch
        {
            parentNode.AddChild("[red]Access denied[/]");
        }
    }

    private static void LoadChildDirectories(TreeNode parentNode, string dirPath)
    {
        if (parentNode.Children.Count > 0 &&
            !(parentNode.Children.Count == 1 && parentNode.Children[0].Text == LazyPlaceholder))
            return;

        parentNode.ClearChildren();
        AddLazyChildren(parentNode, dirPath);
    }

    private static void PopulateFileTable(TableControl fileTable, string dirPath)
    {
        fileTable.ClearRows();

        try
        {
            // Add directories first
            var dirs = Directory.GetDirectories(dirPath)
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                .Take(MaxDirectoryEntries);

            foreach (var dir in dirs)
            {
                var name = Path.GetFileName(dir);
                var info = new DirectoryInfo(dir);
                var dateStr = info.LastWriteTime.ToString(DateFormat);
                fileTable.AddRow(new TableRow($"[cyan]{name}/[/]", "[dim]DIR[/]", $"[dim]{dateStr}[/]") { Tag = dir });
            }

            // Add files
            var files = Directory.GetFiles(dirPath)
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .Take(MaxFileEntries);

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                var icon = FileOperations.GetFileIcon(info.Extension);
                var size = FileOperations.FormatFileSize(info.Length);
                var dateStr = info.LastWriteTime.ToString(DateFormat);
                fileTable.AddRow(new TableRow($"{icon} {info.Name}", size, $"[dim]{dateStr}[/]") { Tag = file });
            }

            if (fileTable.RowCount == 0)
                fileTable.AddRow("[dim](empty directory)[/]", "", "");
        }
        catch
        {
            fileTable.AddRow("[red]Access denied[/]", "", "");
        }
    }
}
