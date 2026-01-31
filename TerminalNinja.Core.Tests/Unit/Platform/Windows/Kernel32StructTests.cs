using System.Runtime.InteropServices;
using TerminalNinja.Core.Platform.Windows;

namespace TerminalNinja.Core.Tests.Unit.Platform.Windows;

/// <summary>
/// Tests to verify P/Invoke struct layouts match Windows API expectations.
/// </summary>
public class Kernel32StructTests
{
    [Test]
    public async Task COORD_HasCorrectSize()
    {
        // COORD should be 4 bytes (2 shorts)
        var size = Marshal.SizeOf<Kernel32.COORD>();
        await Assert.That(size).IsEqualTo(4);
    }

    [Test]
    public async Task KEY_EVENT_RECORD_HasCorrectSize()
    {
        // KEY_EVENT_RECORD should be 16 bytes
        // bool (4) + ushort (2) + ushort (2) + ushort (2) + char (2) + uint (4) = 16
        var size = Marshal.SizeOf<Kernel32.KEY_EVENT_RECORD>();
        await Assert.That(size).IsEqualTo(16);
    }

    [Test]
    public async Task MOUSE_EVENT_RECORD_HasCorrectSize()
    {
        // MOUSE_EVENT_RECORD should be 16 bytes
        // COORD (4) + uint (4) + uint (4) + uint (4) = 16
        var size = Marshal.SizeOf<Kernel32.MOUSE_EVENT_RECORD>();
        await Assert.That(size).IsEqualTo(16);
    }

    [Test]
    public async Task WINDOW_BUFFER_SIZE_RECORD_HasCorrectSize()
    {
        // WINDOW_BUFFER_SIZE_RECORD should be 4 bytes (COORD)
        var size = Marshal.SizeOf<Kernel32.WINDOW_BUFFER_SIZE_RECORD>();
        await Assert.That(size).IsEqualTo(4);
    }

    [Test]
    public async Task INPUT_RECORD_HasCorrectSize()
    {
        // INPUT_RECORD should be 20 bytes
        // ushort EventType (2) + padding (2) + union (16) = 20
        var size = Marshal.SizeOf<Kernel32.INPUT_RECORD>();
        await Assert.That(size).IsEqualTo(20);
    }

    [Test]
    public async Task INPUT_RECORD_EventType_HasCorrectOffset()
    {
        var offset = Marshal.OffsetOf<Kernel32.INPUT_RECORD>(nameof(Kernel32.INPUT_RECORD.EventType));
        await Assert.That(offset.ToInt32()).IsEqualTo(0);
    }

    [Test]
    public async Task INPUT_RECORD_KeyEvent_HasCorrectOffset()
    {
        // All union members should start at offset 4
        var offset = Marshal.OffsetOf<Kernel32.INPUT_RECORD>(nameof(Kernel32.INPUT_RECORD.KeyEvent));
        await Assert.That(offset.ToInt32()).IsEqualTo(4);
    }

    [Test]
    public async Task INPUT_RECORD_MouseEvent_HasCorrectOffset()
    {
        // All union members should start at offset 4
        var offset = Marshal.OffsetOf<Kernel32.INPUT_RECORD>(nameof(Kernel32.INPUT_RECORD.MouseEvent));
        await Assert.That(offset.ToInt32()).IsEqualTo(4);
    }

    [Test]
    public async Task INPUT_RECORD_WindowBufferSizeEvent_HasCorrectOffset()
    {
        // All union members should start at offset 4
        var offset = Marshal.OffsetOf<Kernel32.INPUT_RECORD>(nameof(Kernel32.INPUT_RECORD.WindowBufferSizeEvent));
        await Assert.That(offset.ToInt32()).IsEqualTo(4);
    }

    [Test]
    public async Task KEY_EVENT_RECORD_Fields_HaveCorrectOffsets()
    {
        // Verify field offsets match Windows API expectations
        var bKeyDownOffset = Marshal.OffsetOf<Kernel32.KEY_EVENT_RECORD>(nameof(Kernel32.KEY_EVENT_RECORD.bKeyDown));
        await Assert.That(bKeyDownOffset.ToInt32()).IsEqualTo(0);

        var wRepeatCountOffset = Marshal.OffsetOf<Kernel32.KEY_EVENT_RECORD>(nameof(Kernel32.KEY_EVENT_RECORD.wRepeatCount));
        await Assert.That(wRepeatCountOffset.ToInt32()).IsEqualTo(4);

        var wVirtualKeyCodeOffset = Marshal.OffsetOf<Kernel32.KEY_EVENT_RECORD>(nameof(Kernel32.KEY_EVENT_RECORD.wVirtualKeyCode));
        await Assert.That(wVirtualKeyCodeOffset.ToInt32()).IsEqualTo(6);

        var wVirtualScanCodeOffset = Marshal.OffsetOf<Kernel32.KEY_EVENT_RECORD>(nameof(Kernel32.KEY_EVENT_RECORD.wVirtualScanCode));
        await Assert.That(wVirtualScanCodeOffset.ToInt32()).IsEqualTo(8);

        var unicodeCharOffset = Marshal.OffsetOf<Kernel32.KEY_EVENT_RECORD>(nameof(Kernel32.KEY_EVENT_RECORD.UnicodeChar));
        await Assert.That(unicodeCharOffset.ToInt32()).IsEqualTo(10);

        var dwControlKeyStateOffset = Marshal.OffsetOf<Kernel32.KEY_EVENT_RECORD>(nameof(Kernel32.KEY_EVENT_RECORD.dwControlKeyState));
        await Assert.That(dwControlKeyStateOffset.ToInt32()).IsEqualTo(12);
    }

    [Test]
    public async Task EventType_Constants_HaveCorrectValues()
    {
        ushort keyEvent = Kernel32.KEY_EVENT;
        ushort mouseEvent = Kernel32.MOUSE_EVENT;
        ushort windowBufferSizeEvent = Kernel32.WINDOW_BUFFER_SIZE_EVENT;
        ushort menuEvent = Kernel32.MENU_EVENT;
        ushort focusEvent = Kernel32.FOCUS_EVENT;

        await Assert.That(keyEvent).IsEqualTo((ushort)0x0001);
        await Assert.That(mouseEvent).IsEqualTo((ushort)0x0002);
        await Assert.That(windowBufferSizeEvent).IsEqualTo((ushort)0x0004);
        await Assert.That(menuEvent).IsEqualTo((ushort)0x0008);
        await Assert.That(focusEvent).IsEqualTo((ushort)0x0010);
    }
}
