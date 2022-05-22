// ReSharper disable SuggestVarOrType_BuiltInTypes

using System.Diagnostics;
using FileCompressor;

class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("LZW File Compressor");
            Console.WriteLine("Usage:");
            Console.WriteLine("./FileCompressor.exe [option] [file name]\n");
            Console.WriteLine("Options:");
            Console.WriteLine("\t --compress\t-c\tCompress file.");
            Console.WriteLine("\t --decompress\t-d\tDecompress file.");
        }
        else if ((args[0] == "--compress" || args[0] == "-c") && args[1] != null)
        {
            string fileName = Path.GetFileNameWithoutExtension(args[1]);
            string fileDirectory = Path.GetDirectoryName(args[1]);
            string fileExtension = Path.GetExtension(args[1]);

            Lzw lzw = new();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            lzw.Compress(args[1],
                Path.Combine(fileDirectory ?? string.Empty, string.Concat(fileName, "_compressed", fileExtension)));
            sw.Stop();

            Console.WriteLine("File has been compressed successfully!");
            Console.WriteLine("Time Elapsed in milliseconds: " + sw.Elapsed.TotalMilliseconds + "ms");
            Console.WriteLine($"Decompressed size: {lzw.DecompressedSize} B");
            Console.WriteLine($"Compressed size: {lzw.CompressedSize} B");
            Console.WriteLine("Ratio: %" + lzw.Ratio);
        }
        else if (args[0] == "--decompress" || args[0] == "-d")
        {
            string fileName = Path.GetFileNameWithoutExtension(args[1]);
            string fileDirectory = Path.GetDirectoryName(args[1]);
            string fileExtension = Path.GetExtension(args[1]);

            Stopwatch sw = new Stopwatch();
            sw.Start();

            Lzw lzw = new();
            lzw.Decompress(args[1],
                Path.Combine(fileDirectory ?? string.Empty, string.Concat(fileName, "_decompressed", fileExtension)));
            sw.Stop();

            Console.WriteLine("File has been decompressed successfully!");
            Console.WriteLine("Time Elapsed in milliseconds: " + sw.Elapsed.TotalMilliseconds + "ms");
            Console.WriteLine($"Compressed size: {lzw.CompressedSize} B");
            Console.WriteLine($"Decompressed size: {lzw.DecompressedSize} B");
            Console.WriteLine("Ratio: %" + lzw.Ratio);
        }
        else
        {
            Console.WriteLine("Passed arguments are not valid.");
        }
    }
}