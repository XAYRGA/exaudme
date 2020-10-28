using System;
using System.Text;
using System.IO;
using Be.IO;


/* 
    BME (no magic)
    0x00 int32 count 
    0x08 int32[count] sequence* 


    sequence:
    Opcode size: 1 byte 

    Instructions:
    0x20, none = OPEN 
    0x24, none = CLOSE 
    0x40, short = WAIT
 */

/* 
  BMT (no magic)
    0x00 int32 recordCount 
    0x04 int32 recordClusterOffset
    0x08 int32 nametableOffset
    0x0C int32 0x00000000
    0x10 (or &recordClusterOffset) record[recordCount] records; 

    &nametableOffset int32[recordCount] namePointers
    &namePointer string \x00 

    struct record {
	    byte[8]
    }
 */

namespace exAudMe
{
    class Program
    {
        static void Main(string[] args)
        {

            cmdarg.cmdargs = args; 
            var bmeFile = cmdarg.assertArg(0, "BME File");
            var bmtFile = cmdarg.assertArg(1, "BMT File");
            var outDir = cmdarg.findDynamicStringArgument("-outdir", "./");

            if (!File.Exists(bmeFile))
                cmdarg.assert("BME file not found '{0}'", bmeFile);
            if (!File.Exists(bmtFile))
                cmdarg.assert("BMT file not found '{0}'", bmtFile);

            Stream hBME = null;
            Stream hBMT = null;
            try {
                hBME = File.OpenRead(bmeFile);
            } catch (Exception E) { cmdarg.assert("Failed to open BME file: {0}", E.Message);}

            try {
                hBMT = File.OpenRead(bmtFile);
            } catch (Exception E) { cmdarg.assert("Failed to open BMT file: {0}", E.Message); }

            BeBinaryReader rBME = new BeBinaryReader(hBME);
            BeBinaryReader rBMT = new BeBinaryReader(hBMT);

            try {
                Directory.CreateDirectory(outDir);
            } catch (Exception E) { cmdarg.assert("Cannot create output directory {0}", E.Message); }

            string[] names; 
            //--------------------------
            // Parse BMT (contains names)
            {
                var count = rBMT.ReadInt32();
                rBMT.ReadInt32(); // Unknown?
                var paramSize = 8;
                var nametableOffset = rBMT.ReadInt32();

                names = new string[count];
                rBMT.BaseStream.Position = nametableOffset;
                int[] namePointers = readInt32Array(rBMT, count);

                for (int i = 0; i < count; i++)
                {
                    rBMT.BaseStream.Position = namePointers[i];
                    names[i] = readTerminated(rBMT, 0x00);
                }

                rBMT.BaseStream.Position = 0x10; // Seek back to byte stream. 
                //long anchor = 0x10;
                for (int i = 0; i < count; i++)
                {
                    //Console.WriteLine(rBMT.BaseStream.Position);
                    var byArr = rBMT.ReadBytes(paramSize);
                    File.WriteAllBytes($"{outDir}/{names[i]}.prm",byArr);
                }
            }
            rBMT.Close();
            hBMT.Close();
            
            {
                var count = rBME.ReadInt32();
                int[] seqPointers = readInt32Array(rBME, count);

                var len = seqPointers.Length;

                /*
                for (int i = 0; i < len - 1; i++) 
                {
                    for (int j = 0; j < len - 1; j++)
                    {
                        var current = seqPointers[i];
                        var currentName = names[i]; 

                        var cmp = seqPointers[j];
                        var cmpName = names[j];
                        if (cmp > current) // if its time is greater than ours
                        {
                            seqPointers[j] = current; // shift us down
                            names[j] = currentName;

                            seqPointers[i] = cmp; // shift it up
                            names[i] = cmpName;
                        }
                    }
                }
                */



                int[] sizes = new int[count];


                for (int i = 0; i < count - 1; i++)
                {
                    rBME.BaseStream.Position = seqPointers[i];
                    short nextshort = 0;
                    int size = 0;
                    while ((nextshort = rBME.ReadInt16()) != 0x2420) // hax
                    {
                        rBME.BaseStream.Seek(-1, SeekOrigin.Current);
                        size ++;  // hax 
                    }
                    size += 1; // i'm so sorry
                    sizes[i] = size; // 
                }
                var sl = sizes.Length - 1;
                sizes[sl] = (int)(rBME.BaseStream.Length - seqPointers[sl]);

                for (int i = 0; i < count; i++)
                {
                    if (sizes[i] < 0)
                    {
                        Console.WriteLine($"{names[i]} error -- couldn't calculate delta size");
                        continue;
                    }
                    Console.WriteLine($"\t->Extracted {names[i]} Size:{sizes[i]:X}");
                    var byArr = rBME.ReadBytes(sizes[i]);
                    rBME.BaseStream.Position = seqPointers[i];
                    File.WriteAllBytes($"{outDir}/{names[i]}.seq", byArr);
                }
            }
            rBME.Close();
            hBME.Close();    
        }

        public static int[] readInt32Array(BeBinaryReader binStream, int count)
        {
            var b = new int[count];
            for (int i = 0; i < count; i++)
            {
                b[i] = binStream.ReadInt32();
            }
            return b;
        }

        public static string readTerminated(BeBinaryReader rd, byte term)
        {
            int count = 0;
            int nextbyte = 0xFFFFFFF;
            byte[] name = new byte[0xFF];
            while ((nextbyte = rd.ReadByte()) != term)
            {
                name[count] = (byte)nextbyte;
                count++;
            }
            return Encoding.ASCII.GetString(name, 0, count);
        }
    }
}
