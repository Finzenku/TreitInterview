namespace TreitFileParser
{
    public static class FileSplitter
    {
        const int OneMB = 1024*1024;

        /// <summary>
        /// Splits a file into chunks
        /// </summary>
        /// <param name="fileToSplitPath">The absolute path to the file that you want to split</param>
        /// <param name="splitSizeInMB">The maximum size of the split files in MB (1024 KB)</param>
        /// <param name="outputFilesFolder">The folder you want the output files writen to</param>
        /// <param name="useRelativePath">Determines whether the outputFilesFolder is a relative path from your fileToSplitPath's directory or an absolute path, default: true</param>
        /// <returns>An array of file path strings for the newly created files</returns>
        public static string[] Split(string fileToSplitPath, int splitSizeInMB, string outputFilesFolder, bool useRelativePath = true)
        {
            int chunkSize = splitSizeInMB * OneMB;
            FileInfo fileInfo = new FileInfo(fileToSplitPath);
            string fileName = fileInfo.Name.Substring(0, fileInfo.Name.IndexOf(fileInfo.Extension));
            bool spareChunk = fileInfo.Length % chunkSize > 0;
            long chunkCount = fileInfo.Length / chunkSize + (spareChunk ? 1 : 0);

            if (chunkCount > int.MaxValue)
                throw new InvalidOperationException($"The file is too large to be split into {splitSizeInMB} MB chunks. Consider using a larger SplitSizeInMB");

            string outputFolder = CheckAndCreateOutputFolder(fileToSplitPath, outputFilesFolder, useRelativePath);
            string[] outputFiles = new string[chunkCount];
            Span<byte> buffer = new byte[chunkSize];
            using FileStream fsSplit = File.OpenRead(fileToSplitPath);
            for (int i = 0; i < chunkCount; i++)
            {
                int bytesRead = fsSplit.Read(buffer);
                outputFiles[i] = $"{outputFolder}/{fileName}.{i+1}{fileInfo.Extension}";
                using FileStream fsOut = File.Create(outputFiles[i]);
                fsOut.Write(buffer.Slice(0, bytesRead));
            }
            return outputFiles;
        }

        private static string CheckAndCreateOutputFolder(string fileToSplitPath, string outputPath, bool useRelativePath)
        {
            string absOutput = outputPath;

            if (useRelativePath)
            {
                FileInfo fileInfo = new FileInfo(fileToSplitPath);
                if (fileInfo.DirectoryName is not null)
                {
                    absOutput = Path.Combine(fileInfo.DirectoryName, outputPath);
                }
            }

            Directory.CreateDirectory(absOutput);
            return absOutput;
        }
    }
}
