using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace TreitFileParser
{
    public static class FileSorter<T> where T : struct, IComparable<T>
    {
        public static void SortFile(string filePath)
        {
            T[] values;
            using (var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read))
            using (var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
            {
                int fileSize = (int)accessor.Capacity;
                int numberToRead = fileSize / Marshal.SizeOf<T>();

                values = new T[numberToRead];
                accessor.ReadArray(0, values, 0, numberToRead);
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
