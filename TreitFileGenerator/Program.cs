// I'm lazy and run things straight out of Visual Studio
args = new string[] { @"M:\TreitFileGenerator\", "1073741824", "518" };

if (args.Length < 2)
{
    Console.WriteLine($"Usage:");
    Console.WriteLine($"Program.exe <filepath> <size> <seed>");
    return;
}

var filepath = args[0];
if (!long.TryParse(args[1], out var size))
{
    Console.WriteLine("Invalid input.");
    return;
}

if (!int.TryParse(args[2], out var seed))
{
    Console.WriteLine("Invalid input.");
    return;
}

var r = new Random(seed);
using var fs = new FileStream($"{filepath}{seed}.bin", FileMode.Create);

var buff = new byte[4];

var buffer = size < (1024 * 1024 * 1024) ? new byte[size] : new byte[1024 * 1024 * 1024];
var totalWritten = 0L;

while (totalWritten < size)
{
    r.NextBytes(buffer);
    long amountToWrite = size - totalWritten;
    fs.Write(buffer, 0, (int)Math.Min(amountToWrite, buffer.Length));
    totalWritten += buffer.Length;
}
Console.WriteLine($"{totalWritten} bytes written.");