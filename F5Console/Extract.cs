using System;
using System.IO;

namespace F5
{
    public static class Extract
    {
        public static void Run(params string[] args)
        {
            if (args.Length < 1)
            {
                StandardUsage();
                return;
            }

            string jpegFileName = null;
            string embFileName = null;
            string password = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-"))
                {
                    if (!args[i].EndsWith(".jpg"))
                    {
                        StandardUsage();
                        return;
                    }
                    jpegFileName = args[i];
                    continue;
                }
                if (args.Length < +1)
                {
                    Console.WriteLine("Missing parameter for switch " + args[i]);
                    StandardUsage();
                    return;
                }
                if (args[i].Equals("-e"))
                {
                    embFileName = args[i + 1];
                }
                else if (args[i].Equals("-p"))
                {
                    password = args[i + 1];
                }
                else
                {
                    Console.WriteLine("Unknown switch " + args[i] + " ignored.");
                }
                i++;
            }

            using (JpegExtract extractor = new JpegExtract(File.OpenWrite(embFileName), password))
            {
                extractor.Extract(File.OpenRead(jpegFileName));
            }
        }

        public static void StandardUsage()
        {
            Console.WriteLine("Extract [Options] \"image.jpg\"");
            Console.WriteLine("Options:");
            Console.WriteLine("\t-p password (default: abc123)");
            Console.WriteLine("\t-e extractedFileName (default: output.txt)");
            Console.WriteLine("\nAuthor: Andreas Westfeld, westfeld@inf.tu-dresden.de");
        }
    }
}
