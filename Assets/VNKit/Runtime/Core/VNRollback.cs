using System.Collections.Generic;

namespace VNKit
{
    /// <summary>
    /// Ring buffer of lightweight state snapshots for mouse-wheel rollback
    /// (Ren'Py / Naninovel style). The engine captures a snapshot before every
    /// Say / Choice command; wheel-up (or the rollback hotkey) restores the
    /// previous one. Snapshots are session-only, not written to save files.
    /// </summary>
    public class VNRollback
    {
        public class Snapshot
        {
            public string scriptName;
            public int commandIndex;                 // index of the Say/Choice command to re-execute
            public List<VNVariableEntry> variables;
            public int backlogCount;
            public string background;
            public string bgm;
            public string cg;
            public List<VNCharState> characters;
        }

        readonly List<Snapshot> stack = new List<Snapshot>();
        const int Capacity = 40;

        public int Count { get { return stack.Count; } }

        public void Push(Snapshot s)
        {
            if (s == null) return;
            stack.Add(s);
            if (stack.Count > Capacity) stack.RemoveAt(0);
        }

        public Snapshot Pop()
        {
            if (stack.Count == 0) return null;
            var s = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            return s;
        }

        public void Clear()
        {
            stack.Clear();
        }
    }
}
