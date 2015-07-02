using System;
using System.Linq;

namespace F5
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1 || args[0].Equals("-h") || args[0].Equals("--help"))
            {
                Console.WriteLine("F5 Usage: F5 [e|x|-h] [-options] files...\n -e for Embed mode\n -x for eXtract mode\n -h for help");
                Console.WriteLine("\nHelp for embedding:\n");
                Embed.StandardUsage();
                Console.WriteLine("\n\n----------------------------------\nHelp for extracting:\n\n");
                Extract.StandardUsage();
                Console.WriteLine("\n\n----------------------------------\nExamples:");
                Console.WriteLine("F5 e -e msg.txt -p mypasswd -q 70 in.jpg out.jpg");
                Console.WriteLine("F5 x -p mypasswd -e out.txt in.jpg");
                Console.WriteLine();
            }
            else if (args[0].Equals("e"))
            {
                Embed.Run(args.Skip(1).ToArray());
            }
            else if (args[0].Equals("x"))
            {
                Extract.Run(args.Skip(1).ToArray());
            }
            else
            {
                Main(new string[0]);
            }
        }
    }
}
