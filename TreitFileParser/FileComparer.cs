using System.Runtime.InteropServices;

namespace TreitFileParser
{
    public static class FileComparer<T> where T: struct, IComparable<T>
    {
        private static int Size;

        static FileComparer()
        {
            Size = Marshal.SizeOf<T>();
        }

        public static T[] GetDistinctValues(string file1Path, string file2Path)
        {
            HashSet<T> hashSet = new();

            using FileStream file1Stream = File.OpenRead(file1Path);
            using FileStream file2Stream = File.OpenRead(file2Path);

            Span<byte> file1Buffer = new byte[Size];
            Span<byte> file2Buffer = new byte[Size];

            // Preload our first values because I don't want to think about do-while loops here
            // TODO: This needs error checking before casting
            file1Stream.Read(file1Buffer);
            T val1 = MemoryMarshal.Cast<byte, T>(file1Buffer)[0];
            file2Stream.Read(file2Buffer);
            T val2 = MemoryMarshal.Cast<byte, T>(file2Buffer)[0];


            while (file1Stream.Position < file1Stream.Length && file2Stream.Position < file2Stream.Length)
            {
                if (val1.CompareTo(val2) == 0)
                {
                    hashSet.Add(val1);
                    if (file1Stream.Read(file1Buffer) < Size)
                    {
                        break;
                    }
                    val1 = MemoryMarshal.Cast<byte, T>(file1Buffer)[0];
                    if (file2Stream.Read(file2Buffer) < Size)
                    {
                        break;
                    }
                    val2 = MemoryMarshal.Cast<byte, T>(file2Buffer)[0];
                }
                else if (val1.CompareTo(val2) < 0)
                {
                    // If the file didn't have enough bytes left to create a full T value, it's done reading
                    if (file1Stream.Read(file1Buffer) < Size)
                    {
                        break;
                    }
                    val1 = MemoryMarshal.Cast<byte, T>(file1Buffer)[0];
                }
                else
                {
                    if (file2Stream.Read(file2Buffer) < Size)
                    {
                        break;
                    }
                    val2 = MemoryMarshal.Cast<byte, T>(file2Buffer)[0];
                }
            }

            // One of the streams has reached the end of the file, check the rest of the other comapred to the last value
            // At most, we should find one more match
            while (file1Stream.Position < file1Stream.Length)
            {
                if (val1.CompareTo(val2) == 0)
                {
                    hashSet.Add(val1);
                    break;
                }
                else
                {
                    if (file1Stream.Read(file1Buffer) < Size)
                    {
                        break;
                    }
                    val1 = MemoryMarshal.Cast<byte, T>(file1Buffer)[0];
                }
            }
            while(file2Stream.Position < file2Stream.Length)
            {
                if (val1.CompareTo(val2) == 0)
                {
                    hashSet.Add(val1);
                    break;
                }
                else
                {
                    if (file2Stream.Read(file2Buffer) < Size)
                    {
                        break;
                    }
                    val2 = MemoryMarshal.Cast<byte, T>(file2Buffer)[0];
                }
            }

            // One last check for the road...
            if (val1.CompareTo(val2) == 0)
                hashSet.Add(val1);

            return hashSet.ToArray();
        }

    }

    public static class FileComparer
    {
        public static bool AreIdentical(string file1Path, string file2Path)
        {
            using FileStream file1Stream = File.OpenRead(file1Path);
            using FileStream file2Stream = File.OpenRead(file2Path);

            if (file1Stream.Length != file2Stream.Length)
                return false;

            byte[] buffer1 = new byte[1024];
            byte[] buffer2 = new byte[1024];
            while (file1Stream.Position < file1Stream.Length && file2Stream.Position < file2Stream.Length)
            {
                file1Stream.Read(buffer1);
                file2Stream.Read(buffer2);
                if (!Enumerable.SequenceEqual(buffer1, buffer2))
                    return false;
            }
            if (file1Stream.Position != file2Stream.Position)
                return false;
            return true;
        }
    }
}
