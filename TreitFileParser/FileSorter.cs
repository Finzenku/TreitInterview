using System.Runtime.InteropServices;

namespace TreitFileParser
{
    public static class FileSorter<T> where T : struct, IComparable<T>
    {
        private static int Size;

        static FileSorter()
        {
            Size = Marshal.SizeOf<T>();
        }

        public static void SortFile(string filePath)
        {
            T[] values;

            using (FileStream fileStream = File.OpenRead(filePath))
            {
                values = new T[fileStream.Length / Size];
                Span<byte> file1Buffer = new byte[Size];
                for (int i = 0; i< values.Length; i++)
                {
                    fileStream.Read(file1Buffer);
                    values[i] = MemoryMarshal.Cast<byte, T>(file1Buffer)[0];
                }
            }

            Array.Sort(values);
            using (FileStream fs = File.Create(filePath))
                fs.Write(MemoryMarshal.Cast<T, byte>(values));
        }

        public static string SortReallyBigFile(string filePath, int chunkSizeInMB)
        {
            FileInfo fileInfo = new(filePath);
            string newFile = @$"{fileInfo.DirectoryName!}\Sorted{fileInfo.Name}";
            string[] splitFiles = FileSplitter.Split(filePath, chunkSizeInMB, "Splits", true);
            foreach (string file in splitFiles)
            {
                FileSorter<T>.SortFile(file);
            }

            while (splitFiles.Length > 2)
                splitFiles = FileZipperMerger<T>.MergeFiles(splitFiles, Path.Combine(fileInfo.DirectoryName!, "Splits"));

            if (splitFiles.Length > 1)
            {
                FileZipperMerger<T>.MergeFiles(splitFiles[0], splitFiles[1], newFile);
                File.Delete(splitFiles[1]);
            }
            else
            {
                File.Copy(splitFiles[0], newFile, true);
            }
            File.Delete(splitFiles[0]);
            return newFile;
        }
    }
}
