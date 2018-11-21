using MessagePack;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IOBound
{
    /// <summary>
    /// Sample container object which is serialized to MessagePack and Json
    /// </summary>
    [MessagePackObject]
    public class Container
    {
        [Key(0)]
        public List<double> Doubles = new List<double>();
        [Key(1)]
        public List<int> Ints = new List<int>();
    }

    /// <summary>
    /// Test how fast you can read 10 million doubles and integers from a file. 
    /// 
    /// This includes
    ///   - Reading data from a text file where on each line a double and an integer is stored of the form
    ///          d.dddd xxxxxx
    ///      - ReadFileIntoByteBuffer: Read the file via the ReadFile Win32 API at once to show how long the disc IO takes.
    ///      - CountLines:             Read the file line by line but not parsing or storing of the data is performed
    ///                                This is basically only counting the lines
    ///       - ParseSpan:             Read and parse the text file with a Span based approach
    ///       - ParseLinesUnsafe:      Read and parse the file with raw pointers
    ///
    ///  - Reading data from a Container object as JSON and binary serialized MessagePack object
    ///             public class Container
    ///             {
    ///                public List<double> Doubles = new List<double>();
    ///                public List<int> Ints = new List<int>();
    ///             }
    ///       - DeserializeMessagePack:  Read binary messagepack file and deserialize ContainerObject
    ///       - DeserializeUTF8Json:     Read JSON file and deserialize ContainerObject
    ///                                  UTF8Json is the fastest known serializer for .NET
    /// </summary>
    class Program
    {
        const string MessagePackExt = ".msgPack";
        const string JsonExt = ".json";

        static void Do(string[] args)
        { }

        static void Main(string[] args)
        {
            string fileName = "NumericData.txt";
            string jsonFile = fileName + JsonExt;
            string messagePackFile = fileName + MessagePackExt;

            if (!File.Exists(fileName) || !File.Exists(jsonFile) || !File.Exists(messagePackFile) )
            {
                CreateTestData(fileName);
            }

            int MBText = (int)(new FileInfo(fileName).Length / (1024 * 1024));
            int MBJson = (int)(new FileInfo(jsonFile).Length / (1024 * 1024));
            int MBMessagePack = (int)(new FileInfo(messagePackFile).Length / (1024 * 1024));

            uint bytes = 0;

            var sw = Stopwatch.StartNew();
            bytes = ReadFileIntoByteBuffer(fileName);
            sw.Stop();
            Console.WriteLine($"ReadFile                {bytes / (1024 * 1024)} MB in {sw.Elapsed.TotalSeconds:F2}s, {MBText / sw.Elapsed.TotalSeconds:F2} MB/s");

            CleanMemory();

            sw = Stopwatch.StartNew();
            int n = CountLines(fileName);
            sw.Stop();
            Console.WriteLine($"Count Lines             {bytes / (1024 * 1024)} MB in {sw.Elapsed.TotalSeconds:F2}s,  {MBText / sw.Elapsed.TotalSeconds:F2} MB/s, {n:N0} lines");

            CleanMemory();

            sw = Stopwatch.StartNew();
            ParseSpan(fileName);
            sw.Stop();
            Console.WriteLine($"Span Parser             {bytes / (1024 * 1024)} MB in {sw.Elapsed.TotalSeconds:F2}s,  {MBText / sw.Elapsed.TotalSeconds:F2} MB/s");

            CleanMemory();

            sw = Stopwatch.StartNew();
            ParseLinesUnsafe(fileName);
            sw.Stop();
            Console.WriteLine($"Unsafe Parse            {MBText} MB in {sw.Elapsed.TotalSeconds:F2}s,  {MBText / sw.Elapsed.TotalSeconds:F2} MB/s");

            CleanMemory();

            sw = Stopwatch.StartNew();
            DeserializeMessagePack(messagePackFile);
            sw.Stop();
            Console.WriteLine($"Deserialize MessagePack {MBMessagePack} MB in {sw.Elapsed.TotalSeconds:F2}s,  {MBMessagePack / sw.Elapsed.TotalSeconds:F2} MB/s");

            CleanMemory();

            sw = Stopwatch.StartNew();
            DeserializeUTF8Json(jsonFile);
            sw.Stop();
            Console.WriteLine($"Deserialize Utf8Json    {MBJson} MB in {sw.Elapsed.TotalSeconds:F2}s,   {MBJson / sw.Elapsed.TotalSeconds:F2} MB/s");

            CleanMemory();

            sw = Stopwatch.StartNew();
            ParseLines(fileName);
            sw.Stop();
            Console.WriteLine($"Standard Parse          {MBText} MB in {sw.Elapsed.TotalSeconds:F2}s,   {MBText / sw.Elapsed.TotalSeconds:F2} MB/s");
        }


        /// <summary>
        /// Deserialize a binary message pack file with the fastest message pack parser from neucc. 
        /// See https://aloiskraus.wordpress.com/2018/05/06/serialization-performance-update-with-net-4-7-2/
        /// </summary>
        private static void DeserializeMessagePack(string dataFile)
        {
            using (var stream = new FileStream(dataFile, FileMode.Open))
            {
                Container deser = MessagePack.MessagePackSerializer.Deserialize<Container>(stream);
            }
        }

        /// <summary>
        /// Deserialize a Json text file with the fastest known Json serializer
        /// https://github.com/neuecc/Utf8Json
        /// </summary>
        private static void DeserializeUTF8Json(string dataFile)
        {
            using (var stream = new FileStream(dataFile, FileMode.Open))
            {
                Container deser = Utf8Json.JsonSerializer.Deserialize<Container>(stream);
            }
        }

        /// <summary>
        /// Check native reading speed from the OS
        /// </summary>
        private unsafe static uint ReadFileIntoByteBuffer(string dataFile)
        {
            using (var stream = new FileStream(dataFile, FileMode.Open))
            {
                byte[] buf = new byte[200 * 1024 * 1024];
                fixed (byte* pBuf = &buf[0])
                {
                    uint dwRead = 0;
                    if (ReadFile(stream.SafeFileHandle, pBuf, 200 * 1000 * 1000, out dwRead, IntPtr.Zero) == 0)
                    {
                        throw new Win32Exception();
                    }
                    return dwRead;
                }

            }
        }

        /// <summary>
        /// Read the file line by line and count the lines.
        /// This is very fast because one never allocates long living objects.
        /// </summary>
        private static int CountLines(string dataFile)
        {
            using (var reader = new StreamReader(dataFile))
            {
                string line;
                int count = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    count++;
                }

                return count;
            }
        }

        static byte[] NewLine = Encoding.ASCII.GetBytes(Environment.NewLine);

        /// <summary>
        /// High Perf Span based text parser
        /// </summary>
        /// <param name="dataFile"></param>
        private static void ParseSpan(string dataFile)
        {
            var doubles = new List<double>();
            var ints = new List<int>();

            double curMeasurement = 0.0d;
            int curMeasurement2 = 0;

            ReadOnlySpan<byte> newLineSpan = NewLine.AsSpan();

            using (var stream = new FileStream(dataFile, FileMode.Open))
            {
                const int bufferSize = 10*4096;
                byte[] buffer = new byte[bufferSize];
                int readBytes = 0;

                int lastLineSize = 0;
                while ((readBytes = stream.Read(buffer, lastLineSize, bufferSize- lastLineSize)) != 0)
                {
                    Span<byte> bufferSpan = new Span<byte>(buffer, 0, readBytes+ lastLineSize);

                    if( bufferSpan.StartsWith(Encoding.UTF8.GetPreamble()) ) // skip byte order mark
                    {
                        bufferSpan = bufferSpan.Slice(Encoding.UTF8.GetPreamble().Length); 
                    }

                    int newLineStart = 0;
                    while( (newLineStart = bufferSpan.IndexOf(newLineSpan)) > 0 )
                    {
                        if( ParseLine( bufferSpan.Slice(0, newLineStart), ref curMeasurement, ref curMeasurement2) )
                        {
                            doubles.Add(curMeasurement);
                            ints.Add(curMeasurement2);
                        }
                        bufferSpan = bufferSpan.Slice(newLineStart + newLineSpan.Length);
                    }

                    bufferSpan.CopyTo(buffer.AsSpan());
                    lastLineSize = bufferSpan.Length;
                }
            }
        }

        /// <summary>
        /// Parse a input line and set the parsed double and int on success by reference
        /// </summary>
        private static bool ParseLine(Span<byte> line, ref double value, ref int other)
        {

            //bool success = Utf8Parser.TryParse(line, out value, out int bytesConsumed);
            bool success = TryParseDouble(line, out value, out int bytesConsumed);
            if ( success )
            {
                if( line.Length-bytesConsumed > 1 ) // after double a space is separator. No whole number fits there
                {
                    //  return Utf8Parser.TryParse(line.Slice(bytesConsumed + 1), out other, out bytesConsumed);
                    return TryParseInt(line.Slice(bytesConsumed + 1), out other, out bytesConsumed);
                }
            }

            return false;
        }

        /// <summary>
        /// Parse an integer from a byte buffer. This assumes no thousand separators which are dependent on the 
        /// current locale. This is the reason for the "bad" performance of the Utf8Parser.TryParse method.
        /// </summary>
        static bool TryParseInt(Span<byte> bytes, out int value, out int bytesConsumed)
        {
            int parsed = 0;
            bytesConsumed = 0;

            bool succeeded = false;
            for (int i = 0; i < bytes.Length; i++)
            {
                var charB = (char)bytes[i];

                if (char.IsNumber(charB))
                {
                    parsed += parsed * 10 + (charB - '0');
                    succeeded = true;
                }
                else
                {
                    bytesConsumed = i;
                    break;
                }
            }

            value = parsed;
            return succeeded;
        }

        /// <summary>
        /// This assumes the double has no exponent notation inside it!
        /// </summary>
        static bool TryParseDouble(Span<byte> bytes, out double value, out int bytesConsumed)
        {
            long a = 0, b = 0;
            long div = 1;
            bytesConsumed = 0;
            char bi = (char) 0;
            int i = 0;
            for (; i < bytes.Length; i++) // two for loops means less branching for the CPU
            {
                bi = (char)bytes[i];
                if (char.IsNumber(bi))
                {
                    a = a * 10 + bi - '0';
                }
                else
                {
                    break;
                }
            }

            i++;

            for (;i<bytes.Length;i++)
            {
                bi = (char)bytes[i];
                if (char.IsNumber(bi))
                {
                    b += b * 10 + (bi - '0');
                    div *= 10;
                }
                else
                {
                    bytesConsumed = i;
                    break;
                }
            }

            value =  a + ((double)b) / div;
            return bytesConsumed != 0;
        }

        /// <summary>
        /// This code ignores the BOM mark at the beginning of the file for simplicity
        /// </summary>
        /// <param name="dataFile"></param>
        unsafe private static void ParseLinesUnsafe(string dataFile)
        {
            var dobules = new List<double>();
            var ints = new List<int>();
            char decimalChar = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

            using (var reader = new StreamReader(dataFile))
            {
                string line;
                double d = 0;
                long a = 0, b = 0;
                int ix = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    int len = line.Length;
                    ix = 0;
                    a = 0;
                    b = 0;
                    fixed (char* ln = line)
                    {
                        while (ix < len && char.IsNumber(ln[ix]))
                        {
                            a = a * 10 + (ln[ix++] - '0');
                        }

                        if (ln[ix] == decimalChar)
                        {
                            ix++;
                            long div = 1;
                            while (ix < len && char.IsNumber(ln[ix]))
                            {
                                b += b * 10 + (ln[ix++] - '0');
                                div *= 10;
                            }
                            d = a + ((double)b) / div;
                        }

                        while (ix < len && char.IsWhiteSpace(ln[ix]))
                        {
                            ix++;
                        }

                        int i = 0;
                        while (ix < len && char.IsNumber(ln[ix]))
                        {
                            i = i * 10 + (ln[ix++] - '0');
                        }

                        dobules.Add(d);
                        ints.Add(i);
                    }
                }
            }
        }

        /// <summary>
        /// Straightforward implementation how one would naively parse the line 
        /// </summary>
        /// <param name="dataFile"></param>
        private static void ParseLines(string dataFile)
        {
            var dobules = new List<double>();
            var ints = new List<int>();

            using (var reader = new StreamReader(dataFile))
            {
                string line;
                char[] sep = new char[] { ' ' };
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(sep);
                    if (parts.Length == 2)
                    {
                        dobules.Add(double.Parse(parts[0]));
                        ints.Add(int.Parse(parts[1]));
                    }
                }
            }
        }

        /// <summary>
        /// Create the test input data as text file, .son and .messagepack file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="n"></param>
        static void CreateTestData(string fileName, int n=10*1000*1000)
        {
            using (var fstream = new FileStream(fileName, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(fstream, Encoding.UTF8))
                {
                    for (int i = 0; i < n; i++)
                    {
                        writer.WriteLine("{0} {1}", 1.1d + i, i);
                    }
                }
            }

            string jFileName = fileName + JsonExt;
            string msgFileName = fileName + MessagePackExt;
            using (var mStream = new FileStream(msgFileName, FileMode.Create))
            {
                using (var jStream = new FileStream(jFileName, FileMode.Create))
                {
                    Container list = new Container();
                    for (int i = 0; i < n; i++)
                    {
                        list.Doubles.Add(1.1d + i);
                        list.Ints.Add(i);
                    }


                    Utf8Json.JsonSerializer.Serialize<Container>(jStream, list);
                    MessagePack.MessagePackSerializer.Serialize<Container>(mStream, list);
                }
            }
        }

        /// <summary>
        /// PInvoke directly to the OS to test the native reading perf
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        unsafe static extern uint ReadFile(SafeFileHandle hFile, [Out] byte* lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        static void CleanMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}