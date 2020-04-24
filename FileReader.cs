using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ValveConvarParsingSystem
{
    class FileReader
    {
        //File information.
        private string fileContents;
        private string currentPath;
        private int fileLength;

        //Reader information.
        private int currentPosition;
        private char currentCharacter;
        public bool readFinished;
        private ConVar currentConVar;

        //Special characters and strings to look out for.
        private const char directiveChar = '#';
        private const char commentChar = '/';
        private const char assignmentChar = '=';

        //Miscellaneous information.
        private List<ConVar> conVars = new List<ConVar>();

        private static readonly string[] conVarClasses = new string[]
        {
            "ConVar",
            "ConVar_ServerBounded",
            "GCConVar"
        };
        private static readonly string[] directives = new string[]
        {
            "ifdef",
            "elseif",
            "endif"
        };
        private static readonly string[] symbols = new string[]
        {
            "None",
            "STAGING_ONLY",
            "GAME_DLL"
        };

        private Symbol currentSymbol;
        private bool inMultiLineComment;

        public void LoadFile(string path)
        {
            using (StreamReader streamReader = new StreamReader(path, Encoding.UTF8))
            {
                fileContents = streamReader.ReadToEnd();
            }
            currentPath = path;
            fileLength = fileContents.Length;
        }

        private char ReadCharacter(int position)
        {
            if (position < fileLength)
            {
                return fileContents[position];
            }
            else
            {
                readFinished = true;
                return '\0'; //TODO: There's probably a better way to go about doing this.
            }
        }

        public void ReadWholeFile()
        {
            if (currentPath == null)
            {
                throw new InvalidOperationException("Attempted to read through a file without initializing first.");
            }
            while (currentPosition < fileLength)
            {
                currentCharacter = fileContents[currentPosition];
                if (!CheckForSpecialCharacters(currentCharacter))
                {
                    CheckForConVarPresence(currentCharacter);
                }
                currentPosition++;
            }
        }

        private void SkipToEndOfLine()
        {
            bool endOfLine = false;
            while (!endOfLine)
            {
                if (currentPosition >= fileLength || fileContents[currentPosition] == '\n') //WARNING: Potential for going out of the file's range!!!
                {
                    endOfLine = true;
                }
                currentPosition++;
            }
        }

        private void AdvanceReader(int steps = 1)
        {
            currentPosition += steps;
            if(currentPosition < fileLength)
            {
                currentCharacter = fileContents[currentPosition];
            }
        }

        private bool CheckForSpecialCharacters(char charToCheck)
        {
            //TODO: Directive checks do not account for #if defined( STAGING_ONLY ) || defined( _DEBUG )
            if (inMultiLineComment)
            {
                if (charToCheck == '*')
                {
                    if (ReadCharacter(currentPosition + 1) == commentChar)
                    {
                        inMultiLineComment = false; //Finally break out of this multiline hell. (See past commits)
                        return true;
                    }
                }
                return false; //No point in continuing.
            }
            if (charToCheck == directiveChar)
            {
                //TODO: To ensure efficiency, maybe make this ONLY execute on the start of the line. If it's the start of a line, go crazy, otherwise, ignore.
                for (int i = 0; i < directives.Length; i++)
                {
                    //Looping through the list of directives, check if the directive matches any symbols in the list.
                    if (GetRangeOfString(currentPosition + 1, directives[i].Length) == directives[i])
                    {
                        if(directives[i] == "endif")
                        {
                            currentSymbol = Symbol.NONE;
                            SkipToEndOfLine();
                            return true;
                        }
                        AdvanceReader(directives[i].Length+1);
                        if (char.IsWhiteSpace(ReadCharacter(currentPosition)))
                        {
                            for (int j = 0; j < symbols.Length; j++)
                            {
                                if (GetRangeOfString(currentPosition + 1, symbols[j].Length) == symbols[j])
                                {
                                    currentSymbol = (Symbol)j;
                                    SkipToEndOfLine();
                                    return true;
                                }
                            }
                        }
                        else
                        {
                            return false;
                        }

                    }
                }
            }
            else if (charToCheck == assignmentChar)
            {
                if (ReadCharacter(currentPosition + 1) != assignmentChar && ReadCharacter(currentPosition - 1) != assignmentChar)
                {
                    //TODO: This was a strange solution. But the complicated, fully justifiable reasoning behind this is as follows:
                    //ConVar constructors cannot return anything.
                    SkipToEndOfLine();
                    return true;
                }
            }
            else if (charToCheck == commentChar)
            {
                if (ReadCharacter(currentPosition + 1) == commentChar)
                {
                    SkipToEndOfLine();
                    return true;
                }
                if (ReadCharacter(currentPosition + 1) == '*')
                {
                    inMultiLineComment = true;
                    return true;
                }
            }
            return false;
        }

        private bool CheckForConVarPresence(char charToCheck)
        {
            if (currentPosition != 0)
            {
                if (char.IsWhiteSpace(fileContents[currentPosition - 1])) //If the character before this one is a whitespace or newline, that means this one is in a league of its own(...?)
                {
                    for (int i = 0; i < conVarClasses.Length; i++)
                    {
                        //Check if the character could at least be one of them.
                        //TODO: Redundancy in ConVar and ConVar_ServerBounded!
                        if (charToCheck == conVarClasses[i][0])
                        {
                            if (GetRangeOfString(currentPosition, conVarClasses[i].Length) == conVarClasses[i])
                            {
                                AdvanceReader(conVarClasses[i].Length);
                                if (char.IsWhiteSpace(ReadCharacter(currentPosition)))
                                {
                                    //Believe it or not, this still has false checks.
                                    AdvanceReader(); //Advance one step ahead for good luck.
                                    ReadConVar(fileContents[currentPosition]);
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        private void ReadConVar(char charToCheck)
        {
            bool checkComplete = false;
            while (!checkComplete)
            {
                SeekToConVarName();
                string conVarName = ReadConVarName();
                if (conVarName != string.Empty)
                {
                    currentConVar = new ConVar();
                    currentConVar.name = conVarName;
                    AdvanceReader(); //Advance to get inside the bracket.
                    string[] arguments = ReadConVarParameters();

                    currentConVar.symbolEnforced = currentSymbol;
                    currentConVar.conVarType = GetConVarTypeFromArgumentList(arguments);
                    ParseConVarArguments(arguments);
                    checkComplete = true;
                    conVars.Add(currentConVar);
                }
                else
                {
                    return;
                }
            }
        }

        private void ParseConVarArguments(string[] arguments)
        {
            if((int)currentConVar.conVarType > 2) //Must have flags.
            {
                currentConVar.flags = Program.StringsToFlag(arguments[2]); //What in god's name- am I really doing this?
            }
            if ((int)currentConVar.conVarType > 3) //Must have a description.
            {
                currentConVar.description = arguments[3];
            }
            if ((int)currentConVar.conVarType == 5) //Has a callback, but nothing else.
            {
                currentConVar.callbackName = arguments[4];
            }
            if ((int)currentConVar.conVarType >= 8) //Has min and max values.
            {
                currentConVar.usesMinValue = ParseBool(arguments[4]);
                currentConVar.minValue = arguments[5];
                currentConVar.usesMaxValue = ParseBool(arguments[6]);
                currentConVar.maxValue = arguments[7];
            }
            if ((int)currentConVar.conVarType == 9) //Has min and max values and a callback.
            {
                currentConVar.callbackName = arguments[8];
            }
            if ((int)currentConVar.conVarType == 13) //Has comp min and max values and a callback.
            {
                currentConVar.usesCompetitiveMinValue = ParseBool(arguments[8]);
                currentConVar.competitiveMinValue = arguments[9];
                currentConVar.usesCompetitiveMinValue = ParseBool(arguments[10]);
                currentConVar.competitiveMaxValue = arguments[11];
                currentConVar.callbackName = arguments[12];
            }
        }

        private bool ParseBool(string value)
        {
            if(bool.TryParse(value, out bool result))
            {
                return result;
            }
            else
            {
                return value != "0"; //Fallback.
            }
        }

        private void SeekToConVarName()
        {
            bool seeking = true;
            while (seeking)
            {
                if (char.IsWhiteSpace(ReadCharacter(currentPosition)))
                {
                    AdvanceReader();
                }
                else
                {
                    seeking = false;
                }
            }
        }

        private string ReadConVarName()
        {
            bool reading = true;
            int nameLength = 0;
            while (reading)
            {
                if (char.IsWhiteSpace(ReadCharacter(currentPosition)))
                {
                    //The reader has bamboozled us and sent us on a wild goose chase for a ConVar name that isn't really a ConVar name.
                    return string.Empty;
                }
                if (ReadCharacter(currentPosition) == '*')
                {
                    //A pointer asterisk? Unacceptable.
                    return string.Empty;
                }
                if (ReadCharacter(currentPosition) == '(')
                {
                    reading = false;
                }
                else
                {
                    nameLength++;
                    AdvanceReader();
                }
            }
            return GetRangeOfString(currentPosition - nameLength, nameLength);
        }

        private string[] ReadConVarParameters()
        {
            //For anyone who reads these, this is actually what I came up with a parser system in the first place. Regex wasn't cut out for it.
            bool reading = true;
            bool inQuotes = false; // " "
            bool inComment = false; // /* */

            string currentParameter = string.Empty;
            List<string> parameters = new List<string>();
            while (reading)
            {
                char currentCharacter = ReadCharacter(currentPosition);
                if (inComment)
                {
                    if(currentCharacter == '*')
                    {
                        if(ReadCharacter(currentPosition+1) == '/')
                        {
                            inComment = false;
                        }
                    }
                    AdvanceReader();
                    continue;
                }
                if (char.IsWhiteSpace(currentCharacter) && inQuotes == false)
                {
                    AdvanceReader();
                    continue;
                }
                //Split parameters.
                if (currentCharacter == ',' && inQuotes == false)
                {
                    parameters.Add(currentParameter);
                    currentParameter = string.Empty;
                    AdvanceReader();
                    continue;
                }
                //Start/end quotes.
                if (currentCharacter == '\"')
                {
                    //Check for string escapes.
                    if(ReadCharacter(currentPosition-1) != '\\')
                    {
                        inQuotes = !inQuotes;
                    }
                    AdvanceReader();
                    continue;
                }
                if (currentCharacter == ')' && inQuotes == false)
                {
                    parameters.Add(currentParameter); //TODO: I mean, when will there NOT be a parameter at the end, but this code repetition feels wrong.
                    reading = false;
                    AdvanceReader();
                    break;
                }
                if (currentCharacter == '/' && inQuotes == false)
                {
                    if (ReadCharacter(currentPosition+1) == '*')
                    {
                        inComment = true;
                    }
                    AdvanceReader();
                    continue;
                }
                currentParameter += currentCharacter;
                AdvanceReader();
            }
            return parameters.ToArray();
        }

        private string GetRangeOfString(int startingPoint, int range)
        {
            if(startingPoint+range >= fileLength)
            {
                readFinished = true;
                return null;
            }
            return fileContents.Substring(startingPoint, range);
        }

        public List<ConVar> GetResults()
        {
            return conVars;
        }

        private ConVarType GetConVarTypeFromArgumentList(string[] arguments)
        {
            return (ConVarType)arguments.Length;
        }
    }
}
