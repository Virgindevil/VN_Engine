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

    // Один из вариантов внутри команды @choice
    public class VNChoiceOption
    {
        public string Text;
        public string GotoLabel;   // "Label" или "Script.Label" может быть null 
        public string Condition;   // необязательное выражение; опция скрыта, если значение равно false
        public string DoAssign;    // Необязательные присваивания применяются при выборе, например, "affection+=1"
    }

    // Отдельная команда, обработанная из скрипта .vns
    public class VNCommand
    {
        public VNCommandType Type;
        public int LineNumber;      // Строка в исходном файле, начинающаяся с 1 (используется для отслеживания "прочитанного текста")
        public string Raw;          // Исходная строка кода для отладки

        // --- Say ---
        public string Speaker;      // null => говорит "нарратор"
        public string Appearance;   // необязательный префикс "Name.Appearance" для эмоций
        public string Text;

        // --- Универсальный основной аргумент (имя фона, идентификатор персонажа, имя клипа и т.д.) ---
        public string Name;
        public string Pos;          // Свободный позиционный токен (например, позиция персонажа)

        // --- key:value parameters ---
        public Dictionary<string, string> Params = new Dictionary<string, string>();

        // --- Choice ---
        public List<VNChoiceOption> Options;

        // --- Set / If / Goto ---
        public string Expression;   // If
        public string Assignments;  // Set, raw "a=1, b+=2"
        public string GotoLabel;    // Goto / If
        public string ElseLabel;    // If

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
