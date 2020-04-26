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

        static int conVarCount = 0;
        enum ResultFormat
        {
            SQL,
            PLAINTEXT
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

            DisplayMessage("\nTotal ConVars written to file: " + conVarCount);
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
                            conVarString += ": " + conVar.initialValue;
                            conVarString += " -" + FlagsToString(conVar.flags);
                            if (string.IsNullOrEmpty(conVar.description)) //Maybe work a little better with the output formatting in the future...?
                            {
                                conVarString += " - No description";
                            }
                            else
                            {
                                conVarString += " - " + conVar.description;
                            }
                            conVarString += conVar.symbolRequired != Symbol.NONE ? " - Note: Only used if " + conVar.symbolRequired.ToString() + " is defined." : string.Empty;

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

        static void WriteToDebug(List<ConVar> conVarList, string path)
        {
            DisplayMessage(string.Format("Found {0} ConVar(s) in file {1}:", conVarList.Count, Path.GetFileNameWithoutExtension(path)));
            for(int i = 0; i < conVarList.Count; i++)
            {
                ConVar conVar = conVarList[i];
                switch(conVar.conVarType)
                {
                    case ConVarType.ConVar:
                    case ConVarType.ConVarWithFlags:
                        DisplayMessage(string.Format("\t-{0}: {1} - {2}", i, conVar.name, conVar.initialValue, conVar.flags));
                        break;
                    case ConVarType.ConVarDescription:
                        DisplayMessage(string.Format("\t-{0}: {1} - Default Value: {2} - Flags: {3} - \"{4}\"",
                            i,
                            conVar.name,
                            conVar.initialValue,
                            conVar.flags,
                            conVar.description));
                        break;
                    case ConVarType.ConVarCallback:
                        DisplayMessage(string.Format("\t-{0}: {1} - Default Value: {2} - Flags: {3} - \"{4}\" - Callback Function Name: {5}",
                            i,
                            conVar.name,
                            conVar.initialValue,
                            conVar.flags,
                            conVar.description,
                            conVar.callbackName));
                        break;
                    case ConVarType.ConVarLimitedInput:
                        DisplayMessage(string.Format("\t-{0}: {1} - Default Value: {2} - Flags: {3} - \"{4}\" - Uses Min Value: {5} - Min Value: {6} - Uses Max Value: {7} - Max Value: {8}",
                            i,
                            conVar.name,
                            conVar.initialValue,
                            conVar.flags,
                            conVar.description,
                            conVar.usesMinValue,
                            conVar.minValue,
                            conVar.usesMaxValue,
                            conVar.maxValue));
                        break;
                    case ConVarType.ConVarLimitedInputCallback:
                        DisplayMessage(string.Format("\t-{0}: {1} - Default Value: {2} - Flags: {3} - \"{4}\" - Uses Min Value: {5} - Min Value: {6} - Uses Max Value: {7} - Max Value: {8} - Callback Function Name: {9}",
                            i,
                            conVar.name,
                            conVar.initialValue,
                            conVar.flags,
                            conVar.description,
                            conVar.usesMinValue,
                            conVar.minValue,
                            conVar.usesMaxValue,
                            conVar.maxValue,
                            conVar.callbackName));
                        break;
                    case ConVarType.ConVarLimitedInputComp:
                        DisplayMessage(string.Format("\t-{0}: {1} - Default Value: {2} - Flags: {3} - \"{4}\" - Uses Min Value: {5} - Min Value: {6} - Uses Max Value: {7} - Max Value: {8} - Uses Comp Min Value: {9} - Comp Min Value: {10} - Uses Comp Max Value: {11} - Comp Max Value: {12} - Callback Function Name: {13}",
                            i,
                            conVar.name,
                            conVar.initialValue,
                            conVar.flags,
                            conVar.description,
                            conVar.usesMinValue,
                            conVar.minValue,
                            conVar.usesMaxValue,
                            conVar.maxValue,
                            conVar.usesCompetitiveMinValue,
                            conVar.competitiveMinValue,
                            conVar.usesCompetitiveMaxValue,
                            conVar.competitiveMaxValue,
                            conVar.callbackName));
                        break;
                }
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
                FileReader file = new FileReader();
                file.LoadFile(path);
                file.ReadWholeFile();
                List<ConVar> fileConVars = file.GetResults();
                conVarList.AddRange(fileConVars);
                conVarCount += fileConVars.Count;
                if(verboseOutput && fileConVars.Count > 0)
                {
                    WriteToDebug(fileConVars, path);
                }
            }
        }
        public static ConVarFlags StringsToFlag(string flags)
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
            else if(flag == "NULL")
            {
                return ConVarFlags.FCVAR_NONE;
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
                return " FCVAR_NONE";
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
