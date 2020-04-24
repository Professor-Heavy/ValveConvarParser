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

        public string conVarType;

        public int flags;

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

        public bool isStagingOnly;
    }
}
