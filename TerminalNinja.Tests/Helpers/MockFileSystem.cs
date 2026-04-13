using TerminalNinja.IO;

namespace TerminalNinja.Tests.Helpers;

/// <summary>
/// In-memory filesystem for testing FilePicker and FolderPicker without real disk access.
/// </summary>
public class MockFileSystem : IFileSystem
{
    private readonly Dictionary<string, List<string>> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _currentDirectory;

    public MockFileSystem(string currentDirectory = "/home/user")
    {
        _currentDirectory = currentDirectory;
    }

    public void AddDirectory(string parentPath, string dirName)
    {
        if (!_directories.ContainsKey(parentPath))
            _directories[parentPath] = [];
        _directories[parentPath].Add(dirName);

        // Ensure the child path also exists as a key
        var childPath = Path.Combine(parentPath, dirName);
        if (!_directories.ContainsKey(childPath))
            _directories[childPath] = [];
    }

    public void AddFile(string parentPath, string fileName)
    {
        if (!_files.ContainsKey(parentPath))
            _files[parentPath] = [];
        _files[parentPath].Add(fileName);
    }

    public IEnumerable<string> GetDirectories(string path) =>
        _directories.TryGetValue(path, out var dirs) ? dirs : [];

    public IEnumerable<string> GetFiles(string path) =>
        _files.TryGetValue(path, out var files) ? files : [];

    public bool DirectoryExists(string path) =>
        _directories.ContainsKey(path) || path == _currentDirectory;

    public string GetFileName(string path) => Path.GetFileName(path);

    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);

    public string GetCurrentDirectory() => _currentDirectory;
}
