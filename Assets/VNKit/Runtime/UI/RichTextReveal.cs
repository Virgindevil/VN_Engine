using System.Collections.Generic;
using System.Text;

namespace VNKit
{
    /*
    Постепенно отображает форматированный текст для создания эффекта пишущей машинки.
    Теги никогда не разделяются посередине и автоматически закрываются на каждом шаге,
    поэтому uGUI всегда получает корректную разметку.
    */
    public class RichTextReveal
    {
        public int Total { get; private set; }
        readonly string[] prefixes;

        public RichTextReveal(string raw)
        {
            raw = raw ?? "";
            var list = new List<string>();
            var sb = new StringBuilder();
            var open = new List<string>();

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '<')
                {
                    int close = raw.IndexOf('>', i);
                    if (close > i)
                    {
                        string tag = raw.Substring(i, close - i + 1);
                        sb.Append(tag);
                        string tagName = TagName(tag);
                        if (tagName != null)
                        {
                            if (tag.StartsWith("</"))
                            {
                                int li = open.LastIndexOf(tagName);
                                if (li >= 0) open.RemoveAt(li);
                            }
                            else if (!tag.EndsWith("/>")) open.Add(tagName);
                        }
                        i = close;
                        continue;
                    }
                }

                sb.Append(c);

                var snap = new StringBuilder(sb.ToString());
                for (int t = open.Count - 1; t >= 0; t--)
                    snap.Append("</").Append(open[t]).Append('>');
                list.Add(snap.ToString());
            }

            prefixes = list.ToArray();
            Total = prefixes.Length;
        }

        public string Get(int visibleChars)
        {
            if (Total == 0 || visibleChars <= 0) return "";
            if (visibleChars >= Total) return prefixes[Total - 1];
            return prefixes[visibleChars - 1];
        }

        static string TagName(string tag)
        {
            int s = tag.StartsWith("</") ? 2 : 1;
            int e = s;
            while (e < tag.Length && (char.IsLetterOrDigit(tag[e]) || tag[e] == '#')) e++;
            return e > s ? tag.Substring(s, e - s) : null;
        }
    }
}
