namespace TerminalNinja.IO;

/// <summary>
/// Default filesystem implementation wrapping <see cref="System.IO"/>.
/// Handles access-denied errors gracefully by returning empty collections.
/// </summary>
public sealed class RealFileSystem : IFileSystem
{
    public IEnumerable<string> GetDirectories(string path)
    {
        try { return Directory.EnumerateDirectories(path).Select(Path.GetFileName).Where(n => n != null)!; }
        catch { return []; }
    }

    public IEnumerable<string> GetFiles(string path)
    {
        try { return Directory.EnumerateFiles(path).Select(Path.GetFileName).Where(n => n != null)!; }
        catch { return []; }
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string GetFileName(string path) => Path.GetFileName(path);

    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);

    public string GetCurrentDirectory() => Directory.GetCurrentDirectory();
}
