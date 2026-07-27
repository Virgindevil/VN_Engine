using System;
using System.Collections.Generic;

namespace VNKit
{
    [Serializable]
    public class VNVariableEntry
    {
        public string name;
        public int type;      // VNValueType
        public double number;
        public string text;
        public bool boolean;
    }

    [Serializable]
    public class VNCharState
    {
        public string name;
        public string appearance;
        public float pos;
        public bool visible;
    }

    //Все необходимое для возобновления игровой сессии. Сериализовано с помощью JsonUtility
    [Serializable]
    public class VNSaveData
    {
        public int version = 1;
        public string scriptName;
        public int nextCommandIndex;
        public string background;
        public string bgm;
        public List<VNCharState> characters = new List<VNCharState>();
        public List<VNVariableEntry> variables = new List<VNVariableEntry>();
        public List<VNBacklogEntry> backlog = new List<VNBacklogEntry>();
        public string preview;
        public string timestamp;
    }
}
