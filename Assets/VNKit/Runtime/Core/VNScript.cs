using System.Collections.Generic;

namespace VNKit
{
    // Полностью проанализированный скрипт .vns: упорядоченные команды + таблица меток
    public class VNScript
    {
        public string Name;
        public readonly List<VNCommand> Commands = new List<VNCommand>();
        public readonly Dictionary<string, int> Labels = new Dictionary<string, int>();

        public VNScript(string name)
        {
            Name = name;
        }

        // Возвращает индекс команды, на которую указывает метка, или -1
        public int FindLabel(string label)
        {
            int idx;
            return Labels.TryGetValue(label, out idx) ? idx : -1;
        }
    }
}
