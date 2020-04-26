using System;
using System.Collections.Generic;
using System.Text;

namespace ValveConvarParsingSystem
{
    public struct ConVar //TODO: There are multiple ConVar types. Assuming that all of these values are used may be a bad idea.
    {
        public string variableName; //Necessary...?

        public string name;
        public string initialValue;
        public string description;

        public ConVarFlags flags;
        public ConVarType conVarType;

        public bool usesMinValue;
        public string minValue;

        public bool usesMaxValue;
        public string maxValue;

        public bool executesCallback;
        public string callbackName;

        public bool usesCompetitiveMinValue;
        public string competitiveMinValue;

        public bool usesCompetitiveMaxValue;
        public string competitiveMaxValue;

        public Symbol symbolRequired;
    }

    public enum ConVarType
    {
        ConVar = 2, //Simple ConVar.
        ConVarWithFlags, //ConVar with flag values.
        ConVarDescription, //ConVar with description.
        ConVarCallback, //ConVar with description and callback.
        ConVarLimitedInput = 8, //ConVar with description and value limits.
        ConVarLimitedInputCallback, //ConVar with description, value limits, and a callback.
        ConVarLimitedInputComp = 13, //ConVar with description, value limits, Competitive Mode value limits, and a callback.
    }

    public enum Symbol
    {
        NONE,
        STAGING_ONLY,
        GAME_DLL,
        TF_RAID_MODE,
        _DEBUG
    }
}
