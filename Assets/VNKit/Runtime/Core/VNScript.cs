using System.Collections.Generic;

namespace VNKit
{
    /// <summary>A fully parsed .vns script: ordered commands + label table.</summary>
    public class VNScript
    {
        public string Name;
        public readonly List<VNCommand> Commands = new List<VNCommand>();
        public readonly Dictionary<string, int> Labels = new Dictionary<string, int>();

        public VNScript(string name)
        {
            Name = name;
        }

        /// <summary>Returns the command index the label points to, or -1.</summary>
        public int FindLabel(string label)
        {
            int idx;
            return Labels.TryGetValue(label, out idx) ? idx : -1;
        }
    }
}
