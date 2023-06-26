using System.IO.MemoryMappedFiles;

namespace TreitFileParser
{
    /*

    This is an interview question posed by Mike Treit on his blog: https://treit.github.io/programming,/interviewing/2023/02/03/InterviewQuestion.html
    "Your task is to produce an output file containing the distinct set of integers that are shared between the two input files."
    (I used uint just because I like seeing a big range of positive numbers more than a big range of positive and negative numbers)

    My original approach was to use BinaryReader to read the uints from the file and store them in a HashSet to remove any duplicates right away.
    This took a huge amount of memory because of the overhead of using a HashSet and the fact that there were barely any duplicates per file.
    While I was messing with this, I saw File.ReadAllBytes, tried to read the whole file, break it down into Memory<byte> chunks and parse the uints in each chunk.
    I thought I could try MemoryMarshal.Cast but I couldn't use Spans in async methods, which I really wanted to use to proccess both files at the same time.

    This wasn't going anywhere fast as I'm not super familiar with Memory<T> or Span<T>.
    I remembered using Buffer.BlockCopy in another project to copy an array of bytes to an array of shorts and figured I could apply it to uints as well, it worked!
    I didn't have each files values in a HashSet but realized that the final result is the only one that needed to have distinct values anyways.
    From there it was as simple as slapping a .Intersect on the two and I was off to the races.

    A quick chat with ChatGPT later and I realized that I lost the race, by a lot.
    Intersect was just way too slow on these large data sets. ChatGPT recommended either adding .AsParallel or using the "Two-Pointer method".
    I plugged both into my program and noticed immediate improvements in speed, with AsParallel being nearly three times faster on my i9 9900k.
    A quick look at the debug menu told me that AsParallel, while fast, was not the solution I was looking for. 
    It ate as much RAM as it could, and then some; even going as far as causing the debugger to stop updating the graphs.

    The Two-Pointer method, however, was still a large improvement over Intersect and more memory efficient to boot.
    I did notice that ChatGPT elected to use a List though, forgetting about the distinct restriction I mentioned earlier.
    I figured this could be solved by either using a HashSet instead of the List or using .Distinct before .ToArray, both seemed to very similar 
        (maybe .Distinct uses a HashSet in its implementation?)
    The downside to the Two-Pointer method is that you need the arrays to be sorted, which means that the result will be sorted too.
    I wasn't sure if this was important for the result but Mike just said he wants the distinct set, nothing about how they are arranged.
    I wrote a "pure" method that leaves the two arrays in their original state because some people might find that important.

    Once I had the main meat of the problem where I wanted it, with the Two-Pointer method using a HashSet, I bugged ChatGPT some more to improve my memory efficiency elsewhere.
    This is where I learned about MemoryMappedFile and how to read a file directly to the type I wanted.
    MemoryMappedFile was a tiny bit slower than File.ReadAllBytes but it saved an entire array allocation, so I'll take that for these big boys.

    Trying to save even more memory, I asked it about writing my sharedValues array to a new file without allocating another byte array to use WriteAllBytes.
    When it suggested using a BinaryWriter and looping over the array I thought, "Well duh, but isn't that slow?"
    The answer is no. No, it's not. It was actually faster.

    With all that, my big memory allocations went down to being just the two files in uint arrays and the uint array of their shared values.
    It'd be interesting to see an efficient way to compare the values in the two files without loading both into memory but I think this is the limit of my attention on this project.
    I also wonder if there's anything more efficient than .Intersect that gives you an unsorted result. 
        (For reference, .Intersect makes a HashSet of array1 then foreach(var value in array2) tries hash.Remove(value) to check for shared values
         Implementing my own kind of intersect in DoItAll() had moderate memory improvements over my original solution but also saw a speed decrease.
         Using MemoryMappedFile or BinaryReader were both extremely slow or memory inefficient for that method)

    Overall, this project was fun to do and then try to optimize. 
    I went into it with a lot of confidence having used FileStream, BinaryReader/Writer, and BitConverter in a lot of other projects and having recently learned a little about MemoryMarshal.
    But the bulk of the problem was efficiently comparing the two files, which had nothing to do with any of those things I just mentioned!
    I'm a little disapointed that I took the easy way out with .Intersect then immediately moved on to asking ChatGPT for optimization tips
    but, at the same time, ChatGPT taught me a lot of things that could have taken a lot of Google-fu and research to come across naturally.

    */

    // Some crude benchmarks
    //                                                                Read uints                             Get intersection                Write file
    // Original (No GPT) (Unsorted)        [91,098ms, 8.2 GB memory]: ReadUInt32sFromFileAsync,              Intersection,                   WriteUInt32sToFile
    // DoItAll           (Unsorted)       [101,353ms, 6.1 GB memory]: ReadValuesFromFile,  Self-implemented intersection writer
    // Best Memory Combo (Sorted)          [55,977ms, 2.8 GB memory]: ReadValuesFromFile,  TwoPointersHashSetIntersection, WriteUInt32sToFileWithBinaryWriter
    // Best Speed Combo  (Unsorted)        [32,405ms, ??? GB memory]: ReadUInt32sFromFileAsync,              IntersectionParallel,           WriteUInt32sToFileWithBinaryWriter

    public static class OldSolutions
    {
        const int UInt32ByteLength = 4;

        // Saving memory at the cost of speed (unsorted results)
        // HashSet:    101,353ms, 6.1 GB
        // Dictionary:  92,305ms  7.1 GB
        static async void DoItAll(string inputPath1, string inputPath2, string outputPath)
        {
            long binLength;
            int binValueCount;

            var bin2Task = ReadMemoryMappedUInt32sFromFileAsync(inputPath2);
            HashSet<uint> hashSet = new(await ReadMemoryMappedUInt32sFromFileAsync(inputPath1));

            using FileStream fs = new FileStream(outputPath, FileMode.Create);
            using BinaryWriter br = new(fs);

            uint[] bin2values = await bin2Task;
            for (int i = 0; i < bin2values.Length; i++)
            {
                if (hashSet.Remove(bin2values[i]))
                    br.Write(bin2values[i]);
            }
        }

        // My original file reading solution
        // 1,960ms
        // Pros: Fairly quick
        // Cons: Double allocation
        static async Task<uint[]> ReadUInt32sFromFileAsync(string filePath)
        {
            byte[] data = await File.ReadAllBytesAsync(filePath);
            uint[] values = new uint[data.Length / UInt32ByteLength];
            Buffer.BlockCopy(data, 0, values, 0, data.Length);
            return values;
        }

        // BinaryReader
        // 5,827ms
        // Pros: Read directly into uint[]
        // Cons: Slow
        static async Task<uint[]> ReadUInt32sFromBinaryReaderAsync(string filePath)
        {
            using FileStream fs = File.OpenRead(filePath);
            using BinaryReader br = new(fs);

            await Task.Yield();

            uint[] values = new uint[fs.Length / UInt32ByteLength];
            for (int i = 0; i < values.Length; i++)
                values[i] = br.ReadUInt32();

            return values;

        }

        // ChatGPT's MemoryMappedFile reading solution
        // 3,591ms
        // Pros: Read directly into uint[], faster than BinaryReader
        // Cons: Slightly slower than Buffer.BlockCopy
        static async Task<uint[]> ReadMemoryMappedUInt32sFromFileAsync(string filePath)
        {
            using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            int fileSize = (int)accessor.Capacity;
            int numUInts = fileSize / UInt32ByteLength;

            var values = new uint[numUInts];
            accessor.ReadArray(0, values, 0, numUInts);

            return values;
        }

        // My original file writing solution
        // 465ms
        // Cons: Copies array
        static void WriteUInt32sToFile(string filePath, uint[] array)
        {
            byte[] data = new byte[array.Length * UInt32ByteLength];
            Buffer.BlockCopy(array, 0, data, 0, data.Length);
            File.WriteAllBytes(filePath, data);
        }

        // BinaryWriter, "I thought this would be slower"
        // 231ms
        static void WriteUInt32sToFileWithBinaryWriter(string filePath, uint[] array)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            using (BinaryWriter br = new(fs))
            {
                foreach (uint value in array)
                    br.Write(value);
            }
        }

        // Different ways of getting the intersection of two arrays
        // Both arrays contain 268,435,456 values in the commented benchmarks

        // My original solution, "The easy way"
        // 89,425ms
        // Pros: Easy, output is not sorted
        // Cons: Not great on speed or memory
        static uint[] Intersection(uint[] array1, uint[] array2) => array1.Intersect(array2).ToArray();


        // Presorted Intersection, "Does this help?"
        // 65,289ms
        // Pros: Slightly faster than Intersection
        // Cons: Mutates both arrays, still not great on memory
        static uint[] IntersectionSorted(uint[] array1, uint[] array2)
        {
            Array.Sort(array1);
            Array.Sort(array2);
            return array1.Intersect(array2).ToArray();
        }

        // Intersect with AsParallel(), "Is my RAM on fire?"
        // 30,802ms
        // Pros: FAST!
        // Cons: Memory hog
        static uint[] IntersectionParallel(uint[] array1, uint[] array2) => array1.AsParallel().Intersect(array2.AsParallel()).ToArray();

        // ChatGPT's Two-Pointers solution, "I didn't fully understand the problem but..."
        // 49,708ms
        // Pros: Faster and more memory efficient than Intersection
        // Cons: Mutates both arrays, includes duplicates
        static uint[] TwoPointersIntersection(uint[] array1, uint[] array2)
        {
            Array.Sort(array1);
            Array.Sort(array2);

            var sharedValues = new List<uint>();

            int i = 0;
            int j = 0;

            while (i < array1.Length && j < array2.Length)
            {
                if (array1[i] < array2[j])
                {
                    i++;
                }
                else if (array1[i] > array2[j])
                {
                    j++;
                }
                else
                {
                    sharedValues.Add(array1[i]);
                    i++;
                    j++;
                }
            }

            return sharedValues.ToArray();
        }

        // Two-Pointer method with HashSet instead of List, "Noice"
        // 52,424ms
        // Pros: Faster and more memory efficient than Intersection
        // Cons: Mutates both arrays
        static uint[] TwoPointersHashSetIntersection(uint[] array1, uint[] array2)
        {
            Array.Sort(array1);
            Array.Sort(array2);

            var sharedValues = new HashSet<uint>();

            int i = 0;
            int j = 0;

            while (i < array1.Length && j < array2.Length)
            {
                if (array1[i] < array2[j])
                {
                    i++;
                }
                else if (array1[i] > array2[j])
                {
                    j++;
                }
                else
                {
                    sharedValues.Add(array1[i]);
                    i++;
                    j++;
                }
            }

            return sharedValues.ToArray();
        }

        // Pure Two-Pointer method w/ HashSet, "Sometimes it's important"
        // 57,158ms
        // Pros: Pure, not much speed loss
        // Cons: Moderate extra RAM usage
        static uint[] PureTwoPointersHashSetIntersection(uint[] array1, uint[] array2)
        {
            uint[] sorted1 = DuplicateAndSortArray(array1);
            uint[] sorted2 = DuplicateAndSortArray(array2);

            var sharedValues = new HashSet<uint>();

            int i = 0;
            int j = 0;

            while (i < sorted1.Length && j < sorted2.Length)
            {
                if (sorted1[i] < sorted2[j])
                {
                    i++;
                }
                else if (sorted1[i] > sorted2[j])
                {
                    j++;
                }
                else
                {
                    sharedValues.Add(sorted1[i]);
                    i++;
                    j++;
                }
            }

            return sharedValues.ToArray();

        }

        static T[] DuplicateAndSortArray<T>(T[] array)
        {
            T[] duplicate = new T[array.Length];
            Array.Copy(array, duplicate, array.Length);
            Array.Sort(duplicate);
            return duplicate;
        }
    }
}
