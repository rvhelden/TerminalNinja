using System;

namespace TerminalNinja.Terminal;

/// <summary>
/// Factory + base utility class for <see cref="ITerminalBackend"/>. <see cref="Create"/>
/// picks the right concrete backend for the current OS; consumer code stays platform-neutral.
/// </summary>
public static class TerminalBackend
{
    /// <summary>
    /// Creates the platform-appropriate <see cref="ITerminalBackend"/>. The returned backend
    /// has been constructed but not yet started — call <see cref="ITerminalBackend.StartAsync"/>
    /// before <see cref="ITerminalBackend.WriteAsync"/> / <see cref="ITerminalBackend.ResizeAsync"/>.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown on platforms where no concrete backend is available (e.g. browser / wasm).
    /// </exception>
    public static ITerminalBackend Create(TerminalBackendOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (OperatingSystem.IsWindows())
        {
            return new ConPtyTerminalBackend(options);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
        {
            return new UnixTerminalBackend(options);
        }

        throw new PlatformNotSupportedException(
            $"No ITerminalBackend implementation available for this OS ({Environment.OSVersion}). " +
            "Use NullTerminalBackend in tests or a remote backend (e.g. Docker exec) for non-PTY scenarios.");
    }
}
