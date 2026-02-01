using TerminalNinja.Core.Buffers;
using TerminalNinja.Core.Primitives;

// Create a small buffer and render some content
var buffer = new CellBuffer(40, 15);

// Draw a header
var header = "Terminal Dump Demo";
for (int i = 0; i < header.Length && i < 40; i++)
{
    buffer.SetChar(i + 10, 2, header[i], Color.Cyan, Color.Black);
}

// Draw a box
for (int x = 5; x < 35; x++)
{
    buffer.SetChar(x, 5, '═', Color.Yellow, Color.Black);
    buffer.SetChar(x, 10, '═', Color.Yellow, Color.Black);
}
for (int y = 6; y < 10; y++)
{
    buffer.SetChar(5, y, '║', Color.Yellow, Color.Black);
    buffer.SetChar(34, y, '║', Color.Yellow, Color.Black);
}
buffer.SetChar(5, 5, '╔', Color.Yellow, Color.Black);
buffer.SetChar(34, 5, '╗', Color.Yellow, Color.Black);
buffer.SetChar(5, 10, '╚', Color.Yellow, Color.Black);
buffer.SetChar(34, 10, '╝', Color.Yellow, Color.Black);

// Draw text inside
var message = "Hello from TerminalNinja!";
for (int i = 0; i < message.Length; i++)
{
    buffer.SetChar(i + 7, 7, message[i], Color.White, Color.Black);
}

// Dump to file
var dump = buffer.DumpToString();
File.WriteAllText("example_dump.txt", dump);

Console.WriteLine("Created example_dump.txt");
Console.WriteLine("Content:");
Console.WriteLine(dump);
