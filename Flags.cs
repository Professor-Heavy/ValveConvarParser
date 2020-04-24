using System;
using System.Collections.Generic;
using System.Text;

namespace ValveConvarParsingSystem
{
    //TODO: This was hardcoded from the Team Fortress 2 source code. This needs to be rewritten.#
    [Flags]
    public enum ConVarFlags
    {
        Default = -1,
        FCVAR_NONE = 0,
        FCVAR_UNREGISTERED = (1 << 0),
        FCVAR_DEVELOPMENTONLY = (1 << 1),
        FCVAR_GAMEDLL = (1 << 2),
        FCVAR_CLIENTDLL = (1 << 3),
        FCVAR_HIDDEN = (1 << 4),
        FCVAR_PROTECTED = (1 << 5),
        FCVAR_SPONLY = (1 << 6),
        FCVAR_ARCHIVE = (1 << 7),
        FCVAR_NOTIFY = (1 << 8),
        FCVAR_USERINFO = (1 << 9),
        FCVAR_PRINTABLEONLY = (1 << 10),
        FCVAR_UNLOGGED = (1 << 11),
        FCVAR_NEVER_AS_STRING = (1 << 12),
        FCVAR_REPLICATED = (1 << 13),
        FCVAR_CHEAT = (1 << 14),
        FCVAR_INTERNAL_USE = (1 << 15),
        FCVAR_DEMO = (1 << 16),
        FCVAR_DONTRECORD = (1 << 17),
        FCVAR_ALLOWED_IN_COMPETITIVE = (1 << 18),
        FCVAR_RELOAD_MATERIALS = (1 << 20), //19 is reserved.
        FCVAR_RELOAD_TEXTURES = (1 << 21),
        FCVAR_NOT_CONNECTED = (1 << 22),
        FCVAR_MATERIAL_SYSTEM_THREAD = (1 << 23),
        FCVAR_ARCHIVE_XBOX = (1 << 24),
        FCVAR_ACCESSIBLE_FROM_THREADS = (1 << 25),
        FCVAR_SERVER_CAN_EXECUTE = (1 << 28),
        FCVAR_SERVER_CANNOT_QUERY = (1 << 29),
        FCVAR_CLIENTCMD_CAN_EXECUTE = (1 << 30),
        FCVAR_EXEC_DESPITE_DEFAULT = (1 << 31)
    }
}
