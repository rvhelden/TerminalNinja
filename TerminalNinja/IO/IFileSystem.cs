namespace TerminalNinja.IO;

/// <summary>
/// Abstraction over filesystem operations for <see cref="Controls.FilePicker"/>
/// and <see cref="Controls.FolderPicker"/>. Allows substituting a mock
/// implementation for testing without real filesystem access.
/// </summary>
public interface IFileSystem
{
    /// <summary>Gets subdirectory names in the specified path.</summary>
    IEnumerable<string> GetDirectories(string path);

    /// <summary>Gets file names in the specified path.</summary>
    IEnumerable<string> GetFiles(string path);

    /// <summary>Returns whether a directory exists at the given path.</summary>
    bool DirectoryExists(string path);

    /// <summary>Gets the file name from a full path.</summary>
    string GetFileName(string path);

    /// <summary>Gets the parent directory path.</summary>
    string? GetDirectoryName(string path);

    /// <summary>Gets the current working directory.</summary>
    string GetCurrentDirectory();
}
