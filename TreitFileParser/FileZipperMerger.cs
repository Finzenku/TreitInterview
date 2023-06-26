using System.Runtime.InteropServices;

namespace TreitFileParser
{
    public static class FileZipperMerger<T> where T : struct, IComparable<T>
    {
        private static int Size;

        static FileZipperMerger()
        {
            Size = Marshal.SizeOf<T>();
        }

        public static void MergeFiles(string file1Path, string file2Path, string outputPath)
        {
            using FileStream file1Stream = File.OpenRead(file1Path);
            using FileStream file2Stream = File.OpenRead(file2Path);
            using FileStream outputStream = File.Create(outputPath);

            Span<byte> file1Buffer = new byte[Size];
            Span<byte> file2Buffer = new byte[Size];
            bool stream1Finished = false;
            bool stream2Finished = false;

            // Preload our first values because I don't want to think about do-while loops here
            // TODO: This needs error checking before casting
            file1Stream.Read(file1Buffer);
            T val1 = MemoryMarshal.Cast<byte, T>(file1Buffer)[0];
            file2Stream.Read(file2Buffer);
            T val2 = MemoryMarshal.Cast<byte, T>(file2Buffer)[0];


            while (file1Stream.Position < file1Stream.Length && file2Stream.Position < file2Stream.Length)
            {
                if (val1.CompareTo(val2) <= 0)
                {
                    outputStream.Write(file1Buffer);
                    // If the file didn't have enough bytes left to create a full T value, it's done reading
                    if (file1Stream.Read(file1Buffer) < Size)
                    {
                        stream1Finished = true;
                        break;
                    }
                    val1 = MemoryMarshal.Cast<byte, T>(file1Buffer)[0];
                }
                else
                {
                    outputStream.Write(file2Buffer);
                    if (file2Stream.Read(file2Buffer) < Size)
                    {
                        stream2Finished = true;
                        break;
                    }
                    val2 = MemoryMarshal.Cast<byte, T>(file2Buffer)[0];
                }
            }

            // One of the files will be at the last value it has, so we need to keep reading the other
            // The one that finished won't enter the while loop
            while (file1Stream.Position < file1Stream.Length)
            {
                if(stream2Finished || val1.CompareTo(val2) <= 0)
                {
                    outputStream.Write(file1Buffer);
                    if (file1Stream.Read(file1Buffer) < Size)
                    {
                        stream1Finished = true;
                        break;
                    }
                    val1 = MemoryMarshal.Cast<byte, T>(file1Buffer)[0];
                }
                else
                {
                    outputStream.Write(file2Buffer);
                    stream2Finished = true;
                }
            }
            while (file2Stream.Position < file2Stream.Length)
            {
                if (stream1Finished || val2.CompareTo(val1) < 0)
                {
                    outputStream.Write(file2Buffer);
                    if (file2Stream.Read(file2Buffer) < Size)
                    {
                        stream2Finished = true;
                        break;
                    }
                    val2 = MemoryMarshal.Cast<byte, T>(file2Buffer)[0];
                }
                else
                {
                    outputStream.Write(file1Buffer);
                    stream1Finished = true;
                }
            }

            // If both streams are at their last values, we need to check them one last time
            if (!stream1Finished && !stream2Finished)
            {
                if (val1.CompareTo(val2) <= 0)
                {
                    outputStream.Write(file1Buffer);
                    stream1Finished = true;
                }
                else
                {
                    outputStream.Write(file2Buffer);
                    stream2Finished = true;
                }
            }

            // One of the streams has been waiting to write its biggest value
            if (!stream1Finished)
                outputStream.Write(file1Buffer);
            if (!stream2Finished)
                outputStream.Write(file2Buffer);
        }

        public static string[] MergeFiles(string[] filePathsToMerge, string outputFolder)
        {
            int mergePairs = filePathsToMerge.Length / 2;
            bool spareSplitFile = filePathsToMerge.Length % 2 == 1;
            string[] mergeFiles = new string[mergePairs];

            if (filePathsToMerge.Length > 1)
            {
                for (int i = 0; i < mergePairs; i++)
                {
                    FileInfo file1 = new FileInfo(filePathsToMerge[i]);
                    FileInfo file2 = new FileInfo(filePathsToMerge[i+mergePairs]);
                    mergeFiles[i] = MergeAndDeleteFiles(file1, file2, outputFolder);
                }
                if (spareSplitFile)
                {
                    FileInfo file1 = new FileInfo(mergeFiles[^1]);
                    FileInfo file2 = new FileInfo(filePathsToMerge[^1]);
                    mergeFiles[^1] = MergeAndDeleteFiles(file1, file2, outputFolder);
                }
            }

            return mergeFiles;
        }

        private static string MergeAndDeleteFiles(FileInfo file1, FileInfo file2, string outputFolder)
        {
            uint hash = (uint)$"{file1.Name}{file2.Name}".GetHashCode();
            string fileName = Path.Combine(outputFolder, $"{hash:X8}.bin");
            FileZipperMerger<T>.MergeFiles(file1.FullName, file2.FullName, fileName);
            File.Delete(file1.FullName);
            File.Delete(file2.FullName);
            return fileName;
        }
    }
}
