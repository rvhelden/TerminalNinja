using System.Runtime.InteropServices;

namespace TerminalNinja.Terminal.Native;

/// <summary>
/// Windows ConPTY + supporting kernel32.dll P/Invoke surface. Uses [LibraryImport] so the
/// marshalling is source-generated (AOT-safe). With <c>DisableRuntimeMarshalling</c> at the
/// assembly level, all types here are blittable — BOOL is <c>int</c> (1 = true, 0 = false),
/// strings are marshalled via <c>StringMarshalling.Utf16</c> by the LibraryImport generator.
/// </summary>
/// <remarks>
/// All entry points live in kernel32.dll. Calling these on a non-Windows platform raises
/// <see cref="DllNotFoundException"/> at runtime — <see cref="TerminalBackend.Create"/>
/// routes non-Windows requests to the POSIX backend.
/// </remarks>
internal static partial class ConPtyNative
{
    private const string Lib = "kernel32.dll";

    // STARTUPINFO flags
    public const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;

    // PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE_HANDLE — passed to UpdateProcThreadAttribute so the
    // child process inherits our pseudoconsole rather than the parent's normal console.
    // Value matches ProcThreadAttributeValue(22, FALSE, TRUE, FALSE) = 0x00020016.
    public static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE_HANDLE = 0x00020016;

    // WaitForSingleObject return values.
    public const uint WAIT_OBJECT_0 = 0x00000000;
    public const uint WAIT_TIMEOUT = 0x00000102;
    public const uint INFINITE = 0xFFFFFFFF;

    // ReadFile/WriteFile error codes worth recognising on the read loop's exit path.
    public const int ERROR_BROKEN_PIPE = 109;
    public const int ERROR_OPERATION_ABORTED = 995;

    [StructLayout(LayoutKind.Sequential)]
    public struct COORD
    {
        public short X;
        public short Y;
    }

    /// <summary>
    /// Win32 SECURITY_ATTRIBUTES. <c>bInheritHandle</c> is BOOL (int) per the C declaration;
    /// 1 = inheritable, 0 = not inheritable.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SECURITY_ATTRIBUTES
    {
        public uint nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFOW
    {
        public uint cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public uint dwX, dwY;
        public uint dwXSize, dwYSize;
        public uint dwXCountChars, dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public ushort wShowWindow;
        public ushort cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFOEXW
    {
        public STARTUPINFOW StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [LibraryImport(Lib, SetLastError = true)]
    public static partial int CreatePipe(
        out IntPtr hReadPipe,
        out IntPtr hWritePipe,
        ref SECURITY_ATTRIBUTES lpPipeAttributes,
        uint nSize);

    [LibraryImport(Lib, SetLastError = true)]
    public static partial int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

    [LibraryImport(Lib, SetLastError = true)]
    public static partial int ResizePseudoConsole(IntPtr hPC, COORD size);

    [LibraryImport(Lib, SetLastError = true)]
    public static partial void ClosePseudoConsole(IntPtr hPC);

    [LibraryImport(Lib, SetLastError = true)]
    public static partial int CloseHandle(IntPtr hObject);

    [LibraryImport(Lib, SetLastError = true)]
    public static partial int InitializeProcThreadAttributeList(
        IntPtr lpAttributeList,
        int dwAttributeCount,
        int dwFlags,
        ref IntPtr lpSize);

    [LibraryImport(Lib, SetLastError = true)]
    public static partial int UpdateProcThreadAttribute(
        IntPtr lpAttributeList,
        uint dwFlags,
        IntPtr Attribute,
        IntPtr lpValue,
        IntPtr cbSize,
        IntPtr lpPreviousValue,
        IntPtr lpReturnSize);

    [LibraryImport(Lib)]
    public static partial void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [LibraryImport(Lib, SetLastError = true, EntryPoint = "CreateProcessW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CreateProcessW(
        string? lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        int bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFOEXW lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [LibraryImport(Lib, SetLastError = true)]
    public static partial int ReadFile(
        IntPtr hFile,
        IntPtr lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [LibraryImport(Lib, SetLastError = true)]
    public static partial int WriteFile(
        IntPtr hFile,
        IntPtr lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        IntPtr lpOverlapped);

    [LibraryImport(Lib, SetLastError = true)]
    public static partial uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [LibraryImport(Lib, SetLastError = true)]
    public static partial int GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [LibraryImport(Lib, SetLastError = true)]
    public static partial int TerminateProcess(IntPtr hProcess, uint uExitCode);
}
