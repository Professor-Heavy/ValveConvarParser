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
        static string conVarPattern = @"((?:GCConVar|ConVar_ServerBounded|ConVar))\s*(\S*)\s*\(\s*""(.*?)"",\s*""(.*?)""\s*\)";
        static string conVarFlagPattern = @"((?:GCConVar|ConVar_ServerBounded|ConVar))\s*(\S*)\s*\(\s*""(.*?)"",\s*""(.*?)"",(.*?)\s*?\)";
        static string conVarDescriptionPattern = @"((?:GCConVar|ConVar_ServerBounded|ConVar))\s*(\S*)\s*\(\s*""(.*?)"",\s*""(.*?)"",(.*?),\s*""(.*?)""\s*\)";
        static string conVarLimitedPattern = @"((?:GCConVar|ConVar_ServerBounded|ConVar))\s*(\S*)\s*\(\s*""(.*?)"",\s*""(.*?)"",\s*(.*?),\s*""(.*?)"",\s*(.*?),\s*(.*?),\s*(.*?),\s*(.*?)\s\)";
        static string conVarCallbackPattern = @"((?:GCConVar|ConVar_ServerBounded|ConVar))\s*(\S*)\s*\(\s*""(.*?)"",\s*""(.*?)"",(.*?),\s*""(.*?)"",\s*(.*?)\s*\)";
        static string conVarLimitedCallbackPattern = @"((?:GCConVar|ConVar_ServerBounded|ConVar))\s*(\S*)\s*\(\s*""(.*?)"",\s*""(.*?)"",\s*(.*?),\s*""(.*?)"",\s*(.*?),\s*(.*?),\s*(.*?),\s*(.*?),\s*(.*?)\s\)";

        //static string conVarLimitedCompPattern = @"((?:ConVar_ServerBounded|ConVar))\s*(\S*)\s*\(\s*""(.*?)"",\s*""(.*?)"",\s*(.*?),\s*""(.*?)"",\s*(.*?),\s*(.*?),\s*(.*?),\s*(.*?),\s*(.*?)\s\)";

        //REGEX CHECKS
        static Regex directiveStartRegex = new Regex(directiveStartPattern, RegexOptions.IgnoreCase);
        static Regex directiveEndRegex = new Regex(directiveEndPattern, RegexOptions.IgnoreCase);

        static Regex baseConVarRegex = new Regex(baseConVarPattern, RegexOptions.IgnoreCase);
        static Regex conVarRegex = new Regex(conVarPattern, RegexOptions.IgnoreCase);
        static Regex conVarFlagRegex = new Regex(conVarFlagPattern, RegexOptions.IgnoreCase);
        static Regex conVarDescriptionRegex = new Regex(conVarDescriptionPattern, RegexOptions.IgnoreCase);
        static Regex conVarLimitedRegex = new Regex(conVarLimitedPattern, RegexOptions.IgnoreCase);
        static Regex conVarCallbackRegex = new Regex(conVarCallbackPattern, RegexOptions.IgnoreCase);
        static Regex conVarLimitedCallbackRegex = new Regex(conVarLimitedCallbackPattern, RegexOptions.IgnoreCase);

        //static Regex conVarLimitedCompRegex = new Regex(conVarLimitedCompPattern, RegexOptions.IgnoreCase);


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

            if(args.Length == 0)
            {
                DisplayMessage(helpMessage);
                return;
            }

            if(args[0] == "/?")
            {
                DisplayMessage(helpMessage);
                return;
            }

            filePath = args[0];
            
            if(args.Length == 1)
            {
                DisplayMessage("No output path specified.");
                return;
            }
            else //Verify output here. Maybe move this into its own function when it's done?
            {

            }

            if(args.Length == 2)
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
            catch(ArgumentException)
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
            switch(format)
            {
                case ResultFormat.PLAINTEXT:
                    try
                    {
                        FileStream file = File.Create(path + ".txt");
                        foreach (ConVar conVar in conVarList)
                        {
                            string conVarString = string.Empty;
                            conVarString += conVar.name;
                            conVarString += " - " + ParseFlags(conVar.flags);
                            if(string.IsNullOrEmpty(conVar.description)) //Maybe work a little better with the layout in the future...?
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
            foreach(string path in directoryFiles)
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
            if(extension != ".cpp" && extension != ".h")
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
                                conVar.description = conVarDescriptionRegex.Match(text).Groups[6].Value;
#if DEBUG
                                Console.WriteLine("Found the following: " + conVar.name + " with description " + conVar.description);
#endif
                                break;
                            case LineRegexResult.ConVarLimitedInput:

                                conVar.description = conVarLimitedRegex.Match(text).Groups[6].Value;
                                conVar.usesMinValue = IsTrue(conVarLimitedRegex.Match(text).Groups[7].Value);
                                conVar.minValue = conVarLimitedRegex.Match(text).Groups[8].Value;
                                conVar.usesMaxValue = IsTrue(conVarLimitedRegex.Match(text).Groups[9].Value);
                                conVar.maxValue = conVarLimitedRegex.Match(text).Groups[10].Value;
#if DEBUG
                                Console.WriteLine("Found the following: " + conVar.name + " with a low value of " + conVar.minValue + " and a max value of " + conVar.maxValue);
#endif
                                break;
                            case LineRegexResult.ConVarCallback:

                                conVar.description = conVarCallbackRegex.Match(text).Groups[6].Value;
                                conVar.executesCallback = true;
                                conVar.callbackName = conVarCallbackRegex.Match(text).Groups[7].Value;
#if DEBUG
                                Console.WriteLine("Found the following: " + conVar.name + " with a callback of " + conVar.callbackName);
#endif
                                break;
                            case LineRegexResult.ConVarLimitedInputCallback:

                                conVar.description = conVarLimitedCallbackRegex.Match(text).Groups[6].Value;
                                conVar.usesMinValue = IsTrue(conVarLimitedCallbackRegex.Match(text).Groups[7].Value);
                                conVar.minValue = conVarLimitedCallbackRegex.Match(text).Groups[8].Value;
                                conVar.usesMaxValue = IsTrue(conVarLimitedCallbackRegex.Match(text).Groups[9].Value);
                                conVar.maxValue = conVarLimitedCallbackRegex.Match(text).Groups[10].Value;
                                conVar.executesCallback = true;
                                conVar.callbackName = conVarLimitedCallbackRegex.Match(text).Groups[11].Value;
#if DEBUG
                                Console.WriteLine("Found the following: " + conVar.name + " with a low value of " + conVar.minValue + " and a max value of " + conVar.maxValue + " with a callback of " + conVar.callbackName);
#endif
                                break;
                            case LineRegexResult.ConVarLimitedInputComp:
                                //TODO: Todo, or not todo?
                                //I'm considering ignoring this all together, it depends on if other code uses it or not.
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

            //I'm also sacrificing performance for my own lack of knowledge of Regex.
            //The code starts with the really long ones, and works its way down the list. This is because everything is so damn complicated in Regex, and having comma separators made this easier.
            //Maybe in future, I should look at splitting the string. Only problem is that splitting it might also split the inside of any descriptions.
            //Uhh...
            //TODO: Better ConVar type checks. High priority.

            if(baseConVarRegex.Match(textLine).Success)
            {
                if (conVarLimitedCallbackRegex.Match(textLine).Success)
                {
                    return LineRegexResult.ConVarLimitedInputCallback;
                }
                if (conVarLimitedRegex.Match(textLine).Success)
                {
                    return LineRegexResult.ConVarLimitedInput;
                }
                if (conVarCallbackRegex.Match(textLine).Success)
                {
                    return LineRegexResult.ConVarCallback;
                }
                if (conVarDescriptionRegex.Match(textLine).Success)
                {
                    return LineRegexResult.ConVarDescription;
                }
                if (conVarFlagRegex.Match(textLine).Success)
                {
                    return LineRegexResult.ConVar;
                }
                if (conVarRegex.Match(textLine).Success)
                {
                    return LineRegexResult.ConVar;
                }
                else
                {
#if DEBUG
                    DisplayMessage("Unable to parse line: " + textLine);
#endif
                    conVarFailures++;
                }
            }
            else if(directiveStartRegex.Match(textLine).Success)
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
            if(eval == "1")
            {
                return true;
            }
            if(eval == "true")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        
        static string ParseFlags(int flags)
        {
            return "No_Flags";
        }
    }
}
