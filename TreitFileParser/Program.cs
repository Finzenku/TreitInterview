using TreitFileParser;

const string inputFolder = @"M:\TreitFileGenerator\";
const string outputFolder = inputFolder;
const int bin1Seed = 5185;
const int bin2Seed = 518;
const int SplitSizeInMB = 1024;


string bin1Path = $"{inputFolder}{bin1Seed}.bin";
string bin2Path = $"{inputFolder}{bin2Seed}.bin";

bin1Path = FileSorter<int>.SortReallyBigFile(bin1Path, SplitSizeInMB);
bin2Path = FileSorter<int>.SortReallyBigFile(bin2Path, SplitSizeInMB);

int[] distinctValues = FileComparer<int>.GetDistinctValues(bin1Path, bin2Path);
Console.WriteLine(distinctValues.Length);