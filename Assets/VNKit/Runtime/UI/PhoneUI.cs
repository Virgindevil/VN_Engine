using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace VNKit
{
    /// <summary>
    /// Consumes pointer clicks on the phone body so they cannot fall through to UI
    /// behind the overlay (quick-menu buttons etc.). The advance click itself is read
    /// from raw input polling, so dialogue advance keeps working over the phone.
    /// </summary>
    public class VNClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) { }
    }

    /// <summary>
    /// Smartphone overlay ("Phone UI") — both a story device and the in-game menu.
    ///
    /// STORY MODE (script-driven):
    ///   @phone open contact:"Макс 🐶" chat:max pos:left   — slide in, open chat "max"
    ///   Макс: Ну???                                       — chat bubble (left)
    ///   {playerName}: Пришло.                             — chat bubble (right)
    ///   @photo SunsetPic sender:"Макс"                    — photo attachment bubble
    ///   @typing time:1.2                                  — "typing…" indicator
    ///   @phone close                                      — slide out (chats are KEPT)
    ///   @phone reset                                      — wipe all chats (New Game does this)
    ///
    /// MENU MODE (player-driven, RMB/Esc with usePhoneMenu on): the phone opens on a
    /// Home screen with apps — Chats (full persistent history of every chat, with
    /// scrolling), Photos (all photo attachments), Backlog, Save, Load, Settings,
    /// Title. Menu mode is modal: the game pauses behind it.
    ///
    /// Chats persist across @phone close and are stored in saves; rollback snapshots
    /// keep per-chat message counts and truncate on restore (history is append-only).
    /// A custom skin sprite (e.g. a hand holding a phone) can replace the procedural
    /// body — see VisualNovelEngine.phoneSkin / phoneSize / phoneScreenRect.
    /// </summary>
    public partial class PhoneUI
    {
        // 2.12: Gallery replaces Photos (chat attachments + @gallery items with meta);
        // Contacts / Notes / Schedule / Games are the phone apps.
        // 2.12.1: ContactCard (a contact's page: relations + actions) and
        // PhoneSettings (the Settings app: game settings + save/load/title).
        public enum Screen { Story, Home, ChatList, ReadChat, Gallery, Contacts, Notes, Schedule, Games, ContactCard, PhoneSettings }

        static readonly Color BodyColor     = new Color(0.02f, 0.02f, 0.03f, 0.98f);
        static readonly Color ScreenColor   = new Color(0.07f, 0.08f, 0.11f, 1f);
        static readonly Color HeaderColor   = new Color(0.10f, 0.11f, 0.15f, 1f);
        static readonly Color IncomingColor = new Color(0.16f, 0.18f, 0.23f, 1f);
        static readonly Color SubTextColor  = new Color(1f, 1f, 1f, 0.55f);

        const float DefaultWidth = 470f;
        const float DefaultHeight = 900f;
        const float ScreenX = 560f;         // center offset from screen middle (1920x1080 ref)
        const float MaxTextWidth = 300f;    // bubble text wrap width
        const float PadX = 18f;             // bubble horizontal padding
        const float PadY = 14f;             // bubble vertical padding
        const float PhotoSize = 240f;       // photo attachment thumbnail edge
        const float HeaderHeight = 92f;

        /// <summary>One messenger conversation.</summary>
        class Chat
        {
            public string id;
            public string contact;
            public int unread;        // unseen incoming messages (badge in the chat list)
            public bool awaiting;     // an incoming message waits for the player's reply
            public bool penalized;    // ignore-penalty already applied for the current wait
            public bool online;       // contact is online (@online) — green ● in the list
            public List<VNPhoneMessage> messages = new List<VNPhoneMessage>();
        }

        // ---- public state ----
        public bool IsOpen { get; private set; }        // script mode: spoken lines route into chats
        public bool IsMenuOpen { get; private set; }    // menu mode: player opened the phone (modal)
        /// <summary>2.8 chat mode: the phone menu IS the dialogue UI — the game
        /// keeps running while it is open, every conversation happens inside the
        /// Chats tab like in a real messenger.</summary>
        public bool ChatMode { get { return chatMode; } }
        public string Contact { get; private set; }
        public string CurrentChatId { get { return currentChatId; } }
        public string Position { get; private set; }
        public Transform Root { get { return root.transform; } }
        public System.Action Opened;                    // story phone became visible
        public System.Action Closed;                    // story phone hidden
        public System.Action MenuClosed;                // player put the phone menu away

        // ---- UI ----
        readonly VisualNovelEngine engine;
        readonly GameObject root;
        readonly RectTransform frame;
        readonly RectTransform screen;
        readonly TextMeshProUGUI titleText;
        readonly TextMeshProUGUI statusText;
        readonly Button backButton;
        readonly RectTransform storyViewport, storyContent;   // story bubbles (auto-scroll)
        readonly GameObject storyLayer;
        readonly ScrollRect readScroll; readonly RectTransform readContent;   // chat reading
        readonly GameObject readLayer;
        readonly ScrollRect listScroll; readonly RectTransform listContent;   // chat list
        readonly GameObject listLayer;
        readonly ScrollRect photosScroll; readonly RectTransform photosContent; // photos app
        readonly GameObject photosLayer;
        readonly GameObject homeLayer;
        Image bodyImage;                       // phone body (skin sprite or procedural)
        Image screenImage;                     // screen background tint
        float hiddenY;                         // off-screen Y (depends on the skin aspect)
        float currentX = ScreenX;
        Screen currentScreen = Screen.Story;
        string readChatId;                 // chat open on the reading screen (live-updated)
        bool chatMode;                     // 2.8: phone-as-dialogue-UI mode (@online/@phone chats)

        // ---- notification toast (incoming @msg while the chat is not on screen) ----
        GameObject toast;
        TextMeshProUGUI toastText;
        string toastChatId;
        Coroutine toastRoutine;

        // ---- data ----
        readonly Dictionary<string, Chat> chats = new Dictionary<string, Chat>();
        readonly List<string> chatOrder = new List<string>();
        string currentChatId;
        readonly Dictionary<string, Sprite> photoCache = new Dictionary<string, Sprite>();
        // 2.11: live dialogues registered by @online ... goto:Label — the script
        // jumps to the label when the player enters that chat from the hub.
        readonly Dictionary<string, string> pendingDialogues = new Dictionary<string, string>();
        readonly HashSet<string> doneDialogues = new HashSet<string>();

        GameObject typingBubble;
        TextMeshProUGUI typingLabel;
        Coroutine slideRoutine;
        Coroutine dotsRoutine;

        public PhoneUI(Transform parent, VisualNovelEngine engine)
        {
            this.engine = engine;
            Position = "right";

            root = UIFactory.Rect("VNKit.Phone", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);

            frame = UIFactory.Rect("Frame", root.transform);
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = new Vector2(currentX, -2000f); // provisional; ApplySkin fixes it
            bodyImage = UIFactory.AddImage(frame.gameObject, BodyColor);
            bodyImage.raycastTarget = true;
            frame.gameObject.AddComponent<VNClickCatcher>();

            // Screen area: inside the skin via the normalized phoneScreenRect,
            // or a default inset for the procedural body.
            screen = UIFactory.Rect("Screen", frame);
            screenImage = UIFactory.AddImage(screen.gameObject, ScreenColor);
            screenImage.raycastTarget = false;

            // Frame size + skin (male/female variant by the protagonist's sex).
            ApplySkin();

            // ---- header (back button + title + status) ----
            var header = UIFactory.Rect("Header", screen);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(0f, -HeaderHeight);
            header.offsetMax = new Vector2(0f, 0f);
            var hdr = UIFactory.AddImage(header.gameObject, HeaderColor);
            hdr.raycastTarget = false;

            backButton = UIFactory.Button(header, "Back", "‹", 40, OnBackPressed);
            var bbrt = (RectTransform)backButton.transform;
            bbrt.anchorMin = new Vector2(0f, 0.5f);
            bbrt.anchorMax = new Vector2(0f, 0.5f);
            bbrt.pivot = new Vector2(0f, 0.5f);
            bbrt.sizeDelta = new Vector2(64f, 64f);
            bbrt.anchoredPosition = new Vector2(6f, 0f);

            titleText = UIFactory.Text(header, "Title", "", 30, TextAnchor.MiddleCenter, UIFactory.TextColor);
            var trt = (RectTransform)titleText.transform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(74f, -52f);
            trt.offsetMax = new Vector2(-12f, -6f);
            titleText.enableWordWrapping = false;
            titleText.overflowMode = TextOverflowModes.Ellipsis;

            statusText = UIFactory.Text(header, "Status", "", 20, TextAnchor.MiddleCenter, SubTextColor);
            var srt = (RectTransform)statusText.transform;
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.offsetMin = new Vector2(74f, -90f);
            srt.offsetMax = new Vector2(-12f, -52f);

            // ---- story layer (auto-scrolling bubbles, no ScrollRect) ----
            storyLayer = UIFactory.Rect("Story", screen).gameObject;
            var slrt = (RectTransform)storyLayer.transform;
            slrt.anchorMin = Vector2.zero;
            slrt.anchorMax = Vector2.one;
            slrt.offsetMin = new Vector2(6f, 6f);
            slrt.offsetMax = new Vector2(-6f, -HeaderHeight - 6f);
            storyViewport = UIFactory.Rect("Viewport", storyLayer.transform);
            UIFactory.Stretch(storyViewport);
            storyViewport.gameObject.AddComponent<RectMask2D>();
            storyContent = MakeBubbleColumn(storyViewport);

            // ---- read-chat layer (scrollable) ----
            readLayer = UIFactory.Rect("Read", screen).gameObject;
            readScroll = UIFactory.ScrollView(readLayer.transform, "Scroll", out readContent);
            UIFactory.Stretch((RectTransform)readScroll.transform);
            var rlrt = (RectTransform)readLayer.transform;
            rlrt.anchorMin = Vector2.zero;
            rlrt.anchorMax = Vector2.one;
            rlrt.offsetMin = new Vector2(6f, 6f);
            rlrt.offsetMax = new Vector2(-6f, -HeaderHeight - 6f);

            // ---- chat list layer (scrollable) ----
            listLayer = UIFactory.Rect("List", screen).gameObject;
            listScroll = UIFactory.ScrollView(listLayer.transform, "Scroll", out listContent);
            UIFactory.Stretch((RectTransform)listScroll.transform);
            var llrt = (RectTransform)listLayer.transform;
            llrt.anchorMin = Vector2.zero;
            llrt.anchorMax = Vector2.one;
            llrt.offsetMin = new Vector2(6f, 6f);
            llrt.offsetMax = new Vector2(-6f, -HeaderHeight - 6f);

            // ---- photos app layer (scrollable grid) ----
            photosLayer = UIFactory.Rect("Photos", screen).gameObject;
            photosScroll = UIFactory.ScrollView(photosLayer.transform, "Scroll", out photosContent);
            UIFactory.Stretch((RectTransform)photosScroll.transform);
            var plrt = (RectTransform)photosLayer.transform;
            plrt.anchorMin = Vector2.zero;
            plrt.anchorMax = Vector2.one;
            plrt.offsetMin = new Vector2(6f, 6f);
            plrt.offsetMax = new Vector2(-6f, -HeaderHeight - 6f);

            // ---- home layer (app buttons) ----
            homeLayer = UIFactory.Rect("Home", screen).gameObject;
            var hlrt = (RectTransform)homeLayer.transform;
            hlrt.anchorMin = Vector2.zero;
            hlrt.anchorMax = Vector2.one;
            hlrt.offsetMin = new Vector2(16f, 16f);
            hlrt.offsetMax = new Vector2(-16f, -HeaderHeight - 10f);
            var hvlg = homeLayer.AddComponent<VerticalLayoutGroup>();
            hvlg.childAlignment = TextAnchor.UpperCenter;
            hvlg.childControlWidth = true;
            hvlg.childControlHeight = true;
            hvlg.childForceExpandWidth = true;
            hvlg.childForceExpandHeight = false;
            hvlg.spacing = 14f;
            hvlg.padding = new RectOffset(6, 6, 18, 6);
            BuildHome(); // 2.12: app buttons are rebuilt on every visit (@phoneapp gating)

            // 2.12: contextual chat actions (@chatActions) — a button bar at the
            // bottom of the reading screen; «Далее» — the @phonehub exit button.
            EnsureActionBar();
            EnsureHubContinue();

            // ---- notification toast (top of the screen, tap → open that chat) ----
            // Sibling of the phone root so it stays visible while the phone is hidden.
            toast = UIFactory.Rect("Toast", root.transform.parent).gameObject;
            var toastRt = (RectTransform)toast.transform;
            toastRt.anchorMin = new Vector2(0.5f, 1f);
            toastRt.anchorMax = new Vector2(0.5f, 1f);
            toastRt.pivot = new Vector2(0.5f, 1f);
            toastRt.sizeDelta = new Vector2(620f, 104f);
            toastRt.anchoredPosition = new Vector2(0f, -28f);
            var timg = UIFactory.AddImage(toast, new Color(0.10f, 0.11f, 0.15f, 0.97f));
            timg.sprite = UIFactory.UISprite;
            timg.type = Image.Type.Sliced;
            var tbtn = toast.AddComponent<Button>();
            tbtn.targetGraphic = timg;
            tbtn.onClick.AddListener(OnToastTapped);
            toastText = UIFactory.Text(toast.transform, "Text", "", 24, TextAnchor.MiddleLeft, UIFactory.TextColor);
            var ttrt = (RectTransform)toastText.transform;
            ttrt.anchorMin = Vector2.zero;
            ttrt.anchorMax = Vector2.one;
            ttrt.offsetMin = new Vector2(22f, 8f);
            ttrt.offsetMax = new Vector2(-22f, -8f);
            toastText.raycastTarget = false;
            toast.SetActive(false);

            root.SetActive(false);
        }

        void OnToastTapped()
        {
            toast.SetActive(false);
            string id = toastChatId;
            OpenMenu();
            if (string.IsNullOrEmpty(id) || !chats.ContainsKey(id)) return;
            if (IsOpen && id == currentChatId) ShowScreen(Screen.Story);
            else OpenReadChat(id);
        }

        void ShowToast(Chat chat, VNPhoneMessage msg)
        {
            toastChatId = chat.id;
            if (toast == null || toastText == null) return;
            // msg == null → an "online" notification (@online ... goto:)
            string preview = msg == null ? VNLoc.T("phone.online")
                : msg.kind == 1 ? VNLoc.T("phone.photo") : (msg.text ?? "");
            if (preview.Length > 64) preview = preview.Substring(0, 64) + "…";
            toastText.text = "<b>" + chat.contact + "</b>\n<color=#9aa>" + preview + "</color>";
            toast.SetActive(true);
            toast.transform.SetAsLastSibling();
            if (toastRoutine != null) engine.StopCoroutine(toastRoutine);
            toastRoutine = engine.StartCoroutine(ToastRoutine());
        }

        IEnumerator ToastRoutine()
        {
            yield return new WaitForSecondsRealtime(3.5f);
            toast.SetActive(false);
            toastRoutine = null;
        }

        void AddAppButton(Transform parent, string locKey, UnityEngine.Events.UnityAction action)
        {
            var b = UIFactory.Button(parent, locKey, VNLoc.T(locKey), 28, action);
            UIFactory.Layout(b.gameObject, 0f, 72f);
        }

        static RectTransform MakeBubbleColumn(Transform viewport)
        {
            var content = UIFactory.Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(10, 10, 12, 12);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        // ============================== story mode ==============================

        /// <summary>@online — the contact comes online: the phone opens straight
        /// INTO that chat (chat mode) and the scripted conversation plays there,
        /// like in a real messenger. There is no dialogue outside the Chats tab.</summary>
        public void Open(string chatId, string contact, float time, string pos)
        {
            var chat = GetOrCreateChat(chatId, contact);
            chat.unread = 0; // the player is looking straight at this chat
            currentChatId = chat.id;
            Contact = chat.contact;
            ApplyPosition(pos);
            ApplySkin(); // the protagonist's sex may have been chosen since the last open
            chatMode = true;
            IsOpen = true;
            IsMenuOpen = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            frame.anchoredPosition = new Vector2(currentX, frame.anchoredPosition.y);
            OpenReadChat(chat.id); // the live conversation IS the chat screen
            StartSlide(0f, time, false);
            if (Opened != null) Opened();
        }

        /// <summary>@phone chats — open the phone directly into the chat list in
        /// chat mode: the game keeps running and all scripted dialogues are
        /// delivered into their chats (@chat switches the target chat).</summary>
        public void OpenChats(float time)
        {
            ApplySkin();
            chatMode = true;
            IsOpen = true;
            IsMenuOpen = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            frame.anchoredPosition = new Vector2(currentX, frame.anchoredPosition.y);
            ShowScreen(Screen.ChatList);
            StartSlide(0f, time, false);
            if (Opened != null) Opened();
        }

        /// <summary>@chat — aim the script at a chat: subsequent spoken lines and
        /// @photo land there. Does not change what the player is looking at.</summary>
        public void SetActiveChat(string chatId, string contact)
        {
            var chat = GetOrCreateChat(chatId, contact);
            currentChatId = chat.id;
            Contact = chat.contact;
            if (currentScreen == Screen.ChatList) BuildChatList();
        }

        // ============================== live dialogues (2.11) ==============================

        /// <summary>@online ... goto:Label — the contact comes online with a pending
        /// live dialogue. Does NOT force-open the chat: the player gets a toast,
        /// opens the phone and picks the chat; the script jumps to the label then.
        /// The phone stays in chat mode so RMB/toast open the messenger.</summary>
        public void RegisterDialogue(string chatId, string contact, string label, bool notify)
        {
            var chat = GetOrCreateChat(chatId, contact);
            chat.online = true;
            pendingDialogues[chat.id] = label;
            chatMode = true;
            IsOpen = true;
            if (notify) ShowToast(chat, null);
            if (currentScreen == Screen.ChatList) BuildChatList();
        }

        /// <summary>Label of the pending live dialogue for this chat, or null.</summary>
        public string PendingDialogueLabel(string chatId)
        {
            string label;
            return chatId != null && pendingDialogues.TryGetValue(chatId, out label) ? label : null;
        }

        /// <summary>@chatend — the current chat's live dialogue is finished.
        /// If the player is looking at that chat, return them to the chat list
        /// (like a real messenger when the conversation ends).</summary>
        public void CompleteDialogue(string chatId)
        {
            if (string.IsNullOrEmpty(chatId)) return;
            pendingDialogues.Remove(chatId);
            doneDialogues.Add(chatId);
            if (IsMenuOpen && currentScreen == Screen.ReadChat && readChatId == chatId)
                ShowScreen(Screen.ChatList);
            else if (currentScreen == Screen.ChatList) BuildChatList();
        }

        public bool IsDialogueDone(string chatId)
        {
            return chatId != null && doneDialogues.Contains(chatId);
        }

        /// <summary>True while the player is actually looking at this chat (the phone
        /// menu is open on its reading screen). Chat-mode dialogue lines advance
        /// only in this state.</summary>
        public bool IsViewingChat(string chatId)
        {
            return chatId != null && IsMenuOpen
                && currentScreen == Screen.ReadChat && readChatId == chatId;
        }

        /// <summary>Save/rollback export of the dialogue registry.</summary>
        public List<VNChatDialogue> GetDialogues()
        {
            var d = new List<VNChatDialogue>();
            foreach (var kv in pendingDialogues)
                d.Add(new VNChatDialogue { chatId = kv.Key, label = kv.Value, done = false });
            foreach (var id in doneDialogues)
            {
                string label;
                // done chats keep their label if still known, else just the id
                d.Add(new VNChatDialogue { chatId = id, label = null, done = true });
            }
            return d;
        }

        public void RestoreDialogues(List<VNChatDialogue> list)
        {
            pendingDialogues.Clear();
            doneDialogues.Clear();
            if (list != null)
                foreach (var e in list)
                {
                    if (e == null || string.IsNullOrEmpty(e.chatId)) continue;
                    if (e.done) doneDialogues.Add(e.chatId);
                    else if (!string.IsNullOrEmpty(e.label)) pendingDialogues[e.chatId] = e.label;
                }
        }

        /// <summary>Slide the phone out (@offline). Chat history is KEPT
        /// (use ResetAll to wipe); messages stay in the Chats tab.</summary>
        public void Close(float time)
        {
            if (!IsOpen && !IsMenuOpen && !root.activeSelf) return;
            bool wasMenu = IsMenuOpen;
            chatMode = false;
            IsOpen = false;
            IsMenuOpen = false;
            // @offline — everyone leaves: online markers and pending live
            // dialogues are cleared (finished dialogues stay in doneDialogues).
            foreach (var c in chats.Values) c.online = false;
            pendingDialogues.Clear();
            HideTyping();
            if (wasMenu || root.activeSelf)
                StartSlide(hiddenY, time, true);
            if (Closed != null) Closed();
        }

        /// <summary>Wipe every chat (New Game).</summary>
        public void ResetAll()
        {
            StopSlide();
            HideTyping();
            if (toast != null) toast.SetActive(false);
            chats.Clear();
            chatOrder.Clear();
            photoCache.Clear();
            pendingDialogues.Clear();
            doneDialogues.Clear();
            pendingChoiceTexts = null; // 2.12.2: in-phone @choice
            pendingChoiceCb = null;
            ResetAppsData(); // 2.12: notes / schedule / gallery / actions / hidden apps
            currentChatId = null;
            chatMode = false;
            IsOpen = false;
            IsMenuOpen = false;
            ClearChildren(storyContent);
            frame.anchoredPosition = new Vector2(currentX, hiddenY);
            root.SetActive(false);
            if (Closed != null) Closed();
        }

        void ApplyPosition(string pos)
        {
            Position = string.IsNullOrEmpty(pos) ? "right" : pos.Trim().ToLowerInvariant();
            switch (Position)
            {
                case "left":   currentX = -ScreenX; break;
                case "center": currentX = 0f; break;
                default:       Position = "right"; currentX = ScreenX; break;
            }
        }

        /// <summary>(Re)apply the phone body: the skin sprite is picked for the
        /// protagonist's current sex (engine.GetPhoneSkin), the frame height follows
        /// the sprite aspect and the screen area moves into phoneScreenRect.
        /// Called on every open, so choosing the sex mid-game updates the phone.</summary>
        public void ApplySkin()
        {
            var skin = engine.GetPhoneSkin();

            // Frame size: explicit phoneSize; with a skin sprite the height follows
            // the sprite's aspect ratio (width from phoneSize.x).
            Vector2 size = engine.phoneSize;
            if (size.x < 100f) size = new Vector2(DefaultWidth, DefaultHeight);
            if (skin != null)
            {
                var r = skin.rect;
                if (r.width > 0f) size.y = size.x * (r.height / r.width);
            }
            hiddenY = -(size.y * 0.5f + 560f);
            frame.sizeDelta = size;

            if (skin != null)
            {
                bodyImage.sprite = skin;
                bodyImage.type = Image.Type.Simple;
                bodyImage.color = Color.white;
                Rect sr = engine.phoneScreenRect;
                screen.anchorMin = new Vector2(sr.x, sr.y);
                screen.anchorMax = new Vector2(sr.x + sr.width, sr.y + sr.height);
                screen.offsetMin = Vector2.zero;
                screen.offsetMax = Vector2.zero;
                screenImage.color = new Color(0.07f, 0.08f, 0.11f, 0.92f);
            }
            else
            {
                bodyImage.sprite = UIFactory.UISprite;
                bodyImage.type = Image.Type.Sliced;
                bodyImage.color = BodyColor;
                screen.anchorMin = Vector2.zero;
                screen.anchorMax = Vector2.one;
                screen.offsetMin = new Vector2(10f, 14f);
                screen.offsetMax = new Vector2(-10f, -14f);
                screenImage.color = ScreenColor;
            }

            // Keep the current on/off-screen state with the (possibly new) size.
            float y = frame.anchoredPosition.y;
            frame.anchoredPosition = new Vector2(currentX, y < -100f ? hiddenY : 0f);
        }

        Chat GetOrCreateChat(string chatId, string contact)
        {
            string id = string.IsNullOrEmpty(chatId) ? (contact ?? "") : chatId;
            Chat c;
            if (!chats.TryGetValue(id, out c))
            {
                c = new Chat { id = id, contact = contact ?? "" };
                chats[id] = c;
                chatOrder.Add(id);
            }
            else if (!string.IsNullOrEmpty(contact)) c.contact = contact;
            return c;
        }

        Chat CurrentChat()
        {
            Chat c;
            return currentChatId != null && chats.TryGetValue(currentChatId, out c) ? c : null;
        }

        // ============================== messages ==============================

        /// <summary>Append a text bubble to the current chat. incoming=true → left.</summary>
        public void AddMessage(bool incoming, string speaker, string text)
        {
            PushMessage(currentChatId, incoming, speaker, text, 0, null);
        }

        /// <summary>Append a photo attachment bubble (sprite already loaded by the caller).</summary>
        public void AddPhotoMessage(bool incoming, string speaker, string address, Sprite sprite)
        {
            PushMessage(currentChatId, incoming, speaker, address, 1, sprite);
        }

        /// <summary>Append a message to ANY chat (@msg). If the player is looking at that
        /// chat right now (live story screen or its reading screen) the bubble appears
        /// immediately; otherwise an incoming message bumps the unread badge.</summary>
        public void PushMessage(string chatId, bool incoming, string speaker, string text, int kind, Sprite sprite)
        {
            PushMessage(chatId, incoming, speaker, text, kind, sprite, true);
        }

        /// <summary>notify=false suppresses the toast banner (background history
        /// pre-fill via @msg ... notify:0 — the chat silently gains history).</summary>
        public void PushMessage(string chatId, bool incoming, string speaker, string text, int kind, Sprite sprite, bool notify)
        {
            PushMessage(chatId, incoming, speaker, text, kind, sprite, notify, null);
        }

        /// <summary>2.12: id mirrors the message state into script variables —
        /// message.&lt;id&gt;.received is set here, .read when the player opens the
        /// chat, .answered when the player replies in this chat. Conditions like
        /// @if message.rin_ask.read == true work with no extra systems.</summary>
        public void PushMessage(string chatId, bool incoming, string speaker, string text, int kind, Sprite sprite, bool notify, string id)
        {
            var chat = GetOrCreateChat(chatId, incoming ? speaker : null);
            if (kind == 1 && sprite != null) photoCache[text] = sprite;
            var msg = new VNPhoneMessage { id = id ?? "", incoming = incoming, speaker = speaker, text = text, kind = kind };
            chat.messages.Add(msg);
            if (!string.IsNullOrEmpty(msg.id))
            {
                if (incoming) MarkMessageVar(msg.id, "received");
                else MarkLastIncomingAnswered(chat); // a reply answers the last tracked incoming message
            }
            else if (!incoming) MarkLastIncomingAnswered(chat);
            TrackPriority(chat, incoming);

            bool liveViewed = currentScreen == Screen.ReadChat && readChatId == chat.id;
            if (liveViewed) LiveAppend(chat, msg);
            else
            {
                if (incoming)
                {
                    chat.unread++;
                    if (notify) ShowToast(chat, msg);
                    VNLog.Log("Message to chat '" + chat.id + "' (" + chat.contact +
                              "), unread=" + chat.unread + " — see Phone menu → Chats.");
                }
                if (currentScreen == Screen.ChatList) BuildChatList();
            }
        }

        /// <summary>Priority tracking ("кто у героя в приоритете"). Incoming message → the
        /// chat awaits a reply. Player's reply in chat X → X earns answerPoints; every other
        /// awaiting chat earns ignorePoints (once per wait). Points go to the engine
        /// variables listed in engine.chatPriorities, so scripts can branch on them.</summary>
        void TrackPriority(Chat chat, bool incoming)
        {
            if (incoming) { chat.awaiting = true; chat.penalized = false; return; }
            if (engine == null) return; // reflection tests / headless use
            var cfg = engine.chatPriorities;
            if (cfg != null)
                foreach (var p in cfg)
                {
                    if (p == null || string.IsNullOrEmpty(p.chatId) || string.IsNullOrEmpty(p.variable)) continue;
                    if (p.chatId == chat.id)
                    {
                        if (chat.awaiting && p.answerPoints != 0) AddVar(p.variable, p.answerPoints);
                    }
                    else
                    {
                        Chat other;
                        if (chats.TryGetValue(p.chatId, out other) && other.awaiting && !other.penalized
                            && p.ignorePoints != 0)
                        {
                            AddVar(p.variable, p.ignorePoints);
                            other.penalized = true;
                        }
                    }
                }
            chat.awaiting = false;
        }

        void AddVar(string name, int delta)
        {
            var v = engine.Variables.Get(name).ToNumber() + delta;
            engine.Variables.Set(name, VNValue.FromNumber(v));
        }

        /// <summary>Reflect a new message on whichever screen is showing: the
        /// reading screen (when it shows THIS chat — the conversation continues
        /// live, like in a real messenger) and the chat list (fresh previews).</summary>
        void LiveAppend(Chat chat, VNPhoneMessage msg)
        {
            if (currentScreen == Screen.ReadChat && readChatId == chat.id)
            {
                CreateBubble(readContent, msg);
                ScrollReadToBottom();
            }
            else if (currentScreen == Screen.ChatList)
            {
                BuildChatList();
            }
        }

        /// <summary>Rebuild the story layer from the current chat (open / rollback).</summary>
        void RebuildStory()
        {
            ClearChildren(storyContent);
            var chat = CurrentChat();
            if (chat != null)
                foreach (var m in chat.messages) CreateBubble(storyContent, m);
            ScrollStoryToBottom();
        }

        void CreateBubble(RectTransform parent, VNPhoneMessage msg)
        {
            if (msg.kind == 1) CreatePhotoBubble(parent, msg);
            else CreateTextBubble(parent, msg);
        }

        void CreateTextBubble(RectTransform parent, VNPhoneMessage msg)
        {
            var row = UIFactory.Rect("Msg", parent);
            var rowLE = row.gameObject.AddComponent<LayoutElement>();

            var bubble = UIFactory.Rect("Bubble", row);
            bubble.anchorMin = new Vector2(msg.incoming ? 0f : 1f, 0.5f);
            bubble.anchorMax = new Vector2(msg.incoming ? 0f : 1f, 0.5f);
            bubble.pivot = new Vector2(msg.incoming ? 0f : 1f, 0.5f);
            bubble.anchoredPosition = Vector2.zero;
            var img = UIFactory.AddImage(bubble.gameObject, msg.incoming ? IncomingColor : OutgoingColor());
            img.sprite = UIFactory.UISprite;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;

            var txt = UIFactory.Text(bubble, "Text", msg.text, 24, TextAnchor.UpperLeft, UIFactory.TextColor);
            txt.raycastTarget = false;
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(PadX, PadY);
            trt.offsetMax = new Vector2(-PadX, -PadY);

            // Measure with wrapping OFF: with wrapping enabled TMP reports
            // preferredWidth = current rect width, which is 0 right after creation
            // (e.g. when the chat-reading screen is built before the first layout pass),
            // collapsing bubbles to a sliver.
            txt.enableWordWrapping = false;
            float textW = Mathf.Min(txt.preferredWidth, MaxTextWidth);
            txt.enableWordWrapping = true;
            float textH = txt.GetPreferredValues(msg.text, textW, 0f).y;
            float bh = Mathf.Max(44f, textH + PadY * 2f);
            bubble.sizeDelta = new Vector2(textW + PadX * 2f, bh);
            rowLE.preferredHeight = bh;
        }

        void CreatePhotoBubble(RectTransform parent, VNPhoneMessage msg)
        {
            CreatePhotoBubble(parent, msg, null);
        }

        /// <summary>galleryId != null → this bubble belongs to a @gallery item: opening
        /// the viewer marks gallery.&lt;id&gt;.viewed. The click loads the photo on
        /// demand (not cache-only) so it also works right after the bubble appeared.</summary>
        void CreatePhotoBubble(RectTransform parent, VNPhoneMessage msg, string galleryId)
        {
            string address = msg.text;
            var row = UIFactory.Rect("PhonePhoto", parent);
            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.preferredHeight = PhotoSize + 12f;

            // The row itself is the tap target; "PhonePhoto" is the name the advance
            // gating looks for so tapping a photo opens the viewer instead of advancing.
            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            var bubble = UIFactory.Rect("Bubble", row);
            bubble.anchorMin = new Vector2(msg.incoming ? 0f : 1f, 0.5f);
            bubble.anchorMax = new Vector2(msg.incoming ? 0f : 1f, 0.5f);
            bubble.pivot = new Vector2(msg.incoming ? 0f : 1f, 0.5f);
            bubble.anchoredPosition = Vector2.zero;
            bubble.sizeDelta = new Vector2(PhotoSize + 12f, PhotoSize + 12f);
            var img = UIFactory.AddImage(bubble.gameObject, msg.incoming ? IncomingColor : OutgoingColor());
            img.sprite = UIFactory.UISprite;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;

            var picRT = UIFactory.Rect("Pic", bubble);
            picRT.anchorMin = Vector2.zero;
            picRT.anchorMax = Vector2.one;
            picRT.offsetMin = new Vector2(6f, 6f);
            picRT.offsetMax = new Vector2(-6f, -6f);
            var raw = picRT.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.color = new Color(1f, 1f, 1f, 0.25f); // dim until loaded
            var fit = picRT.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            btn.onClick.AddListener(delegate
            {
                EnsurePhoto(address, delegate (Sprite s)
                {
                    if (s == null) return;
                    engine.ShowPhotoViewer(s);
                    if (!string.IsNullOrEmpty(galleryId)) MarkGalleryViewed(galleryId);
                });
            });

            EnsurePhoto(address, delegate (Sprite s)
            {
                if (raw == null) return;
                if (s != null)
                {
                    raw.texture = s.texture;
                    raw.color = Color.white;
                    fit.aspectRatio = s.rect.width / s.rect.height;
                }
            });
        }

        void EnsurePhoto(string address, System.Action<Sprite> onDone)
        {
            Sprite cached;
            if (photoCache.TryGetValue(address, out cached)) { onDone(cached); return; }
            engine.StartCoroutine(LoadPhotoRoutine(address, onDone));
        }

        IEnumerator LoadPhotoRoutine(string address, System.Action<Sprite> onDone)
        {
            Sprite s = null;
            yield return engine.LoadCgAsync(address, x => s = x);
            if (s != null) photoCache[address] = s;
            onDone(s);
        }

        // ============================== typing indicator ==============================

        public void ShowTyping()
        {
            if (!IsOpen || typingBubble != null) return;
            statusText.text = VNLoc.T("phone.typing") + "…";

            var row = UIFactory.Rect("Typing", storyContent);
            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 46f;

            var bubble = UIFactory.Rect("Bubble", row);
            bubble.anchorMin = new Vector2(0f, 0.5f);
            bubble.anchorMax = new Vector2(0f, 0.5f);
            bubble.pivot = new Vector2(0f, 0.5f);
            bubble.sizeDelta = new Vector2(96f, 46f);
            bubble.anchoredPosition = Vector2.zero;
            var img = UIFactory.AddImage(bubble.gameObject, IncomingColor);
            img.sprite = UIFactory.UISprite;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;

            typingLabel = UIFactory.Text(bubble, "Dots", "•", 34, TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.8f));
            UIFactory.Stretch((RectTransform)typingLabel.transform);

            typingBubble = row.gameObject;
            dotsRoutine = engine.StartCoroutine(DotsRoutine());
            ScrollStoryToBottom();
        }

        public void HideTyping()
        {
            if (dotsRoutine != null) { engine.StopCoroutine(dotsRoutine); dotsRoutine = null; }
            typingLabel = null;
            if (typingBubble != null)
            {
                Object.Destroy(typingBubble);
                typingBubble = null;
            }
            if (IsOpen && currentScreen == Screen.Story) statusText.text = VNLoc.T("phone.online");
        }

        IEnumerator DotsRoutine()
        {
            int n = 1;
            float t = 0f;
            while (typingLabel != null)
            {
                t += Time.unscaledDeltaTime;
                if (t >= 0.45f)
                {
                    t = 0f;
                    n = n % 3 + 1;
                    typingLabel.text = new string('•', n);
                }
                yield return null;
            }
        }

        // ============================== menu mode ==============================

        /// <summary>Player-opened phone (RMB/Esc): modal menu over the paused game.
        /// In chat mode it reopens straight into the chat list.</summary>
        public void OpenMenu()
        {
            if (IsMenuOpen) return;
            ApplySkin();
            IsMenuOpen = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            frame.anchoredPosition = new Vector2(currentX, frame.anchoredPosition.y);
            ShowScreen(chatMode ? Screen.ChatList : Screen.Home);
            StartSlide(0f, 0.35f, false);
        }

        public void CloseMenu()
        {
            if (!IsMenuOpen) return;
            IsMenuOpen = false;
            if (chatMode)
            {
                // Chat mode: the phone just slides away — the script keeps running,
                // messages keep arriving (unread badges); RMB brings it back.
                StartSlide(hiddenY, 0.35f, true);
                if (MenuClosed != null) MenuClosed();
                return;
            }
            if (IsOpen)
            {
                // Back to the story chat without hiding the phone.
                ShowScreen(Screen.Story);
                if (MenuClosed != null) MenuClosed();
                return;
            }
            StartSlide(hiddenY, 0.35f, true);
            if (MenuClosed != null) MenuClosed();
        }

        public void ToggleMenu()
        {
            if (IsMenuOpen) CloseMenu();
            else OpenMenu();
        }

        /// <summary>True while the live conversation screen is showing — the engine
        /// lets the player keep chatting (clicks advance) even in menu mode.</summary>
        public bool IsStoryScreen { get { return currentScreen == Screen.Story; } }

        void OnBackPressed()
        {
            switch (currentScreen)
            {
                case Screen.ReadChat: ShowScreen(Screen.ChatList); break;
                case Screen.ContactCard: ShowScreen(Screen.Contacts); break;
                case Screen.ChatList:
                case Screen.Gallery:
                case Screen.Contacts:
                case Screen.Notes:
                case Screen.Schedule:
                case Screen.Games:
                case Screen.PhoneSettings: ShowScreen(Screen.Home); break;
                case Screen.Home: CloseMenu(); break;
                // From the live chat (entered via the chat list) back to the list.
                case Screen.Story: if (IsMenuOpen) ShowScreen(Screen.ChatList); break;
            }
        }

        void ShowScreen(Screen s)
        {
            // 2.8: the standalone story overlay is gone — a live conversation IS
            // the chat's reading screen. Redirect legacy Story requests to it.
            if (s == Screen.Story)
            {
                var cur0 = CurrentChat();
                if (cur0 != null) { OpenReadChat(cur0.id); return; }
                s = Screen.ChatList;
            }
            currentScreen = s;
            if (s != Screen.ReadChat) readChatId = null;
            storyLayer.SetActive(s == Screen.Story);
            readLayer.SetActive(s == Screen.ReadChat);
            listLayer.SetActive(s == Screen.ChatList);
            photosLayer.SetActive(s == Screen.Gallery);
            homeLayer.SetActive(s == Screen.Home);
            SetAppLayers(s); // 2.12: contacts / notes / schedule / games layers
            backButton.gameObject.SetActive(s != Screen.Story || IsMenuOpen);

            switch (s)
            {
                case Screen.Story:
                    var chat = CurrentChat();
                    titleText.text = chat != null ? chat.contact : "";
                    statusText.text = VNLoc.T("phone.online");
                    statusText.gameObject.SetActive(true);
                    RebuildStory();
                    break;
                case Screen.Home:
                    titleText.text = VNLoc.T("phone.home");
                    statusText.gameObject.SetActive(false);
                    BuildHome(); // rebuild every visit: @phoneapp gating may have changed
                    break;
                case Screen.ChatList:
                    titleText.text = VNLoc.T("phone.chats");
                    statusText.gameObject.SetActive(false);
                    BuildChatList();
                    break;
                case Screen.ReadChat:
                    statusText.gameObject.SetActive(false);
                    break;
                case Screen.Gallery:
                    titleText.text = VNLoc.T("phone.gallery");
                    statusText.gameObject.SetActive(false);
                    BuildGallery();
                    break;
                case Screen.Contacts:
                    titleText.text = VNLoc.T("phone.contacts");
                    statusText.gameObject.SetActive(false);
                    BuildContacts();
                    break;
                case Screen.Notes:
                    titleText.text = VNLoc.T("phone.notes");
                    statusText.gameObject.SetActive(false);
                    BuildNotes();
                    break;
                case Screen.Schedule:
                    titleText.text = VNLoc.T("phone.schedule");
                    statusText.gameObject.SetActive(false);
                    BuildSchedule();
                    break;
                case Screen.Games:
                    titleText.text = VNLoc.T("phone.games");
                    statusText.gameObject.SetActive(false);
                    BuildGames();
                    break;
                case Screen.ContactCard:
                    statusText.gameObject.SetActive(false);
                    BuildContactCard(); // title = contact name (set inside)
                    break;
                case Screen.PhoneSettings:
                    titleText.text = VNLoc.T("phone.app.settings");
                    statusText.gameObject.SetActive(false);
                    BuildPhoneSettings();
                    break;
            }
        }

        void BuildChatList()
        {
            ClearChildren(listContent);
            RefreshHubContinue(); // 2.12: «Далее» button while the script sits at @phonehub
            if (chatOrder.Count == 0)
            {
                UIFactory.Text(listContent, "Empty", VNLoc.T("phone.nochats"), 24,
                    TextAnchor.MiddleCenter, SubTextColor);
                return;
            }
            foreach (var id in chatOrder)
            {
                var chat = chats[id];
                // Green ● — the contact is online (@online). A pending live
                // dialogue gets a "+" hint next to the badge.
                string live = chat.online ? "<color=#7f7>●</color> " : "";
                string badge = chat.unread > 0 ? "  <color=#f66>[" + chat.unread + "]</color>" : "";
                string preview = "";
                if (chat.messages.Count > 0)
                {
                    var last = chat.messages[chat.messages.Count - 1];
                    preview = last.kind == 1 ? VNLoc.T("phone.photo") : last.text;
                    if (preview.Length > 32) preview = preview.Substring(0, 32) + "…";
                }
                var b = UIFactory.Button(listContent, "Chat." + id,
                    live + chat.contact + badge + "\n" + "<color=#9aa>" + preview + "</color>", 24,
                    delegate
                    {
                        chat.unread = 0;
                        // 2.11: entering a chat may start its pending live dialogue
                        // (handled by OnChatEntered inside OpenReadChat).
                        OpenReadChat(chat.id);
                    });
                UIFactory.Layout(b.gameObject, 0f, 84f);
            }
        }

        void OpenReadChat(string chatId)
        {
            Chat c;
            if (!chats.TryGetValue(chatId, out c)) return;
            titleText.text = c.contact;
            readChatId = chatId;
            ShowScreenKeepTitle(Screen.ReadChat);
            // "Online" marker while the script is writing into THIS chat.
            bool live = chatMode && chatId == currentChatId;
            statusText.text = VNLoc.T("phone.online");
            statusText.gameObject.SetActive(live);
            ClearChildren(readContent);
            // Unread divider: new messages are visually separated, like in real messengers.
            int firstUnread = c.messages.Count - c.unread;
            for (int i = 0; i < c.messages.Count; i++)
            {
                if (i == firstUnread && c.unread > 0) CreateDivider(readContent);
                CreateBubble(readContent, c.messages[i]);
            }
            c.unread = 0;
            // 2.12: tracked messages become "read" once the player sees them.
            for (int i = 0; i < c.messages.Count; i++)
                if (!string.IsNullOrEmpty(c.messages[i].id)) MarkMessageVar(c.messages[i].id, "read");
            ScrollReadToBottom();
            RefreshActionBar(c); // 2.12: contextual action buttons (@chatActions)
            // 2.11: entering the chat may resume a held line (the script waits
            // until the player is inside) or jump to a pending live dialogue.
            if (engine.Player != null) engine.Player.OnChatEntered(chatId);
        }

        void CreateDivider(RectTransform parent)
        {
            var t = UIFactory.Text(parent, "Divider", "─── " + VNLoc.T("phone.unread") + " ───",
                20, TextAnchor.MiddleCenter, SubTextColor);
            t.raycastTarget = false;
            UIFactory.Layout(t.gameObject, 0f, 36f);
        }

        void ShowScreenKeepTitle(Screen s)
        {
            currentScreen = s;
            if (s != Screen.ReadChat) readChatId = null;
            storyLayer.SetActive(false);
            readLayer.SetActive(s == Screen.ReadChat);
            listLayer.SetActive(false);
            photosLayer.SetActive(false);
            homeLayer.SetActive(false);
            SetAppLayers(s);
            backButton.gameObject.SetActive(true);
            statusText.gameObject.SetActive(false);
        }

        /// <summary>2.12: the Gallery shows both chat photo attachments and
        /// items added via @gallery (with sender/date/location/description meta).</summary>
        void BuildGallery()
        {
            ClearChildren(photosContent);
            bool any = false;
            foreach (var id in chatOrder)
                foreach (var m in chats[id].messages)
                {
                    if (m.kind != 1) continue;
                    any = true;
                    CreatePhotoBubble(photosContent, m);
                }
            any = BuildGalleryItems(photosContent) || any;
            if (!any)
                UIFactory.Text(photosContent, "Empty", VNLoc.T("phone.nophotos"), 24,
                    TextAnchor.MiddleCenter, SubTextColor);
            Canvas.ForceUpdateCanvases();
            photosScroll.verticalNormalizedPosition = 1f;
        }

        // ============================== save / rollback ==============================

        public List<VNPhoneChat> GetChats()
        {
            var list = new List<VNPhoneChat>();
            foreach (var id in chatOrder)
            {
                var c = chats[id];
                list.Add(new VNPhoneChat
                {
                    id = c.id, contact = c.contact,
                    unread = c.unread, awaiting = c.awaiting, penalized = c.penalized,
                    online = c.online,
                    messages = new List<VNPhoneMessage>(c.messages)
                });
            }
            return list;
        }

        /// <summary>Rollback snapshot: message counts + unread/await flags per chat.</summary>
        public Dictionary<string, VNPhoneChatSnap> GetChatStates()
        {
            var d = new Dictionary<string, VNPhoneChatSnap>();
            foreach (var kv in chats)
                d[kv.Key] = new VNPhoneChatSnap
                {
                    count = kv.Value.messages.Count,
                    unread = kv.Value.unread,
                    awaiting = kv.Value.awaiting,
                    penalized = kv.Value.penalized,
                    online = kv.Value.online
                };
            return d;
        }

        /// <summary>Load restore: full messenger state from a save file.</summary>
        public void Restore(bool open, string chatId, string pos, List<VNPhoneChat> savedChats, bool chatMode)
        {
            StopSlide();
            HideTyping();
            chats.Clear();
            chatOrder.Clear();
            photoCache.Clear();
            if (savedChats != null)
                foreach (var sc in savedChats)
                {
                    if (sc == null || string.IsNullOrEmpty(sc.id)) continue;
                    chats[sc.id] = new Chat
                    {
                        id = sc.id, contact = sc.contact,
                        unread = sc.unread, awaiting = sc.awaiting, penalized = sc.penalized,
                        online = sc.online,
                        messages = new List<VNPhoneMessage>(sc.messages)
                    };
                    chatOrder.Add(sc.id);
                }
            currentChatId = chatId;
            if (currentChatId == null || !chats.ContainsKey(currentChatId))
                currentChatId = chatOrder.Count > 0 ? chatOrder[chatOrder.Count - 1] : null;
            pendingChoiceTexts = null; // 2.12.2: resumed flow re-runs the @choice
            pendingChoiceCb = null;
            var cur = CurrentChat();
            Contact = cur != null ? cur.contact : null;
            ApplyPosition(pos);
            this.chatMode = chatMode;
            IsMenuOpen = false;
            IsOpen = open && cur != null;
            if (IsOpen)
            {
                root.SetActive(true);
                root.transform.SetAsLastSibling();
                frame.anchoredPosition = new Vector2(currentX, 0f);
                if (chatMode) { IsMenuOpen = true; ShowScreen(Screen.ChatList); }
                else ShowScreen(Screen.Story); // legacy saves → live read screen
                if (Opened != null) Opened();
            }
            else
            {
                frame.anchoredPosition = new Vector2(currentX, hiddenY);
                root.SetActive(false);
                if (Closed != null) Closed();
            }
        }

        /// <summary>Rollback restore: truncate the append-only history to the snapshot
        /// counts and bring back the unread/await flags.</summary>
        public void RestoreSnapshot(bool open, string chatId, string pos, Dictionary<string, VNPhoneChatSnap> states, bool chatMode)
        {
            StopSlide();
            HideTyping();
            var toRemove = new List<string>();
            foreach (var kv in chats)
            {
                VNPhoneChatSnap s;
                if (states == null || !states.TryGetValue(kv.Key, out s) || s == null) toRemove.Add(kv.Key);
                else
                {
                    if (kv.Value.messages.Count > s.count)
                        kv.Value.messages.RemoveRange(s.count, kv.Value.messages.Count - s.count);
                    kv.Value.unread = s.unread;
                    kv.Value.awaiting = s.awaiting;
                    kv.Value.penalized = s.penalized;
                    kv.Value.online = s.online;
                }
            }
            foreach (var id in toRemove) { chats.Remove(id); chatOrder.Remove(id); }

            currentChatId = chatId;
            if (currentChatId == null || !chats.ContainsKey(currentChatId))
                currentChatId = chatOrder.Count > 0 ? chatOrder[chatOrder.Count - 1] : null;
            pendingChoiceTexts = null; // 2.12.2: resumed flow re-runs the @choice
            pendingChoiceCb = null;
            var cur = CurrentChat();
            Contact = cur != null ? cur.contact : null;
            ApplyPosition(pos);
            this.chatMode = chatMode;
            IsMenuOpen = false;
            IsOpen = open && cur != null;
            if (IsOpen)
            {
                root.SetActive(true);
                root.transform.SetAsLastSibling();
                frame.anchoredPosition = new Vector2(currentX, 0f);
                if (chatMode) { IsMenuOpen = true; ShowScreen(Screen.ChatList); }
                else ShowScreen(Screen.Story); // legacy saves → live read screen
                if (Opened != null) Opened();
            }
            else
            {
                frame.anchoredPosition = new Vector2(currentX, hiddenY);
                root.SetActive(false);
                if (Closed != null) Closed();
            }
        }

        // ============================== internals ==============================

        static void ClearChildren(RectTransform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Object.Destroy(content.GetChild(i).gameObject);
        }

        static Color OutgoingColor()
        {
            Color a = UIFactory.AccentColor;
            return new Color(a.r * 0.75f, a.g * 0.75f, a.b * 0.75f, 1f);
        }

        /// <summary>Pin the reading screen to the newest message. Freshly rebuilt
        /// content reports stale rects this frame, so re-apply on the next frame —
        /// this keeps the scroll glued to the bottom when switching tabs/chats.</summary>
        void ScrollReadToBottom()
        {
            if (readScroll == null) return;
            Canvas.ForceUpdateCanvases();
            readScroll.verticalNormalizedPosition = 0f;
            engine.StartCoroutine(ScrollReadNextFrame());
        }

        IEnumerator ScrollReadNextFrame()
        {
            yield return null;
            if (readScroll != null) readScroll.verticalNormalizedPosition = 0f;
        }

        void ScrollStoryToBottom()
        {
            ApplyStoryScroll();
            // Freshly activated layers report stale rects this frame — re-apply
            // on the next frame so the column lands at the bottom for sure.
            engine.StartCoroutine(ScrollStoryNextFrame());
        }

        IEnumerator ScrollStoryNextFrame()
        {
            yield return null;
            ApplyStoryScroll();
        }

        void ApplyStoryScroll()
        {
            Canvas.ForceUpdateCanvases();
            float contentH = storyContent.rect.height;
            float viewH = storyViewport.rect.height;
            // Bottom-aligned like a real messenger: contentH - viewH is NEGATIVE for
            // short chats, pinning the column to the bottom of the viewport.
            storyContent.anchoredPosition = new Vector2(0f, contentH - viewH);
        }

        void StartSlide(float targetY, float time, bool deactivateAtEnd)
        {
            StopSlide();
            slideRoutine = engine.StartCoroutine(SlideRoutine(targetY, Mathf.Max(0f, time), deactivateAtEnd));
        }

        void StopSlide()
        {
            if (slideRoutine != null) { engine.StopCoroutine(slideRoutine); slideRoutine = null; }
        }

        IEnumerator SlideRoutine(float targetY, float duration, bool deactivateAtEnd)
        {
            Vector2 from = frame.anchoredPosition;
            Vector2 to = new Vector2(from.x, targetY);
            float t = 0f;
            if (duration <= 0.01f) frame.anchoredPosition = to;
            else
            {
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / duration);
                    k = k * k * (3f - 2f * k); // smoothstep
                    frame.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
                    yield return null;
                }
                frame.anchoredPosition = to;
            }
            slideRoutine = null;
            if (deactivateAtEnd)
            {
                ClearChildren(storyContent);
                typingBubble = null;
                root.SetActive(false);
            }
        }
    }
}
