using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VNKit
{
    public enum VNValueType { Number, Text, Bool }

    // Динамически типизированное значение, используемое системой переменных/выражений
    public struct VNValue
    {
        public VNValueType Type;
        public double Number;
        public string Text;
        public bool Bool;

        public static VNValue FromNumber(double n) { return new VNValue { Type = VNValueType.Number, Number = n, Text = null }; }
        public static VNValue FromText(string s)   { return new VNValue { Type = VNValueType.Text, Text = s ?? "" }; }
        public static VNValue FromBool(bool b)     { return new VNValue { Type = VNValueType.Bool, Bool = b }; }

        public bool IsNumeric()
        {
            if (Type == VNValueType.Number || Type == VNValueType.Bool) return true;
            double d;
            return double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
        }

        public double ToNumber()
        {
            if (Type == VNValueType.Number) return Number;
            if (Type == VNValueType.Bool) return Bool ? 1 : 0;
            double d;
            return double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 0;
        }

        public bool ToBool()
        {
            if (Type == VNValueType.Bool) return Bool;
            if (Type == VNValueType.Number) return Number != 0;
            string t = (Text ?? "").Trim().ToLowerInvariant();
            if (t == "true" || t == "yes") return true;
            if (t == "false" || t == "no" || t.Length == 0) return false;
            double d;
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d != 0;
            return true; // non-empty string is true
        }

        public string ToText()
        {
            if (Type == VNValueType.Text) return Text ?? "";
            if (Type == VNValueType.Bool) return Bool ? "true" : "false";
            return Convert.ToString(Number, CultureInfo.InvariantCulture);
        }

        public override string ToString() { return ToText(); }
    }

    /*
    Небольшой оценщик выражений с рекурсивным нисходящим алгоритмом.
    Поддерживает: числа, "строки", true/false, переменные, скобки,
    + - * / % (с конкатенацией строк с помощью +), == != <= >= >=, && || !
    */
    public static class VNExpression
    {
        public static VNValue Evaluate(string expression, Func<string, VNValue> resolveVariable)
        {
            if (string.IsNullOrEmpty(expression)) return VNValue.FromNumber(0);
            var parser = new Parser(Tokenize(expression), resolveVariable);
            return parser.ParseOr();
        }

        // ---------------- Tokenizer ----------------

        enum TokKind { Num, Str, Id, Op, End }

        struct Tok
        {
            public TokKind Kind;
            public string Text;
            public double Num;
        }

        static List<Tok> Tokenize(string s)
        {
            var list = new List<Tok>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                {
                    int start = i;
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                    var tok = new Tok { Kind = TokKind.Num, Text = s.Substring(start, i - start) };
                    double.TryParse(tok.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out tok.Num);
                    list.Add(tok);
                    continue;
                }

                if (c == '"')
                {
                    var sb = new StringBuilder();
                    i++;
                    while (i < s.Length && s[i] != '"')
                    {
                        if (s[i] == '\\' && i + 1 < s.Length)
                        {
                            i++;
                            if (s[i] == 'n') sb.Append('\n');
                            else sb.Append(s[i]);
                            i++;
                        }
                        else { sb.Append(s[i]); i++; }
                    }
                    i++; 
                    list.Add(new Tok { Kind = TokKind.Str, Text = sb.ToString() });
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '.')) i++;
                    list.Add(new Tok { Kind = TokKind.Id, Text = s.Substring(start, i - start) });
                    continue;
                }

                if (i + 1 < s.Length)
                {
                    string two = s.Substring(i, 2);
                    if (two == "&&" || two == "||" || two == "==" || two == "!=" || two == "<=" || two == ">=")
                    {
                        list.Add(new Tok { Kind = TokKind.Op, Text = two });
                        i += 2;
                        continue;
                    }
                }

                if (c == '+' || c == '-' || c == '*' || c == '/' || c == '%' ||
                    c == '<' || c == '>' || c == '!' || c == '(' || c == ')' || c == '=')
                {
                    // Одиночный знак '=' обрабатывается как '=='
                    list.Add(new Tok { Kind = TokKind.Op, Text = c == '=' ? "==" : c.ToString() });
                    i++;
                    continue;
                }

                i++; // пропускает неизвестный символ
            }
            list.Add(new Tok { Kind = TokKind.End });
            return list;
        }

        // ---------------- Parser ----------------

        class Parser
        {
            readonly List<Tok> toks;
            readonly Func<string, VNValue> resolve;
            int pos;

            public Parser(List<Tok> toks, Func<string, VNValue> resolve)
            {
                this.toks = toks;
                this.resolve = resolve;
            }

            Tok Peek() { return pos < toks.Count ? toks[pos] : toks[toks.Count - 1]; }
            bool IsOp(string op) { var t = Peek(); return t.Kind == TokKind.Op && t.Text == op; }
            void Next() { if (pos < toks.Count - 1) pos++; }

            public VNValue ParseOr()
            {
                var left = ParseAnd();
                while (IsOp("||")) { Next(); var right = ParseAnd(); left = VNValue.FromBool(left.ToBool() || right.ToBool()); }
                return left;
            }

            VNValue ParseAnd()
            {
                var left = ParseEquality();
                while (IsOp("&&")) { Next(); var right = ParseEquality(); left = VNValue.FromBool(left.ToBool() && right.ToBool()); }
                return left;
            }

            VNValue ParseEquality()
            {
                var left = ParseRelational();
                while (IsOp("==") || IsOp("!="))
                {
                    string op = Peek().Text; Next();
                    var right = ParseRelational();
                    bool eq = AreEqual(left, right);
                    left = VNValue.FromBool(op == "==" ? eq : !eq);
                }
                return left;
            }

            VNValue ParseRelational()
            {
                var left = ParseAdditive();
                while (IsOp("<") || IsOp("<=") || IsOp(">") || IsOp(">="))
                {
                    string op = Peek().Text; Next();
                    var right = ParseAdditive();
                    int cmp = left.IsNumeric() && right.IsNumeric()
                        ? left.ToNumber().CompareTo(right.ToNumber())
                        : string.CompareOrdinal(left.ToText(), right.ToText());
                    switch (op)
                    {
                        case "<":  left = VNValue.FromBool(cmp < 0); break;
                        case "<=": left = VNValue.FromBool(cmp <= 0); break;
                        case ">":  left = VNValue.FromBool(cmp > 0); break;
                        default:   left = VNValue.FromBool(cmp >= 0); break;
                    }
                }
                return left;
            }

            VNValue ParseAdditive()
            {
                var left = ParseMultiplicative();
                while (IsOp("+") || IsOp("-"))
                {
                    string op = Peek().Text; Next();
                    var right = ParseMultiplicative();
                    if (op == "+")
                    {
                        left = (left.Type == VNValueType.Text || right.Type == VNValueType.Text)
                            ? VNValue.FromText(left.ToText() + right.ToText())
                            : VNValue.FromNumber(left.ToNumber() + right.ToNumber());
                    }
                    else left = VNValue.FromNumber(left.ToNumber() - right.ToNumber());
                }
                return left;
            }

            VNValue ParseMultiplicative()
            {
                var left = ParseUnary();
                while (IsOp("*") || IsOp("/") || IsOp("%"))
                {
                    string op = Peek().Text; Next();
                    var right = ParseUnary();
                    double a = left.ToNumber(), b = right.ToNumber();
                    switch (op)
                    {
                        case "*": left = VNValue.FromNumber(a * b); break;
                        case "/": left = VNValue.FromNumber(b == 0 ? 0 : a / b); break;
                        default:  left = VNValue.FromNumber(b == 0 ? 0 : a % b); break;
                    }
                }
                return left;
            }

            VNValue ParseUnary()
            {
                if (IsOp("!")) { Next(); return VNValue.FromBool(!ParseUnary().ToBool()); }
                if (IsOp("-")) { Next(); return VNValue.FromNumber(-ParseUnary().ToNumber()); }
                return ParsePrimary();
            }

            VNValue ParsePrimary()
            {
                var t = Peek();
                if (t.Kind == TokKind.Num) { Next(); return VNValue.FromNumber(t.Num); }
                if (t.Kind == TokKind.Str) { Next(); return VNValue.FromText(t.Text); }
                if (t.Kind == TokKind.Id)
                {
                    Next();
                    if (t.Text == "true") return VNValue.FromBool(true);
                    if (t.Text == "false") return VNValue.FromBool(false);
                    return resolve != null ? resolve(t.Text) : VNValue.FromNumber(0);
                }
                if (IsOp("("))
                {
                    Next();
                    var v = ParseOr();
                    if (IsOp(")")) Next();
                    return v;
                }
                Next();
                return VNValue.FromNumber(0);
            }

            static bool AreEqual(VNValue a, VNValue b)
            {
                if (a.IsNumeric() && b.IsNumeric()) return a.ToNumber() == b.ToNumber();
                return a.ToText() == b.ToText();
            }
        }
    }
}
