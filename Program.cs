using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ValveConvarParsingSystem
{
    class Program
    {
        //TODO: Spread this program out a little bit in future builds. Could get a tad too large if left unchecked.

        static string helpMessage = "Usage: " + System.Diagnostics.Process.GetCurrentProcess().ProcessName + " [Input Path] [Output Path] [Output Format (sql/txt)] [/verbose (optional)]";
        static string lastError;

        static bool verboseOutput = false;

        static List<ConVar> conVarList = new List<ConVar>();

        //REGEX PATTERNS
        static string directiveStartPattern = @"#ifdef\sSTAGING_ONLY";
        static string directiveEndPattern = "#endif";

        //TODO: The insane amount of patterns here is because there's apparently no way to check for multiple ',' characters... Is there?
        static string baseConVarPattern = @"((?:GCConVar|ConVar_ServerBounded|ConVar))\s*(\S*)\s*\(\s*""(.*?)"",\s*""(.*?)"""; //The ConVar pattern that will return true for ConVar ALL matches.

        static string conVarArgumentSplitPattern = @"(?!\B""[^""]*)(?:,|\(|\)\;)(?![^""]*""\B)(?!\*\\\B)";

        //REGEX CHECKS
        static Regex directiveStartRegex = new Regex(directiveStartPattern, RegexOptions.IgnoreCase);
        static Regex directiveEndRegex = new Regex(directiveEndPattern, RegexOptions.IgnoreCase);

        static Regex baseConVarRegex = new Regex(baseConVarPattern, RegexOptions.IgnoreCase);

        static Regex conVarArgumentSplitRegex = new Regex(conVarArgumentSplitPattern, RegexOptions.IgnoreCase);


        static int conVarCount = 0;
        static int conVarFailures = 0;
        enum ResultFormat
        {
            SQL,
            PLAINTEXT
        }

        enum LineRegexResult
        {
            None, //No regex match.
            ConVar, //Simple ConVar.
            ConVarWithFlags, //ConVar with flag values.
            ConVarDescription, //ConVar with description.
            ConVarCallback, //ConVar with description and callback.
            ConVarLimitedInput, //ConVar with description and value limits.
            ConVarLimitedInputCallback, //ConVar with description, value limits, and a callback.
            ConVarLimitedInputComp, //ConVar with description, value limits, Competitive Mode value limits, and a callback.
            DirectiveStart, //#ifdef directive.
            DirectiveEnd //#endif directive.
        }

        static void Main(string[] args)
        {
            string filePath;
            ResultFormat format = ResultFormat.PLAINTEXT;

            if (args.Length == 0)
            {
                DisplayMessage(helpMessage);
                return;
            }

            if (args[0] == "/?")
            {
                DisplayMessage(helpMessage);
                return;
            }

            filePath = args[0];

            if (args.Length == 1)
            {
                DisplayMessage("No output path specified.");
                return;
            }
            else //Verify output here. Maybe move this into its own function when it's done?
            {

            }

            if (args.Length == 2)
            {
                DisplayMessage("No output format specified. Assuming txt.");
            }
            else
            {
                switch (args[2])
                {
                    case "sql":
                    case ".sql":
                        DisplayMessage("SQL format is not yet supported. Sorry. :(");
                        return;
                    //format = ResultFormat.SQL;
                    case "txt":
                    case ".txt":
                    case "text":
                        format = ResultFormat.PLAINTEXT;
                        break;
                    default:
                        DisplayMessage("Output Format argument is not valid. \n" + helpMessage);
                        return;
                }
            }

            if(args.Length == 4)
            {
                if(args[3] == "/verbose")
                {
                    verboseOutput = true;
                }
            }
            string[] directoryFiles = GetFilesInDirectory(args[0]);
            if (directoryFiles != null)
            {
                RecursivelyParseFiles(directoryFiles);
            }
            else
            {
                DisplayMessage(lastError);
            }

            OrderByName();
            WriteToOutput(format, args[1]);

            DisplayMessage("Total ConVars written to file: " + conVarCount);
            DisplayMessage("ConVars failed: " + conVarFailures);
        }

        static void DisplayMessage(string message)
        {
            Console.WriteLine(message);
        }

        static string[] GetFilesInDirectory(string path)
        {
            try
            {
                return Directory.GetFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly);
            }
            catch (ArgumentException)
            {
                lastError = "Directory path invalid.";
                return null;
            }
            catch (PathTooLongException)
            {
                lastError = "The directory path specified was too long.";
                return null;
            }
            catch (IOException)
            {
                lastError = "The directory " + path + " is not valid. Please specify a folder directory.";
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                lastError = "The directory " + path + " could not be read due to insufficient application permissions.\nTry running the application as Administrator.";
                return null;
            }
            catch (Exception e)
            {
                //Uhhhhhhhhhhhhhh???
                lastError = "An unknown exception occurred while trying to get files a directory. Error details: " + e.Message;
                return null;
            }
        }

        static void OrderByName()
        {
            IOrderedEnumerable<ConVar> orderedList = conVarList.OrderBy(cvar => cvar.name);
            conVarList = orderedList.ToList();
        }

        static void WriteToOutput(ResultFormat format, string path)
        {
            switch (format)
            {
                case ResultFormat.PLAINTEXT:
                    try
                    {
                        FileStream file = File.Create(path + ".txt");
                        foreach (ConVar conVar in conVarList)
                        {
                            string conVarString = string.Empty;
                            conVarString += conVar.name;
                            conVarString += " -" + FlagsToString(conVar.flags);
                            if (string.IsNullOrEmpty(conVar.description)) //Maybe work a little better with the layout in the future...?
                            {
                                conVarString += " - No description";
                            }
                            else
                            {
                                conVarString += " - " + conVar.description;
                            }
                            conVarString += conVar.isStagingOnly ? " - Note: Hidden from release builds" : string.Empty;

                            conVarString += "\n";
                            byte[] bytes = new UTF8Encoding(true).GetBytes(conVarString);
                            file.Write(bytes, 0, bytes.Length);
                        }
                    }
                    catch (ArgumentException)
                    {
                        DisplayMessage("Output directory path invalid.");
                    }
                    catch (PathTooLongException)
                    {
                        DisplayMessage("The output directory path specified was too long.");
                    }
                    catch (IOException)
                    {
                        DisplayMessage("The output directory " + path + " is not valid. Please specify a folder directory.");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        DisplayMessage("The output directory " + path + " could not be written to due to insufficient application permissions.\nTry running the application as Administrator.");
                    }
                    catch (Exception e)
                    {
                        DisplayMessage("An unknown exception occurred while trying to write the output file to the directory. Error details: " + e.Message);
                    }
                    break;
                case ResultFormat.SQL:
                    break;
            }
        }

        static void RecursivelyParseFiles(string[] directoryFiles)
        {
            foreach (string path in directoryFiles)
            {
                if (Directory.Exists(path)) //This is a directory.
                {
                    RecursivelyParseFiles(GetFilesInDirectory(path));
                }
                else //This is a file.
                {
                    ParseFile(path);
                }
            }
        }

        static void ParseFile(string path)
        {
            string extension = Path.GetExtension(path);
            if (extension != ".cpp" && extension != ".h")
            {
                return;
            }
            else
            {
                //NOTE: The current method reads one line after another. I'm not sure of any occurrences,
                //but isn't it possible that some ConVar values are across multiple lines?

                //TODO: ...Yes, apparently some ConVar values are across multiple lines.

                string text;
                bool insideStagingDirective = false;
                StreamReader file = new StreamReader(path);
                while ((text = file.ReadLine()) != null)
                {
                    LineRegexResult result = PerformRegexChecks(text);

                    if (result < LineRegexResult.DirectiveStart && result != 0) //Result was a ConVar.
                    {
                        ConVar conVar = new ConVar();
                        conVar.variableName = baseConVarRegex.Match(text).Groups[2].Value;
                        if (string.IsNullOrEmpty(conVar.variableName))
                        {
                            conVar.variableName = "[NoVariable]";
                        }
                        conVar.name = baseConVarRegex.Match(text).Groups[3].Value;
                        if (string.IsNullOrEmpty(conVar.name))
                        {
                            conVar.name = "[EmptyConVar]";
                        }
                        conVar.initialValue = baseConVarRegex.Match(text).Groups[4].Value;
                        conVar.flags = 0;

                        conVar.isStagingOnly = insideStagingDirective;
                        conVar.conVarType = baseConVarRegex.Match(text).Groups[1].Value;

                        string[] matches = Regex.Split(text, conVarArgumentSplitPattern); //Unnecessary repetition. In fact, is a LineRegexResult return even necessary???
                        switch (result)
                        {
                            case LineRegexResult.ConVar:
                                if (verboseOutput)
                                {
                                    DisplayMessage(string.Format("Found the ConVar: {0}. Default Value: {1}.",
                                        conVar.name,
                                        conVar.initialValue
                                        ));
                                }
                                break;
                            case LineRegexResult.ConVarWithFlags:
                                conVar.flags = StringsToFlag(matches[3]);
                                if (verboseOutput)
                                {
                                    DisplayMessage(string.Format("Found the ConVar: {0}. Default Value: {1}. Flags: {2}.",
                                        conVar.name,
                                        conVar.initialValue,
                                        FlagsToString(conVar.flags)
                                        ));
                                }
                                break;
                            case LineRegexResult.ConVarDescription:
                                conVar.description = ParseString(matches[4], true, false);
                                if (verboseOutput)
                                {
                                    DisplayMessage(string.Format("Found the ConVar: {0}. Default Value: {1}. Flags: {2}. Description: {3}.",
                                        conVar.name,
                                        conVar.initialValue,
                                        FlagsToString(conVar.flags),
                                        conVar.description
                                        ));
                                }
                                break;
                            case LineRegexResult.ConVarLimitedInput:

                                conVar.description = ParseString(matches[4], true, false);
                                conVar.usesMinValue = IsTrue(matches[5]);
                                conVar.minValue = ParseString(matches[6], true, false);
                                conVar.usesMaxValue = IsTrue(matches[7]);
                                conVar.maxValue = ParseString(matches[8], true, false);
                                if (verboseOutput)
                                {
                                    DisplayMessage(string.Format("Found the ConVar: {0}. Default Value: {1}. Flags: {2}. Description: {3}. Minimum/Maximum values: {4}/{5}.",
                                        conVar.name,
                                        conVar.initialValue,
                                        FlagsToString(conVar.flags),
                                        conVar.description,
                                        conVar.minValue,
                                        conVar.maxValue
                                        ));
                                }
                                break;
                            case LineRegexResult.ConVarCallback:

                                conVar.description = ParseString(matches[4], true, false);
                                conVar.executesCallback = true;
                                conVar.callbackName = ParseString(matches[5], true, false);
                                if (verboseOutput)
                                {
                                    DisplayMessage(string.Format("Found the ConVar: {0}. Default Value: {1}. Flags: {2}. Description: {3}. Callback name: {4}.",
                                        conVar.name,
                                        conVar.initialValue,
                                        FlagsToString(conVar.flags),
                                        conVar.description,
                                        conVar.callbackName
                                        ));
                                }
                                break;
                            case LineRegexResult.ConVarLimitedInputCallback:

                                conVar.description = ParseString(matches[4], true, false);
                                conVar.usesMinValue = IsTrue(matches[5]);
                                conVar.minValue = ParseString(matches[6], true, false);
                                conVar.usesMaxValue = IsTrue(matches[7]);
                                conVar.maxValue = ParseString(matches[8], true, false);
                                conVar.executesCallback = true;
                                conVar.callbackName = ParseString(matches[9], true, false);
                                if (verboseOutput)
                                {
                                    DisplayMessage(string.Format("Found the ConVar: {0}. Default Value: {1}. Flags: {2}. Description: {3}. Minimum/Maximum values: {4}/{5}. Callback name: {6}.",
                                        conVar.name,
                                        conVar.initialValue,
                                        FlagsToString(conVar.flags),
                                        conVar.description,
                                        conVar.minValue,
                                        conVar.maxValue,
                                        conVar.callbackName
                                        ));
                                }
                                break;
                            case LineRegexResult.ConVarLimitedInputComp:
                                conVar.description = ParseString(matches[4], true, false);
                                conVar.usesMinValue = IsTrue(matches[5]);
                                conVar.minValue = ParseString(matches[6], true, false);
                                conVar.usesMaxValue = IsTrue(matches[7]);
                                conVar.maxValue = ParseString(matches[8], true, false);
                                conVar.usesCompetitiveMinValue = IsTrue(matches[9]);
                                conVar.competitiveMinValue = ParseString(matches[10], true, false);
                                conVar.usesCompetitiveMaxValue = IsTrue(matches[11]);
                                conVar.competitiveMaxValue = ParseString(matches[12], true, false);
                                conVar.executesCallback = true;
                                conVar.callbackName = ParseString(matches[13], true, false);
                                if (verboseOutput)
                                {
                                    DisplayMessage(string.Format("Found the ConVar: {0}. Default Value: {1}. Flags: {2}. Description: {3}. Minimum/Maximum values: {4}/{5}. Competitive Minimum/Maximum Values: {6}/{7}. Callback name: {8}.",
                                        conVar.name,
                                        conVar.initialValue,
                                        FlagsToString(conVar.flags),
                                        conVar.description,
                                        conVar.minValue,
                                        conVar.maxValue,
                                        conVar.competitiveMinValue,
                                        conVar.competitiveMaxValue,
                                        conVar.callbackName
                                        ));
                                }
                                break;
                        }
                        conVarList.Add(conVar);
                        conVarCount++;
                    }
                    else if (result >= LineRegexResult.DirectiveStart) //Result was a Directive.
                    {
                        switch (result)
                        {
                            case LineRegexResult.DirectiveStart:
                                insideStagingDirective = true;
                                break;
                            case LineRegexResult.DirectiveEnd:
                                insideStagingDirective = false;
                                break;
                        }
                    }
                }
#if DEBUG
                if (insideStagingDirective)
                {
                    //I don't even think this even deserves to be here, it can only happen if the parser doesn't detect the directive, which is super rare.
                    DisplayMessage("Syntax error in code, the #ifdef STAGING_ONLY directive didn't close?!");
                }
#endif
            }
        }

        static LineRegexResult PerformRegexChecks(string textLine)
        {
            //TODO: Real meaty checks right about here...
            if (baseConVarRegex.Match(textLine).Success)
            {
                string[] results = Regex.Split(textLine, conVarArgumentSplitPattern); //Find any commas that are not in "s
                //NOTE: This code matches any that have been commented out, which happens rarely in the code.

                int argumentCount = results.Length - 1;

                //Maybe it's possible to use the enum int values so that I'm not running through so many of these.
                if (argumentCount == 3)
                {
                    return LineRegexResult.ConVar;
                }
                if (argumentCount == 4)
                {
                    return LineRegexResult.ConVarWithFlags;
                }
                if (argumentCount == 5)
                {
                    return LineRegexResult.ConVarDescription;
                }
                if (argumentCount == 6)
                {
                    return LineRegexResult.ConVarCallback;
                }
                if (argumentCount == 9)
                {
                    return LineRegexResult.ConVarLimitedInput;
                }
                if (argumentCount == 10)
                {
                    return LineRegexResult.ConVarLimitedInputCallback;
                }
                if (argumentCount == 14)
                {
                    return LineRegexResult.ConVarLimitedInputComp;
                }
                else
                {
                    if (verboseOutput)
                    {
                        DisplayMessage("Unable to parse line: " + textLine);
                    }
                    conVarFailures++;
                }
            }
            else if (directiveStartRegex.Match(textLine).Success)
            {
                return LineRegexResult.DirectiveStart;
            }
            else if (directiveEndRegex.Match(textLine).Success)
            {
                return LineRegexResult.DirectiveEnd;
            }

            return LineRegexResult.None;
        }

        static bool IsTrue(string eval)
        {
            eval = ParseString(eval, true, false);
            if(bool.TryParse(eval, out bool result))
            {
                return result;
            }
            else
            {
#if DEBUG
                Console.WriteLine("An attempt to parse an bool value went wrong. Very bad.");
#endif
                return false;
            }
        }

        static ConVarFlags StringsToFlag(string flags)
        {
            ConVarFlags bitValue = 0;
            string returnString = ParseString(flags, true, false);
            string[] stringFlagArray = returnString.Split('|')
                .Select(str => str.Trim()).ToArray();
            for(int i = 0; i < stringFlagArray.Length; i++)
            {
                ConVarFlags flagValue = FlagToEnum(stringFlagArray[i]);
                if(flagValue != ConVarFlags.Default)
                {
                    bitValue |= flagValue;
                }
                else
                {
                    return ConVarFlags.Default;
                }
            }
            return bitValue;
        }

        static ConVarFlags FlagToEnum(string flag)
        {
            if(Enum.TryParse(flag, out ConVarFlags result))
            {
                return result;
            }
            else
            {
                return ConVarFlags.Default;
            }
        }

        static string FlagsToString(ConVarFlags flags)
        {
            //Hooh boy.

            //While it IS true that this basically returns the initial strings, having the flags as their own type in the first place
            //makes future implementations much more smoother. This is most likely going to change.

            if(flags == 0)
            {
                return " FCVAR_NONE ";
            }
            if(flags == ConVarFlags.Default)
            {
                return " Flag parsing error. ";
            }
            string returnString = string.Empty;
            foreach(int i in Enum.GetValues(typeof(ConVarFlags)))
            {
                if(flags.HasFlag((ConVarFlags)i))
                {
                    if(returnString != string.Empty)
                    {
                        returnString += " |";
                    }
                    returnString += " " + Enum.GetName(typeof(ConVarFlags), i);
                }
            }
            return returnString;
        }

        static string ParseString(string str, bool removeWhiteSpace, bool removeQuotes)
        {
            string returnString = str;
            if(removeWhiteSpace)
            {
                returnString = str.Trim();
            }
            if(removeQuotes)
            {
                returnString = str.Trim('"');
            }
            return returnString;
        }
    }
}
