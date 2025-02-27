using System.Text;

namespace EstlKnet
{
    internal class Args
    {
        Dictionary<string, string> _args = new Dictionary<string, string>();
        string[] _cmdArgs;

        internal string this[int index]
        {
            get { return _cmdArgs[index]; }
        }
        internal int Length { get { return _cmdArgs.Length; } }

        internal static Args ParseArgs(string usage, string[] cmdArgs)
        {
            return new Args(usage, cmdArgs);
        }

        internal bool ContainsKey(string key)
        {
            return _args.ContainsKey(key);
        }


        private void ParseArgsArray(string[] args)
        {

            int i = 0;
            while (i < args.Length)
            {
                if (args[i][0] == '-' && i + 1 < args.Length && args[i + 1][0] != '-')
                {
                    _args[args[i++]] = args[i];
                }
                else
                {
                    _args[args[i]] = "";
                }
                i++;
            }
        }


        private Args(string usage, string[] cmdArgs)
        {
            _cmdArgs = cmdArgs;
           
            //Log.Info(_args.Count + " argument(s)");
            //foreach (string key in _args.Keys)
            //{
            //    Log.Info(key + "=" + _args[key]);
            //}

            if (_args.ContainsKey("-h")
                || _args.ContainsKey("-?")
                || _args.ContainsKey("/?"))
            {
                Log.Warn(usage);
                Environment.Exit(0);
            }

            // Need to parse args/config file?
            string? argsFilePath;
            if (null != (argsFilePath = GetArgValue("-args", null)))
            {
                string argsExpr = string.Join(
                    " ",
                    File.ReadLines(argsFilePath)
                        .Select(line => line.Trim())
                        .Where(line => !line.StartsWith("#")));

                // Parse loaded config file args
                string[] args = SplitWithQuotes(argsExpr).ToArray();
                ParseArgsArray(args);

                // Parse cmd line args (again), to enable cmd line args to *override* config file args.
                ParseArgsArray(cmdArgs);
            }

        }

        private static string Serialize(
            Dictionary<string, string> dict,
            bool escapeDelimiters,
            char pairDelimiter = ';',
            char keyValueDelimiter = '=')
        {
            var keyValuePairs = new List<string>();

            foreach (var pair in dict)
            {
                // Escape delimiters in keys and values if needed
                string key = (escapeDelimiters)
                        ? pair.Key
                            .Replace(pairDelimiter.ToString(), "\\" + pairDelimiter)
                            .Replace(keyValueDelimiter.ToString(), "\\" + keyValueDelimiter)
                        : pair.Key;
                string value = (escapeDelimiters)
                    ? pair.Value
                        .Replace(pairDelimiter.ToString(), "\\" + pairDelimiter)
                        .Replace(keyValueDelimiter.ToString(), "\\" + keyValueDelimiter)
                    : pair.Value;

                keyValuePairs.Add($"{key}{keyValueDelimiter}{value}");
            }

            return string.Join(pairDelimiter.ToString(), keyValuePairs);
        }

        private static List<string> SplitWithQuotes(string input)
        {
            var tokens = new List<string>();
            bool insideQuotes = false;
            var currentToken = new StringBuilder();
            //bool isQuotedString = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                // Handle opening/closing quotes
                if (c == '"')
                {
                    insideQuotes = !insideQuotes;
                    //if (insideQuotes) isQuotedString = true;
                    continue; // Skip the quote character itself
                }

                // Handle space outside of quotes (end of token)
                if (char.IsWhiteSpace(c) && !insideQuotes)
                {
                    if (currentToken.Length > 0)
                    {
                        //if (isQuotedString)
                        //{
                        //    tokens.Add("\"" + currentToken.ToString() + "\"");
                        //}
                        //else
                        //{
                        tokens.Add(currentToken.ToString());
                        //}
                        //isQuotedString = false;
                        currentToken.Clear();
                    }
                    continue; // Skip the space
                }

                // Add the character to the current token
                currentToken.Append(c);
            }

            // Add the last token if there is one
            if (currentToken.Length > 0)
            {
                //if (isQuotedString)
                //{
                //    tokens.Add("\"" + currentToken.ToString() + "\"");
                //}
                //else
                //{
                tokens.Add(currentToken.ToString());
                //}
            }

            return tokens;
        }

        private string? GetArgValue(string name, string? defaultValue)
        {
            if (_args.ContainsKey(name))
            {
                return _args[name];
            }
            return defaultValue;
        }

        private string GetArgValue(string[] names, string defaultValue)
        {
            foreach (string name in names)
            {
                if (_args.ContainsKey(name))
                {
                    return _args[name];
                }
            }
            return defaultValue;
        }

        private string GetArgValue(string name)
        {
            if (!_args.ContainsKey(name))
            {
                string msg = "Missing Argument '" + name + "'";
                Log.Error(msg);
                throw new MissingFieldException(msg);
            }
            return _args[name];
        }

        private int GetArgValueInt(string name, int defaultValue)
        {
            if (_args.ContainsKey(name))
            {
                return int.Parse(_args[name]);
            }

            return defaultValue;
        }

        private double GetArgValueDouble(string name, double? defaultValue)
        {
            if (_args.ContainsKey(name))
            {
                return double.Parse(_args[name]);
            }

            if (!defaultValue.HasValue)
            {
                string msg = "Missing Argument '" + name + "'";
                Log.Error(msg);
                throw new MissingFieldException(msg);
            }

            return defaultValue.Value;
        }

        private DateTime GetArgValueDateTime(string name, DateTime defaultValue)
        {
            if (_args.ContainsKey(name))
            {
                return DateTime.Parse(_args[name]);
            }
            return defaultValue;
        }
    }
}
