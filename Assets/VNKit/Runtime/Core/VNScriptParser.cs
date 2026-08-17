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

                if (cmd != null)
                {
                    // Two-line dialogue style ("Макс:" on one line, the text on the next):
                    // a speaker-only Say absorbs the narration line that follows it.
                    if (cmd.Type == VNCommandType.Say && string.IsNullOrEmpty(cmd.Speaker)
                        && cmd.Text.Length > 0 && script.Commands.Count > 0
                        && !script.Labels.ContainsValue(script.Commands.Count))
                    {
                        var prev = script.Commands[script.Commands.Count - 1];
                        if (prev.Type == VNCommandType.Say && !string.IsNullOrEmpty(prev.Speaker)
                            && prev.Text.Length == 0)
                        {
                            prev.Text = cmd.Text;
                            continue;
                        }
                    }
                    script.Commands.Add(cmd);
                }
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
                case "input":     cmd.Type = VNCommandType.Input;      FillPositional(cmd, tokens); break;
                case "phone":     cmd.Type = VNCommandType.Phone;      FillPositional(cmd, tokens); break;
                case "photo":     cmd.Type = VNCommandType.Photo;      FillPositional(cmd, tokens); break;
                case "msg":       cmd.Type = VNCommandType.PhoneMsg;   FillPositional(cmd, tokens); break;
                case "chat":      cmd.Type = VNCommandType.ChatTarget; FillPositional(cmd, tokens); break;
                // @phoneOn / @phoneOff — switch the in-game menu style at runtime:
                // phone menu (RMB/Esc → смартфон) vs the classic box menu.
                case "phoneOn":   cmd.Type = VNCommandType.PhoneMenuToggle; cmd.Name = "on";  break;
                case "phoneOff":  cmd.Type = VNCommandType.PhoneMenuToggle; cmd.Name = "off"; break;
                // @waitchat max,exes — park the script until those chats' live
                // dialogues (@online ... goto:Label) are finished.
                case "waitchat":  cmd.Type = VNCommandType.WaitChat; FillPositional(cmd, tokens); break;
                // @chatend [goto:Label] — finish the current chat dialogue and
                // return to the @waitchat hub (or jump to Label).
                case "chatend":   cmd.Type = VNCommandType.ChatEnd;  FillPositional(cmd, tokens); break;
                // @online = the contact comes online and the live chat begins
                // (alias of @phone open); @offline ends it (= @phone close).
                case "online":    cmd.Type = VNCommandType.Phone;      FillPositional(cmd, tokens);
                                  if (string.IsNullOrEmpty(cmd.Name)) cmd.Name = "open"; break;
                case "offline":   cmd.Type = VNCommandType.Phone;      FillPositional(cmd, tokens);
                                  if (string.IsNullOrEmpty(cmd.Name)) cmd.Name = "close"; break;
                case "typing":    cmd.Type = VNCommandType.Typing;     FillPositional(cmd, tokens); break;
                case "fadeOut":   cmd.Type = VNCommandType.Fade;       FillPositional(cmd, tokens); cmd.Params["dir"] = "out"; break;
                case "fadeIn":    cmd.Type = VNCommandType.Fade;       FillPositional(cmd, tokens); cmd.Params["dir"] = "in"; break;
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
                    ParseIf(cmd, body, name);
                    break;

                case "choice":
                    cmd.Type = VNCommandType.Choice;
                    ParseChoice(cmd, body.Substring(name.Length));
                    break;

                // 2.12 — phone gameplay API
                case "chatActions":
                    cmd.Type = VNCommandType.ChatActions;
                    ParseChatActions(cmd, body.Substring(name.Length));
                    break;
                case "phonehub":  cmd.Type = VNCommandType.PhoneHub;  FillParams(cmd, tokens); break;
                case "note":      cmd.Type = VNCommandType.Note;      FillPositional(cmd, tokens); break;
                case "schedule":  cmd.Type = VNCommandType.Schedule;  FillPositional(cmd, tokens); break;
                case "gallery":   cmd.Type = VNCommandType.Gallery;   FillPositional(cmd, tokens); break;
                case "phoneGame": cmd.Type = VNCommandType.PhoneGame; FillPositional(cmd, tokens); break;
                case "phoneapp":  cmd.Type = VNCommandType.PhoneApp;  FillPositional(cmd, tokens); break;
                case "message":   cmd.Type = VNCommandType.Message;   FillPositional(cmd, tokens); break;

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

        /// <summary>
        /// Parses "@if &lt;expr&gt; goto:X else:Y" from the RAW body (not from split tokens),
        /// so quoted string literals inside the expression keep their quotes:
        /// @if playerName=="" goto:DefaultName — works, "" stays a string literal.
        /// </summary>
        static void ParseIf(VNCommand cmd, string body, string name)
        {
            string rest = body.Length > name.Length ? body.Substring(name.Length).Trim() : "";

            int gotoIdx = FindKeywordOutsideQuotes(rest, "goto:");
            if (gotoIdx < 0) { cmd.Expression = rest; return; }

            cmd.Expression = rest.Substring(0, gotoIdx).Trim();
            cmd.GotoLabel = ReadWord(rest, gotoIdx + 5);

            int elseIdx = FindKeywordOutsideQuotes(rest, "else:");
            if (elseIdx > gotoIdx)
                cmd.ElseLabel = ReadWord(rest, elseIdx + 5);
        }

        /// <summary>Finds "key:" at start or after whitespace, outside of quoted sections.</summary>
        static int FindKeywordOutsideQuotes(string s, string keyword)
        {
            bool quoted = false;
            for (int i = 0; i + keyword.Length <= s.Length; i++)
            {
                char c = s[i];
                if (c == '"') { quoted = !quoted; continue; }
                if (quoted) continue;
                if (i > 0 && !char.IsWhiteSpace(s[i - 1])) continue;
                if (string.CompareOrdinal(s, i, keyword, 0, keyword.Length) == 0) return i;
            }
            return -1;
        }

        /// <summary>Reads a non-whitespace word starting at index i.</summary>
        static string ReadWord(string s, int i)
        {
            int end = i;
            while (end < s.Length && !char.IsWhiteSpace(s[end])) end++;
            return s.Substring(i, end - i);
        }

        /// <summary>
        /// Parses "@choice "Text" goto:X if:expr do:assign | ..." from raw text,
        /// keyword positions are found OUTSIDE quoted sections — so string literals
        /// in conditions/assignments keep their quotes (if:playerSex=="male",
        /// do:mood="happy") and a choice text may itself contain " goto:" in quotes.
        /// </summary>
        static void ParseChoice(VNCommand cmd, string remainder)
        {
            cmd.Options = new List<VNChoiceOption>();
            foreach (string part0 in SplitRespectingQuotes(remainder, '|'))
            {
                string part = part0.Trim();
                if (part.Length == 0) continue;

                var opt = new VNChoiceOption();
                int gt = FindKeywordOutsideQuotes(part, "goto:");
                int ic = FindKeywordOutsideQuotes(part, "if:");
                int dc = FindKeywordOutsideQuotes(part, "do:");

                int cut = part.Length;
                if (gt >= 0 && gt < cut) cut = gt;
                if (ic >= 0 && ic < cut) cut = ic;
                if (dc >= 0 && dc < cut) cut = dc;

                opt.Text = part.Substring(0, cut).Trim();
                if (opt.Text.Length >= 2 && opt.Text[0] == '"' && opt.Text[opt.Text.Length - 1] == '"')
                    opt.Text = opt.Text.Substring(1, opt.Text.Length - 2);

                if (gt >= 0) opt.GotoLabel = ReadWord(part, gt + 5);
                if (ic >= 0) opt.Condition = ReadParamValue(part, ic + 3, gt, dc);
                if (dc >= 0) opt.DoAssign = ReadParamValue(part, dc + 3, gt, ic);

                cmd.Options.Add(opt);
            }
        }

        /// <summary>
        /// Parses "@chatActions chat:rin [once:0] "Text" goto:Label [if:expr] [do:assign] | ..."
        /// or the "@chatActions chat:rin clear" form. Leading key:value params are read
        /// from the raw remainder (their values are single words), the option list itself
        /// reuses the @choice parser.
        /// </summary>
        static void ParseChatActions(VNCommand cmd, string remainder)
        {
            string rest = remainder.Trim();
            while (rest.Length > 0 && rest[0] != '"' && !rest.StartsWith("clear"))
            {
                string word = ReadWord(rest, 0);
                int colon = word.IndexOf(':');
                if (colon <= 0 || !IsKey(word.Substring(0, colon))) break;
                cmd.Params[word.Substring(0, colon)] = word.Substring(colon + 1);
                rest = rest.Substring(word.Length).TrimStart();
            }
            if (rest.StartsWith("clear"))
            {
                cmd.Params["clear"] = "1";
                return;
            }
            ParseChoice(cmd, rest);
        }

        /// <summary>Reads a parameter value (expression/assignments) up to the next keyword or end.</summary>
        static string ReadParamValue(string s, int start, int other1, int other2)
        {
            int end = s.Length;
            if (other1 > start && other1 < end) end = other1;
            if (other2 > start && other2 < end) end = other2;
            return s.Substring(start, end - start).Trim();
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
