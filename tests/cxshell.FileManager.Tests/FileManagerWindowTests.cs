namespace cxshell.FileManager.Tests;

public class FileOperationTests : IDisposable
{
    private readonly string _tempDir;

    public FileOperationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"fm-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void FormatFileSize_Bytes()
    {
        Assert.Equal("500 B", FileOperations.FormatFileSize(500));
    }

    [Fact]
    public void FormatFileSize_Kilobytes()
    {
        Assert.Equal("1.0 KB", FileOperations.FormatFileSize(1024));
    }

    [Fact]
    public void FormatFileSize_Megabytes()
    {
        Assert.Equal("1.0 MB", FileOperations.FormatFileSize(1024 * 1024));
    }

    [Fact]
    public void GetFileIcon_KnownExtensions()
    {
        Assert.NotEmpty(FileOperations.GetFileIcon(".cs"));
        Assert.NotEmpty(FileOperations.GetFileIcon(".txt"));
        Assert.NotEmpty(FileOperations.GetFileIcon(".png"));
    }

    [Fact]
    public void DeleteFile_RemovesFile()
    {
        var path = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(path, "hello");
        FileOperations.DeleteFile(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void RenameFile_ChangesName()
    {
        var oldPath = Path.Combine(_tempDir, "old.txt");
        var newPath = Path.Combine(_tempDir, "new.txt");
        File.WriteAllText(oldPath, "hello");
        FileOperations.RenameFile(oldPath, "new.txt");
        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(newPath));
    }
}
