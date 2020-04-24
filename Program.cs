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

        static string helpMessage = "Usage: " + System.Diagnostics.Process.GetCurrentProcess().ProcessName + " [Input Path] [Output Path] [Output Format (sql/txt)]";
        static string lastError;

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

        //TODO: Do something with this. It's just sitting here.
        enum ConVarFlags 
        {
            FCVAR_NONE,
            FCVAR_UNREGISTERED,
            FCVAR_DEVELOPMENTONLY,
            FCVAR_GAMEDLL,
            FCVAR_CLIENTDLL,
            FCVAR_HIDDEN,
            FCVAR_PROTECTED,
            FCVAR_SPONLY,
            FCVAR_ARCHIVE,
            FCVAR_NOTIFY,
            FCVAR_USERINFO,
            FCVAR_CHEAT,
            FCVAR_PRINTABLEONLY,
            FCVAR_UNLOGGED,
            FCVAR_NEVER_AS_STRING,
            FCVAR_REPLICATED,
            FCVAR_DEMO,
            FCVAR_DONTRECORD,
            FCVAR_RELOAD_MATERIALS,
            FCVAR_RELOAD_TEXTURES,
            FCVAR_NOT_CONNECTED,
            FCVAR_MATERIAL_SYSTEM_THREAD,
            FCVAR_ARCHIVE_XBOX,
            FCVAR_ACCESSIBLE_FROM_THREADS,
            FCVAR_SERVER_CAN_EXECUTE,
            FCVAR_SERVER_CANNOT_QUERY,
            FCVAR_CLIENTCMD_CAN_EXECUTE,
            FCVAR_EXEC_DESPITE_DEFAULT,
            FCVAR_INTERNAL_USE,
            FCVAR_ALLOWED_IN_COMPETITIVE,
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
                            conVarString += " - " + FlagsIntToString(conVar.flags);
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
                        if(string.IsNullOrEmpty(conVar.variableName))
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
#if DEBUG
                                Console.WriteLine("Found the following: " + conVar.name);
#endif
                                break;
                            case LineRegexResult.ConVarWithFlags:
                                conVar.flags = 0; //TODO: This.
#if DEBUG
                                Console.WriteLine("Found the following: " + conVar.name);
#endif
                                break;
                            case LineRegexResult.ConVarDescription:
                                conVar.description = ParseString(matches[4], true, false);
#if DEBUG
                                Console.WriteLine("Found the following: " + conVar.name + " with description " + conVar.description);
#endif
                                break;
                            case LineRegexResult.ConVarLimitedInput:

                                conVar.description = ParseString(matches[4], true, false);
                                conVar.usesMinValue = IsTrue(matches[5]);
                                conVar.minValue = ParseString(matches[6], true, false);
                                conVar.usesMaxValue = IsTrue(matches[7]);
                                conVar.maxValue = ParseString(matches[8], true, false);
#if DEBUG
                                Console.WriteLine("Found the following: " + conVar.name + " with a low value of " + conVar.minValue + " and a max value of " + conVar.maxValue);
#endif
                                break;
                            case LineRegexResult.ConVarCallback:

                                conVar.description = ParseString(matches[4], true, false);
                                conVar.executesCallback = true;
                                conVar.callbackName = ParseString(matches[5], true, false);
#if DEBUG
                                Console.WriteLine("Found the following: " + conVar.name + " with a callback of " + conVar.callbackName);
#endif
                                break;
                            case LineRegexResult.ConVarLimitedInputCallback:

                                conVar.description = ParseString(matches[4], true, false);
                                conVar.usesMinValue = IsTrue(matches[5]);
                                conVar.minValue = ParseString(matches[6], true, false);
                                conVar.usesMaxValue = IsTrue(matches[7]);
                                conVar.maxValue = ParseString(matches[8], true, false);
                                conVar.executesCallback = true;
                                conVar.callbackName = ParseString(matches[9], true, false);
#if DEBUG
                                Console.WriteLine("Found the following: " + conVar.name + " with a low value of " + conVar.minValue + " and a max value of " + conVar.maxValue + " with a callback of " + conVar.callbackName);
#endif
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
#if DEBUG
                                Console.WriteLine("Found the following competitive value: " + conVar.name + " with a low value of " + conVar.minValue + " and a max value of " + conVar.maxValue + " with a callback of " + conVar.callbackName);
#endif
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
#if DEBUG       
                    DisplayMessage("Unable to parse line: " + textLine);
#endif          
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
            if (eval == "1")
            {
                return true;
            }
            if (eval == "true")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        static string FlagsIntToString(int flags)
        {
            return "No_Flags";
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
