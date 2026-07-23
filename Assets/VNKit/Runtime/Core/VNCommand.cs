using System;
using System.Collections.Generic;
using System.Globalization;

namespace VNKit
{
    public enum VNCommandType
    {
        Say, Char, HideChar, HideChars, Background,
        Bgm, StopBgm, Sfx, Voice, StopVoice,
        Choice, Goto, Set, If, Wait, End, Custom
    }

    /// <summary>One option inside a @choice command.</summary>
    public class VNChoiceOption
    {
        public string Text;
        public string GotoLabel;   // "Label" or "Script.Label"; may be null (just continue)
        public string Condition;   // optional expression; option hidden when false
        public string DoAssign;    // optional assignments applied when picked, e.g. "affection+=1"
    }

    /// <summary>A single parsed command of a .vns script.</summary>
    public class VNCommand
    {
        public VNCommandType Type;
        public int LineNumber;      // 1-based line in source file (used for "seen text" tracking)
        public string Raw;          // original source line, for debugging

        // --- Say ---
        public string Speaker;      // null => narration
        public string Appearance;   // optional "Name.Appearance" prefix on the speaker
        public string Text;

        // --- Generic primary argument (bg name, char id, clip name, ...) ---
        public string Name;
        public string Pos;          // free positional token (e.g. character position)

        // --- key:value parameters ---
        public Dictionary<string, string> Params = new Dictionary<string, string>();

        // --- Choice ---
        public List<VNChoiceOption> Options;

        // --- Set / If / Goto ---
        public string Expression;   // for If
        public string Assignments;  // for Set, raw "a=1, b+=2"
        public string GotoLabel;    // for Goto / If
        public string ElseLabel;    // for If

        public string Get(string key, string def = null)
        {
            string v;
            return Params.TryGetValue(key, out v) ? v : def;
        }

        public float GetFloat(string key, float def)
        {
            string v;
            if (Params.TryGetValue(key, out v))
            {
                float f;
                if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return f;
            }
            return def;
        }

        public bool GetBool(string key, bool def)
        {
            string v;
            if (Params.TryGetValue(key, out v))
            {
                if (v == "true" || v == "1") return true;
                if (v == "false" || v == "0") return false;
            }
            return def;
        }

        public override string ToString()
        {
            return string.Format("[{0}:{1}] {2}", Type, LineNumber, Raw);
        }
    }
}
