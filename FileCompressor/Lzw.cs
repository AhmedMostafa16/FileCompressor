// ReSharper disable SuggestVarOrType_BuiltInTypes

using System.Runtime.CompilerServices;

namespace FileCompressor;

public class Lzw
{
    private const int MAX_BITS = 14; // maximum bits allowed to read
    private const int HASH_BIT = MAX_BITS - 8; // hash bit to use with the hashing algorithm to find correct index
    private const int MAX_VALUE = (1 << MAX_BITS) - 1; // max value allowed based on MaxBits
    private const int MAX_CODE = MAX_VALUE - 1; // max code possible
    private const int TABLE_SIZE = 18041; // must be bigger than the maximum allowed by MaxBits and prime

    private readonly int[] _charTable = new int[TABLE_SIZE]; // character table

    private readonly int[] _codeTable = new int[TABLE_SIZE]; // code table

    private readonly int[] _prefixTable = new int[TABLE_SIZE]; // prefix table

    private ulong _bitBuffer; // bit buffer to temporarily store bytes read from the files
    private int _bitCounter; // counter for knowing how many bits are in the bit buffer
    public long CompressedSize { get; private set; }
    public long DecompressedSize { get; private set; }
    public double Ratio => (double)CompressedSize / DecompressedSize * 100.0;

    // used to blank out bit buffer in case this class is called to compress and decompress from the same instance
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Initialize()
    {
        _bitBuffer = 0;
        _bitCounter = 0;
    }

    public void Compress(string inputFileName, string outputFileName)
    {
        FileStream reader = null;
        FileStream writer = null;

        try
        {
            Initialize();
            reader = new FileStream(inputFileName, FileMode.Open);
            writer = new FileStream(outputFileName, FileMode.Create);

            DecompressedSize = reader.Length;

            Array.Fill(_codeTable, -1); // blank out table

            int nextCode = 256;
            int firstCode = reader.ReadByte(); // get first code, will be 0-255 ascii char
            int readByte;

            while ((readByte = reader.ReadByte()) != -1) // read until we reach end of file
            {
                int index = FindMatch(firstCode, readByte);

                if (_codeTable[index] != -1) // set string if we have something at that index
                {
                    firstCode = _codeTable[index];
                }
                else // insert new entry
                {
                    if (nextCode <= MAX_CODE) // otherwise we insert into the tables
                    {
                        _codeTable[index] = nextCode++; // insert and increment next code to use
                        _prefixTable[index] = firstCode;
                        _charTable[index] = (byte)readByte;
                    }

                    WriteCode(writer, firstCode); // output the data in the string
                    firstCode = readByte;
                }
            }

            WriteCode(writer, firstCode); // output last code
            WriteCode(writer, MAX_VALUE); // output end of buffer
            WriteCode(writer, 0); // flush
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.StackTrace);
            if (writer != null)
                writer.Close();
            File.Delete(outputFileName);
        }
        finally
        {
            reader?.Close();
            if (writer != null)
            {
                CompressedSize = writer.Length;
                writer.Close();
            }
        }
    }

    // FindMatch method tries to find index of prefix+char if not found, returns -1 to signify space available
    private int FindMatch(int prefix, int ch)
    {
        int index = (ch << HASH_BIT) ^ prefix;

        int offset = index == 0 ? 1 : TABLE_SIZE - index;

        while (true)
        {
            if (_codeTable[index] == -1)
                return index;

            if (_prefixTable[index] == prefix && _charTable[index] == ch)
                return index;

            index -= offset;
            if (index < 0)
                index += TABLE_SIZE;
        }
    }

    private void WriteCode(Stream writer, int code)
    {
        _bitBuffer |= (ulong)code << (32 - MAX_BITS - _bitCounter); //make space and insert new code in buffer
        _bitCounter += MAX_BITS; //increment bit counter

        while (_bitCounter >= 8) //write all the bytes we can
        {
            // int temp = (byte)((_bitBuffer >> 24) & 255);
            writer.WriteByte((byte)((_bitBuffer >> 24) & 255)); //write byte from bit buffer
            _bitBuffer <<= 8; //remove written byte from buffer
            _bitCounter -= 8; //decrement counter
        }
    }

    public void Decompress(string inputFileName, string outputFileName)
    {
        Stream reader = null;
        Stream writer = null;

        try
        {
            Initialize();
            reader = new FileStream(inputFileName, FileMode.Open);
            writer = new FileStream(outputFileName, FileMode.Create);

            CompressedSize = reader.Length;

            int nextCode = 256;
            byte[] decodeStack = new byte[TABLE_SIZE];

            int oldCode = ReadCode(reader);
            byte code = (byte)oldCode;
            writer.WriteByte((byte)oldCode); // write first byte since it is plain ascii

            int newCode = ReadCode(reader);

            while (newCode != MAX_VALUE) // read file all file
            {
                int currentCode;
                int counter;
                if (newCode >= nextCode)
                {
                    // fix for prefix+chr+prefix+char+prefx special case
                    decodeStack[0] = code;
                    counter = 1;
                    currentCode = oldCode;
                }
                else
                {
                    counter = 0;
                    currentCode = newCode;
                }

                while (currentCode > 255) //decode string by cycling back through the prefixes
                {
                    //lstDecodeStack.Add((byte)_charTable[currentCode]);
                    //currentCode = _prefixTable[currentCode];
                    decodeStack[counter] = (byte)_charTable[currentCode];
                    ++counter;
                    if (counter >= MAX_CODE)
                        throw new Exception("This character is out of char table");
                    currentCode = _prefixTable[currentCode];
                }

                decodeStack[counter] = (byte)currentCode;
                code = decodeStack[counter]; // set last char used

                while (counter >= 0) // write out decodeStack
                {
                    writer.WriteByte(decodeStack[counter]);
                    --counter;
                }

                if (nextCode <= MAX_CODE) // insert into tables
                {
                    _prefixTable[nextCode] = oldCode;
                    _charTable[nextCode] = code;
                    ++nextCode;
                }

                oldCode = newCode;

                //if (reader.PeekChar() != 0)
                newCode = ReadCode(reader);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.StackTrace);
            writer?.Close();
            File.Delete(outputFileName);
        }
        finally
        {
            reader?.Close();
            if (writer != null)
            {
                DecompressedSize = writer.Length;
                writer.Close();
            }
        }
    }

    private int ReadCode(Stream pReader)
    {
        while (_bitCounter <= 24) // fill up buffer
        {
            _bitBuffer |= (ulong)pReader.ReadByte() << (24 - _bitCounter); // insert byte into buffer
            _bitCounter += 8; // increment counter by 8 (1 byte)
        }

        uint returnVal = (uint)_bitBuffer >> (32 - MAX_BITS);
        _bitBuffer <<= MAX_BITS; // remove it from buffer
        _bitCounter -= MAX_BITS; // decrement bit counter

        return (int)returnVal;
    }
}