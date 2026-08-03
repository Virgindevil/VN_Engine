using System;
using System.Collections.Generic;
using System.Text;

namespace VNKit
{
    /// <summary>
    /// Parses .vns text into a VNScript.
    ///
    /// Syntax overview:
    ///   ; comment
    ///   # LabelName
    ///   @bg Campus time:0.8
    ///   @char Hana.Happy pos:left time:0.4
    ///   @hide Hana  |  @hideChars
    ///   @bgm Theme fade:1.5   |   @stopBgm fade:1
    ///   @sfx Chime vol:0.8    |   @voice hana_01  |  @stopVoice
    ///   @cg RooftopSunset fade:0.8  |  @cg off fade:0.5   (full-screen event CG)
    ///   @minigame Lockpick difficulty:2 var:lockResult    (mini-game overlay)
    ///   Hana: Hello there![br]Second line.
    ///   Hana.Smile: Changes appearance, then speaks.
    ///   Narration line without a speaker prefix.
    ///   @choice "Option A" goto:LabelA if:score>0 do:score+=1 | "Option B" goto:LabelB
    ///   @goto Label   |   @goto OtherScript.Label
    ///   @set gold=100, affection+=2, name="Hana"
    ///   @if affection>0 goto:GoodEnd else:NormalEnd
    ///   @wait 1.5
    ///   @end
    /// </summary>
    public static class VNScriptParser
    {
        public static VNScript Parse(string scriptName, string text)
        {
            var script = new VNScript(scriptName);
            if (text == null) return script;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith(";")) continue;                      // comment
                if (line.StartsWith("#"))                                // label
                {
                    string label = line.Substring(1).Trim();
                    if (label.Length > 0) script.Labels[label] = script.Commands.Count;
                    continue;
                }

                VNCommand cmd = line.StartsWith("@")
                    ? ParseCommand(line.Substring(1), i + 1, line)
                    : ParseDialogue(line, i + 1);

                if (cmd != null) script.Commands.Add(cmd);
            }
            return script;
        }

        // ------------------------------------------------------------------

        static VNCommand ParseDialogue(string line, int lineNo)
        {
            var cmd = new VNCommand { Type = VNCommandType.Say, LineNumber = lineNo, Raw = line };
            int colon = line.IndexOf(':');
            string text = line;

            if (colon > 0)
            {
                string head = line.Substring(0, colon);
                // Treat as speaker only when the head is a single "word" (letters/digits/_/.),
                // so narration like "Note: ..." with spaces is not misread.
                if (IsSpeakerToken(head))
                {
                    cmd.Speaker = head;
                    int dot = head.IndexOf('.');
                    if (dot >= 0)
                    {
                        cmd.Speaker = head.Substring(0, dot);
                        cmd.Appearance = head.Substring(dot + 1);
                    }
                    text = line.Substring(colon + 1).Trim();
                }
            }

            text = text.Replace("[br]", "\n");
            if (text.Length >= 2 && text.StartsWith("\"") && text.EndsWith("\""))
                text = text.Substring(1, text.Length - 2);

            cmd.Text = text;
            return cmd;
        }

        static bool IsSpeakerToken(string head)
        {
            if (head.Length == 0 || head.Length > 32) return false;
            for (int i = 0; i < head.Length; i++)
            {
                char c = head[i];
                if (char.IsWhiteSpace(c) || c == '"' || c == '@' || c == '[' || c == ']') return false;
            }
            return true;
        }

        static VNCommand ParseCommand(string body, int lineNo, string raw)
        {
            var tokens = SplitTokens(body);
            if (tokens.Count == 0) return null;

            string name = tokens[0];
            var cmd = new VNCommand { LineNumber = lineNo, Raw = raw };

            switch (name)
            {
                case "bg":        cmd.Type = VNCommandType.Background; FillPositional(cmd, tokens); break;
                case "char":      cmd.Type = VNCommandType.Char;       FillPositional(cmd, tokens); break;
                case "hide":      cmd.Type = VNCommandType.HideChar;   FillPositional(cmd, tokens); break;
                case "hideChars": cmd.Type = VNCommandType.HideChars;  FillParams(cmd, tokens); break;
                case "bgm":       cmd.Type = VNCommandType.Bgm;        FillPositional(cmd, tokens); break;
                case "stopBgm":   cmd.Type = VNCommandType.StopBgm;    FillParams(cmd, tokens); break;
                case "sfx":       cmd.Type = VNCommandType.Sfx;        FillPositional(cmd, tokens); break;
                case "voice":     cmd.Type = VNCommandType.Voice;      FillPositional(cmd, tokens); break;
                case "stopVoice": cmd.Type = VNCommandType.StopVoice;  FillParams(cmd, tokens); break;
                case "cg":        cmd.Type = VNCommandType.Cg;         FillPositional(cmd, tokens); break;
                case "minigame":  cmd.Type = VNCommandType.Minigame;   FillPositional(cmd, tokens); break;
                case "wait":      cmd.Type = VNCommandType.Wait;       FillPositional(cmd, tokens); break;
                case "end":       cmd.Type = VNCommandType.End;        break;

                case "goto":
                    cmd.Type = VNCommandType.Goto;
                    if (tokens.Count > 1) cmd.GotoLabel = tokens[1];
                    break;

                case "set":
                    cmd.Type = VNCommandType.Set;
                    cmd.Assignments = body.Length > name.Length ? body.Substring(name.Length).Trim() : "";
                    break;

                case "if":
                    cmd.Type = VNCommandType.If;
                    ParseIf(cmd, tokens);
                    break;

                case "choice":
                    cmd.Type = VNCommandType.Choice;
                    ParseChoice(cmd, body.Substring(name.Length));
                    break;

                default:
                    cmd.Type = VNCommandType.Custom;
                    cmd.Name = name;
                    FillParams(cmd, tokens);
                    break;
            }
            return cmd;
        }

        /// <summary>First bare token -> Name, second -> Pos; key:value pairs -> Params; "hide" flag supported.</summary>
        static void FillPositional(VNCommand cmd, List<string> tokens)
        {
            var positional = new List<string>();
            for (int i = 1; i < tokens.Count; i++)
            {
                string t = tokens[i];
                if (t == "hide") { cmd.Params["visible"] = "false"; continue; }
                int colon = t.IndexOf(':');
                if (colon > 0 && IsKey(t.Substring(0, colon)))
                    cmd.Params[t.Substring(0, colon)] = t.Substring(colon + 1);
                else
                    positional.Add(t);
            }
            if (positional.Count > 0) cmd.Name = positional[0];
            if (positional.Count > 1) cmd.Pos = positional[1];
        }

        static void FillParams(VNCommand cmd, List<string> tokens)
        {
            for (int i = 1; i < tokens.Count; i++)
            {
                string t = tokens[i];
                int colon = t.IndexOf(':');
                if (colon > 0 && IsKey(t.Substring(0, colon)))
                    cmd.Params[t.Substring(0, colon)] = t.Substring(colon + 1);
            }
        }

        static void ParseIf(VNCommand cmd, List<string> tokens)
        {
            var exprTokens = new List<string>();
            for (int i = 1; i < tokens.Count; i++)
            {
                string t = tokens[i];
                if (t.StartsWith("goto:")) cmd.GotoLabel = t.Substring(5);
                else if (t.StartsWith("else:")) cmd.ElseLabel = t.Substring(5);
                else exprTokens.Add(t);
            }
            cmd.Expression = string.Join(" ", exprTokens.ToArray());
        }

        static void ParseChoice(VNCommand cmd, string remainder)
        {
            cmd.Options = new List<VNChoiceOption>();
            foreach (string part in SplitRespectingQuotes(remainder, '|'))
            {
                var tokens = SplitTokens(part.Trim());
                if (tokens.Count == 0) continue;
                var opt = new VNChoiceOption { Text = tokens[0] };
                for (int i = 1; i < tokens.Count; i++)
                {
                    string t = tokens[i];
                    if (t.StartsWith("goto:")) opt.GotoLabel = t.Substring(5);
                    else if (t.StartsWith("if:")) opt.Condition = t.Substring(3);
                    else if (t.StartsWith("do:")) opt.DoAssign = t.Substring(3);
                }
                cmd.Options.Add(opt);
            }
        }

        static bool IsKey(string s)
        {
            if (s.Length == 0) return false;
            for (int i = 0; i < s.Length; i++)
                if (!char.IsLetter(s[i])) return false;
            return true;
        }

        /// <summary>Splits on whitespace; double-quoted sections stay together (quotes removed).</summary>
        static List<string> SplitTokens(string s)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') { quoted = !quoted; continue; }
                if (!quoted && char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0) { list.Add(sb.ToString()); sb.Length = 0; }
                }
                else sb.Append(c);
            }
            if (sb.Length > 0) list.Add(sb.ToString());
            return list;
        }

        static List<string> SplitRespectingQuotes(string s, char sep)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') quoted = !quoted;
                if (c == sep && !quoted) { list.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(c);
            }
            list.Add(sb.ToString());
            return list;
        }
    }
}
