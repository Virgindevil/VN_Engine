using System;
using System.Collections.Generic;
using UnityEngine;

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

    /// <summary>One chat bubble of the phone messenger (PhoneUI).</summary>
    [Serializable]
    public class VNPhoneMessage
    {
        public string id = "";
        public bool incoming;      // true = left bubble (contact), false = right bubble (player)
        public string speaker;
        public string text;        // message text, or the sprite address for kind==1
        public int kind;           // 0 = text, 1 = photo attachment (2.5+)
    }

    /// <summary>One messenger conversation (chat) inside the phone. Persists across the game.</summary>
    [Serializable]
    public class VNPhoneChat
    {
        public string id;          // chat id from the script (chat:max); defaults to the contact name
        public string contact;     // display name shown in the header / chat list
        public int unread;         // unseen incoming messages (badge in the chat list)
        public bool awaiting;      // last incoming message waits for the player's reply
        public bool penalized;     // ignore-penalty already applied for the current wait
        public bool online;        // 2.11: contact is online (@online)
        public List<VNPhoneMessage> messages = new List<VNPhoneMessage>();
    }

    /// <summary>In-memory rollback snapshot of one chat (not written to save files).</summary>
    public class VNPhoneChatSnap
    {
        public int count;
        public int unread;
        public bool awaiting;
        public bool penalized;
        public bool online;
    }

    /// <summary>A live chat dialogue registered by @online ... goto:Label (2.11).</summary>
    [Serializable]
    public class VNChatDialogue
    {
        public string chatId;
        public string label;   // script label jumped to when the player enters the chat
        public bool done;      // finished via @chatend
    }

    /// <summary>2.12: one entry of the phone Notes app (@note add).</summary>
    [Serializable]
    public class VNPhoneNote
    {
        public string id = "";
        public string text = "";
        public bool important;
        public string category = "general"; // general/people/places/events/evidence/secrets (2.12.1)
        public string source = "";          // who/what the note came from (2.12.1)
    }

    [Serializable]
    public class VNScheduleEvent
    {
        public string id = "";
        public string time = "";
        public string title = "";
    }

    [Serializable]
    public class VNPhoneGalleryItem
    {
        public string id = "";       // 2.12.1: variable-safe id → gallery.<id>.viewed/.locked
        public string address = "";
        public string sender = "";
        public string date = "";
        public string location = "";
        public string desc = "";
        public string tag = "";
        public bool important;
        public bool viewed;          // 2.12.1: the player opened it in the viewer
        public bool locked;          // 2.12.1: hidden behind a lock until @gallery unlock
    }

    // A contextual chat action button offered inside a chat (@chatActions).
    [Serializable]
    public class VNPhoneAction
    {
        public string chatId = "";
        public string text = "";
        public string label = "";
        public string condition = "";
        public string doAssign = "";
        public bool once = true; // picked action disappears (default); once:0 keeps it
        public bool used;
    }

    /// <summary>Everything needed to resume a play session. Serialized with JsonUtility.</summary>
    [Serializable]
    public class VNSaveData
    {
        public int version = 1;
        public string scriptName;
        public int nextCommandIndex;
        public string background;
        public string bgm;
        public string cg;          // currently visible event CG, if any
        public List<VNCharState> characters = new List<VNCharState>();
        public List<VNVariableEntry> variables = new List<VNVariableEntry>();
        public List<VNBacklogEntry> backlog = new List<VNBacklogEntry>();
        public bool phoneOpen;     // phone messenger overlay state (2.4+)
        public string phonePos;    // "left" / "center" / "right" (2.4.2+)
        public string phoneChat;   // active chat id (2.5+)
        public List<VNPhoneChat> phoneChats = new List<VNPhoneChat>(); // full messenger history (2.5+)
        public bool phoneChatMode; // 2.8: phone-as-dialogue-UI chat mode
        public bool phoneMenuActive; // 2.9: phone menu vs classic box menu (@phoneOn/@phoneOff)
        public List<VNChatDialogue> phoneDialogues = new List<VNChatDialogue>(); // 2.11: registered live chat dialogues
        public bool phoneDialogueLock; // 2.12.2: a live dialogue holds the player in the chat
        public List<VNPhoneNote> phoneNotes = new List<VNPhoneNote>();
        public List<VNScheduleEvent> phoneSchedule = new List<VNScheduleEvent>();
        public List<VNPhoneGalleryItem> phoneGallery = new List<VNPhoneGalleryItem>();
        public List<VNPhoneAction> phoneActions = new List<VNPhoneAction>();
        public List<string> phoneHiddenApps = new List<string>();
        public int chatHubReturn = -1; // 2.11: @waitchat hub position for @chatend
        public string preview;
        public string timestamp;
    }

    /// <summary>
    /// A gallery "memory": clicking this unlocked CG in the main-menu gallery
    /// replays the scene — jumps to the given script label.
    /// </summary>
    [Serializable]
    public class VNMemoryEntry
    {
        public string cg;          // CG name from galleryCgs
        public string script;      // script file name (without .vns)
        public string label;       // label inside the script
    }

    /// <summary>Priority tracking config for one chat (who the hero prioritizes by replies).</summary>
    [Serializable]
    public class VNChatPriority
    {
        [Tooltip("Chat id from the script (chat:max)")]
        public string chatId;
        [Tooltip("Engine variable receiving the points, e.g. affinity.max — branch on it with @if")]
        public string variable;
        [Tooltip("Points when the player replies in this chat while it awaits an answer")]
        public int answerPoints = 1;
        [Tooltip("Points (usually negative) when the player replies in ANOTHER chat while this one awaits")]
        public int ignorePoints = -1;
    }
}
