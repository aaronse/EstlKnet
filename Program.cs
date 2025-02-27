using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EstlKnet
{
    // Small app to help post process EstlCam generated g-code to help Makers needing to split 
    // cut 'n carve jobs on CNC setups with 1-many routers (e.g. IDEX).

    // Based on Jason Yeager's (jeyeager) code from the .exe he posted  https://forum.v1e.com/t/how-many-are-were-interested-in-idex-builds/48047/23
    class Program
    {
        // Class to hold core configuration as parsed from GCODE comments containing JSON formatted settings
        public class CoreConfig
        {
            public string Core { get; set; }

            [JsonPropertyName("X-Axis")]
            public string XAxis { get; set; }

            public double Park { get; set; }
            // Optional feed rate. If zero, use rapid move (G00); if nonzero, use G1.
            
            public double Feed { get; set; }
        }

        public class RunOptions
        {
            public string FileExtension { get; set; }

            public string SourceFilePath { get; set; }
            public string DestFilePath { get; set; }
            public string BaseOutputName { get; internal set; }
        }

        public class MachineState
        {
            public MachineState()
            {
                this.CoreConfigs = new Dictionary<string, CoreConfig>();

                // The currently active core (by its identifier, e.g. "A" or "B").
                this.ActiveCore = null;
            }

            public Dictionary<string, CoreConfig> CoreConfigs {  get; set; }

            public string ActiveCore { get; set; }
        }

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

            // Check if a relative file path was specified.
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

            // Prepare naming for output files.
            string destFile = Path.Combine(
                Path.GetDirectoryName(sourceFile),
                Path.GetFileNameWithoutExtension(sourceFile) + "_knet" + Path.GetExtension(sourceFile));
            string baseOutputName = Path.GetFileNameWithoutExtension(sourceFile) + "_knet";
            string fileExtension = Path.GetExtension(sourceFile);

            RunOptions runOptions = new RunOptions()
            {
                SourceFilePath = sourceFile,
                DestFilePath = destFile,
                BaseOutputName = baseOutputName,
                FileExtension = fileExtension
            };

            // Flag for splitting output files on each Tool Change.
            bool splitByTool = false;
            int segmentIndex = 0;
            // We will assign 'output' to a StreamWriter that might be replaced when a Tool Change is encountered.
            StreamWriter output = null;

            // Machine and Core configurations
            MachineState machineState = new MachineState();
            
            // Open the default output file.
            output = File.CreateText(destFile);

            // Process the input file line by line.
            foreach (string readLine in File.ReadLines(sourceFile))
            {
                string line = readLine;

                // Look for the SplitByTool configuration.
                if (line.StartsWith("(SplitByTool:", StringComparison.OrdinalIgnoreCase))
                {
                    if (line.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        line.IndexOf("1") != -1)
                    {
                        splitByTool = true;
                        Console.WriteLine("SplitByTool enabled. Separate output files will be generated for each Tool Change.");
                        // Close the current file and open the first split segment.
                        output.Close();
                        segmentIndex = 0;
                        string splitPath = Path.Combine(Path.GetDirectoryName(sourceFile),
                            $"{baseOutputName}_{segmentIndex}{fileExtension}");
                        output = File.CreateText(splitPath);
                    }
                    output.WriteLine(line);
                    continue;
                }

                // Look for Core configuration comments (only those with the prefix "(Core :").
                if (line.StartsWith("(Core :", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("(Core:", StringComparison.OrdinalIgnoreCase))
                {
                    ProcessCoreConfig(
                        machineState,
                        line,
                        output);
                    continue;
                }

                // Process Tool Change comments.
                if (line.StartsWith("(Tool Change", StringComparison.OrdinalIgnoreCase))
                {
                    ProcessToolChange(
                        runOptions,
                        line,
                        splitByTool,
                        machineState,
                        ref segmentIndex,
                        ref output);
                    continue;
                }

                // Process movement commands: remap axis letter if needed.
                if (line.StartsWith("G", StringComparison.OrdinalIgnoreCase) &&
                    machineState.ActiveCore != null &&
                    machineState.CoreConfigs.ContainsKey(machineState.ActiveCore))
                {
                    CoreConfig activeConfig = machineState.CoreConfigs[machineState.ActiveCore];
                    // If the active core's X-Axis letter differs from "X", replace it.
                    if (!activeConfig.XAxis.Equals("X", StringComparison.OrdinalIgnoreCase))
                    {
                        line = line.Replace("X", activeConfig.XAxis);
                    }
                    output.WriteLine(line);
                    continue;
                }

                // For all other lines, simply write them out.
                output.WriteLine(line);
            }

            output.Close();
            Console.WriteLine("Processing complete.");
            return 0;
        }

        private static void ProcessCoreConfig(
            MachineState machineState,
            string line,
            StreamWriter output) 
        {
            line = line.Trim();
            int jsonStart = line.IndexOf('(');
            int jsonEnd = line.LastIndexOf(')');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                string jsonFragment = "{" + line.Substring(jsonStart + 1, jsonEnd - jsonStart - 1) + "}";

                // Pre-process the JSON fragment to add missing quotes around keys.
                string sanitizedJson = SanitizeJson(jsonFragment);

                try
                {
                    CoreConfig config = JsonSerializer.Deserialize<CoreConfig>(sanitizedJson);
                    if (config != null && !string.IsNullOrEmpty(config.Core))
                    {
                        machineState.CoreConfigs[config.Core] = config;
                        Console.WriteLine($"Loaded config for core {config.Core}: Axis {config.XAxis}, Park {config.Park}");

                        // Set the default active core to the one with the default axis "X" if not already set.
                        if (machineState.ActiveCore == null && "X".Equals(config.XAxis, StringComparison.OrdinalIgnoreCase))
                        {
                            machineState.ActiveCore = config.Core;
                            Console.WriteLine($"Default active core set to {machineState.ActiveCore}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to parse Core config: {line}\nError: {ex.Message}");
                }
            }
            output.WriteLine(line);
        }

        private static void ProcessToolChange(
            RunOptions runOptions,
            string line,
            bool splitByTool, 
            MachineState machineState,
            ref int segmentIndex,
            ref StreamWriter output)
        {
            int openBracket = line.IndexOf('[');
            int closeBracket = line.IndexOf(']');
            if (openBracket >= 0 && closeBracket > openBracket)
            {
                string newCore = line.Substring(openBracket + 1, closeBracket - openBracket - 1).Trim();
                if (!string.IsNullOrEmpty(newCore) && machineState.CoreConfigs.ContainsKey(newCore))
                {
                    // If a change is really happening, park the current active core.
                    if (machineState.ActiveCore != null && machineState.ActiveCore != newCore)
                    {
                        CoreConfig inactive = machineState.CoreConfigs[machineState.ActiveCore];
                        string axisLetter = inactive.XAxis;
                        string parkCommand = (inactive.Feed > 0)
                            ? $"G1 {axisLetter}{inactive.Park:0.000} F{inactive.Feed:0.000}"
                            : $"G00 {axisLetter}{inactive.Park:0.000}";
                        Console.WriteLine($"Parking core {machineState.ActiveCore} with command: {parkCommand}");
                        output.WriteLine(parkCommand);
                    }

                    machineState.ActiveCore = newCore;
                }
            }

            if (splitByTool)
            {
                // If splitting is enabled, close the current file and start a new one for this Tool Change.
                output.Close();
                segmentIndex++;
                string newOutputPath = Path.Combine(Path.GetDirectoryName(runOptions.SourceFilePath),
                    $"{runOptions.BaseOutputName}_{segmentIndex}{runOptions.FileExtension}");
                output = File.CreateText(newOutputPath);
                // Write the tool change comment into the new file.
                output.WriteLine(line);
            }
            else
            {
                // Otherwise, simply write the tool change comment.
                output.WriteLine(line);
            }
        }

        /// <summary>
        /// Pre-process the JSON fragment to relax the syntax by quoting unquoted keys.
        /// This regex looks for property names that are not wrapped in quotes and adds them.
        /// </summary>
        /// <param name="json">The raw JSON fragment.</param>
        /// <returns>A JSON fragment with keys properly quoted.</returns>
        static string SanitizeJson(string json)
        {
            // Quote keys: capture the delimiter ({, or ; with any spaces) in group 1,
            // then capture the key in group 2.
            json = Regex.Replace(json, @"([{,;]\s*)([\w-]+)\s*:", "$1\"$2\":");

            // Quote unquoted text values (alphabetic only), leaving numbers untouched.
            json = Regex.Replace(json, @":\s*([A-Za-z]+)(?=[,;}])", ": \"$1\"");

            // Replace semicolons with commas to ensure comma-delimited output.
            json = Regex.Replace(json, @";", ",");

            return json;
        }



    }
}
