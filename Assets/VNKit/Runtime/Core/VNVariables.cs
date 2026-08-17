using System;
using System.Collections.Generic;
using System.Text;

namespace VNKit
{
    /// <summary>
    /// Global variable store. Values are dynamically typed (number / text / bool).
    /// Undefined variables read as 0, which also means "false".
    /// </summary>
    public class VNVariables
    {
        readonly Dictionary<string, VNValue> map = new Dictionary<string, VNValue>();

        public event Action Changed;

        public VNValue Get(string name)
        {
            VNValue v;
            return map.TryGetValue(name, out v) ? v : VNValue.FromNumber(0);
        }

        public float GetFloat(string name) { return (float)Get(name).ToNumber(); }
        public string GetString(string name) { return Get(name).ToText(); }
        public bool GetBool(string name) { return Get(name).ToBool(); }

        static readonly System.Text.RegularExpressions.Regex varRef =
            new System.Text.RegularExpressions.Regex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// Expands {variable} references inside script text: "Hi, {playerName}!".
        /// Unknown variables expand to an empty string. Double the braces
        /// ("{{" / "}}") to print them literally.
        /// </summary>
        public string Expand(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            text = text.Replace("{{", "\u0001").Replace("}}", "\u0002");
            text = varRef.Replace(text, delegate (System.Text.RegularExpressions.Match m)
            {
                return map.ContainsKey(m.Groups[1].Value) ? GetString(m.Groups[1].Value) : "";
            });
            return text.Replace("\u0001", "{").Replace("\u0002", "}");
        }

        public void Set(string name, VNValue value)
        {
            map[name] = value;
            var h = Changed;
            if (h != null) h();
        }

        public void Clear()
        {
            map.Clear();
            var h = Changed;
            if (h != null) h();
        }

        public bool Evaluate(string conditionExpression)
        {
            return VNExpression.Evaluate(conditionExpression, Get).ToBool();
        }

        /// <summary>Applies a comma-separated list such as "gold=100, affection+=2, name=\"Hana\"".</summary>
        public void Apply(string assignments)
        {
            if (string.IsNullOrEmpty(assignments)) return;
            foreach (string rawPart in SplitTopLevel(assignments, ','))
            {
                string part = rawPart.Trim();
                if (part.Length == 0) continue;

                string op;
                int opIndex = FindAssignmentOp(part, out op);
                if (opIndex < 0) continue;

                string name = part.Substring(0, opIndex).Trim();
                string expr = part.Substring(opIndex + op.Length).Trim();
                if (name.Length == 0) continue;

                VNValue rhs = VNExpression.Evaluate(expr, Get);
                VNValue cur = Get(name);

                switch (op)
                {
                    case "=": Set(name, rhs); break;
                    case "+=":
                        Set(name, cur.Type == VNValueType.Text || rhs.Type == VNValueType.Text
                            ? VNValue.FromText(cur.ToText() + rhs.ToText())
                            : VNValue.FromNumber(cur.ToNumber() + rhs.ToNumber()));
                        break;
                    case "-=": Set(name, VNValue.FromNumber(cur.ToNumber() - rhs.ToNumber())); break;
                    case "*=": Set(name, VNValue.FromNumber(cur.ToNumber() * rhs.ToNumber())); break;
                    case "/=":
                        double d = rhs.ToNumber();
                        Set(name, VNValue.FromNumber(d == 0 ? cur.ToNumber() : cur.ToNumber() / d));
                        break;
                }
            }
        }

        static int FindAssignmentOp(string s, out string op)
        {
            op = null;
            bool quoted = false;
            int depth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') quoted = !quoted;
                if (quoted) continue;
                if (c == '(') depth++;
                if (c == ')') depth--;
                if (depth != 0) continue;

                if ((c == '+' || c == '-' || c == '*' || c == '/') && i + 1 < s.Length && s[i + 1] == '=')
                {
                    op = c + "=";
                    return i;
                }
                if (c == '=')
                {
                    bool prevOk = i == 0 || (s[i - 1] != '=' && s[i - 1] != '!' && s[i - 1] != '<' && s[i - 1] != '>');
                    bool nextOk = i + 1 >= s.Length || s[i + 1] != '=';
                    if (prevOk && nextOk) { op = "="; return i; }
                }
            }
            return -1;
        }

        static List<string> SplitTopLevel(string s, char sep)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            int depth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') quoted = !quoted;
                if (!quoted)
                {
                    if (c == '(') depth++;
                    else if (c == ')') depth--;
                    else if (c == sep && depth == 0) { list.Add(sb.ToString()); sb.Length = 0; continue; }
                }
                sb.Append(c);
            }
            list.Add(sb.ToString());
            return list;
        }

        // ---------------- Serialization (JsonUtility-compatible) ----------------

        public List<VNVariableEntry> ToEntries()
        {
            var list = new List<VNVariableEntry>();
            foreach (var kv in map)
            {
                list.Add(new VNVariableEntry
                {
                    name = kv.Key,
                    type = (int)kv.Value.Type,
                    number = kv.Value.Number,
                    text = kv.Value.Text,
                    boolean = kv.Value.Bool
                });
            }
            return list;
        }

        public void FromEntries(List<VNVariableEntry> entries)
        {
            map.Clear();
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    VNValue v;
                    switch ((VNValueType)e.type)
                    {
                        case VNValueType.Text: v = VNValue.FromText(e.text); break;
                        case VNValueType.Bool: v = VNValue.FromBool(e.boolean); break;
                        default: v = VNValue.FromNumber(e.number); break;
                    }
                    map[e.name] = v;
                }
            }
            var h = Changed;
            if (h != null) h();
        }
    }
}
