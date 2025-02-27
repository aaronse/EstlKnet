using System;
using System.IO;

namespace EstlKnet
{
    // Small app to help post process EstlCam generated g-code to help Makers needing to split 
    // cut 'n carve jobs on CNC setups with 1-many routers (e.g. IDEX).

    // Based on Jason Yeager's (jeyeager) code from the .exe he posted  https://forum.v1e.com/t/how-many-are-were-interested-in-idex-builds/48047/23
    // 
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: EstlKnet <filepath>");
                return 1;
            }

            string sourceFile = args[0];
            if (File.Exists(sourceFile))
            {
                Console.WriteLine($"Processing {sourceFile}");
            }

            // Check is relative file path specified?
            if (!File.Exists(sourceFile) && Environment.CurrentDirectory.IndexOf("\\bin\\Debug") != -1)
            {
                string curDir = Environment.CurrentDirectory;
                string altFilePath = Path.Combine(
                    curDir.Substring(0, curDir.IndexOf("\\bin\\Debug")),
                    sourceFile);

                if (File.Exists(altFilePath))
                {
                    sourceFile = altFilePath;
                }

                Console.WriteLine($"Using alternative fallback path {sourceFile}");
            }

            if (!File.Exists(sourceFile))
            {
                Console.WriteLine($"FAIL, file '{sourceFile}' not found.");
                return 1;
            }

            string destFile = Path.Combine(
                Path.GetDirectoryName(sourceFile),
                Path.GetFileNameWithoutExtension(sourceFile) + "_knet" + Path.GetExtension(sourceFile));

            string oldValue = null;
            string newValue = null;
            using (StreamWriter text = File.CreateText(destFile))
            {
                foreach (string readLine in File.ReadLines(sourceFile))
                {
                    string str = readLine;
                    if (readLine.StartsWith("(Tool Change"))
                    {
                        if (!readLine.Contains("->"))
                        {
                            oldValue = null;
                            newValue = null;
                            Console.WriteLine("Disabling axis rewrite");
                        }
                        else
                        { 
                            oldValue = readLine.Substring(readLine.IndexOf('[') + 1, 1);
                            newValue = readLine.Substring(readLine.IndexOf(']') - 1, 1);
                            Console.WriteLine("Axis Change from {0} to {1}", (object)oldValue, (object)newValue);
                        }
                    }
                    else if (oldValue != null && newValue != null && readLine.StartsWith('G') && readLine.Contains(oldValue))
                    {
                        str = readLine.Replace(oldValue, newValue);
                    }

                    text.WriteLine(str);
                }
            }
            Console.WriteLine("Updated file written to {0}", (object)destFile);
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            return 0;
        }
    }
}
