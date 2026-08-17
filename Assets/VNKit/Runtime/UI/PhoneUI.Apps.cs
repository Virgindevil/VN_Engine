using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VNKit
{
    /// <summary>
    /// 2.12 phone gameplay layer — partial of PhoneUI:
    ///   Home apps: Chats / Gallery / Contacts / Notes / Schedule / Games (+ save/load/...),
    ///   gated per-app via @phoneapp.
    ///   Contextual chat actions (@chatActions) rendered as buttons in the reading screen;
    ///   they are tappable only while the script is parked at @waitchat / @phonehub.
    ///   @phonehub "continue" button (VNLoc "phone.continue") on the chat list.
    ///   Message state mirroring: @msg id:x → variables message.x.received / .read / .answered.
    /// All state lives in serializable POCOs (Save/SaveData.cs) so save/load and
    /// rollback stay consistent; the UI never owns game logic beyond these lists.
    /// </summary>
    public partial class PhoneUI
    {
        // ---- 2.12 data ----
        readonly List<VNPhoneNote> notes = new List<VNPhoneNote>();
        readonly List<VNScheduleEvent> scheduleEvents = new List<VNScheduleEvent>();
        readonly List<VNPhoneGalleryItem> galleryItems = new List<VNPhoneGalleryItem>();
        readonly List<VNPhoneAction> chatActions = new List<VNPhoneAction>();
        readonly HashSet<string> hiddenApps = new HashSet<string>();
        int actionsVersion; // bumped on any action-list change (drives action bar refresh)

        // ---- 2.12 UI (app layers are built lazily) ----
        GameObject contactsLayer, notesLayer, scheduleLayer, gamesLayer;
        RectTransform contactsContent, notesContent, scheduleContent, gamesContent;
        // 2.12.1: contact card (Contacts → персонаж) + in-phone settings screen
        GameObject contactCardLayer, settingsLayer;
        RectTransform contactCardContent, settingsContent;
        string contactCardId = ""; // contact whose card is open
        GameObject actionBar;
        RectTransform actionBarContent;
        GameObject hubContinue;
        int lastBarKey = int.MinValue;

        // ---- in-phone choice (2.12.2) ----
        // A @choice reached during a live chat dialogue owns the action bar:
        // its options render as buttons at the bottom of the chat instead of
        // the full-screen overlay.
        List<string> pendingChoiceTexts;
        System.Action<int> pendingChoiceCb;

        /// <summary>True while a @choice waits for the player inside the chat.</summary>
        public bool HasPendingChoice { get { return pendingChoiceTexts != null; } }

        /// <summary>2.12.2: show a @choice as buttons at the bottom of the active
        /// chat. Makes sure the chat is actually on screen first (the player may
        /// have backed out to the chat list after the last line).</summary>
        public void ShowChoice(List<string> texts, System.Action<int> onPick)
        {
            if (texts == null || texts.Count == 0) { if (onPick != null) onPick(0); return; }
            pendingChoiceTexts = texts;
            pendingChoiceCb = onPick;
            if (!string.IsNullOrEmpty(CurrentChatId)) RevealChat(CurrentChatId);
            RefreshActionBarIfViewing(readChatId);
        }

        /// <summary>Drop a pending in-phone choice (picked, rollback, reset).</summary>
        public void HideChoice()
        {
            if (pendingChoiceTexts == null) return;
            pendingChoiceTexts = null;
            pendingChoiceCb = null;
            RefreshActionBarIfViewing(readChatId);
        }

        // ============================== home apps / gating ==============================

        /// <summary>Rebuild the home screen app grid. Called on every visit so
        /// @phoneapp changes apply immediately.</summary>
        void BuildHome()
        {
            ClearChildren((RectTransform)homeLayer.transform);
            // 2.12.1 order: Chats Gallery Contacts Schedule Notes Games Settings.
            // Save/Load/Preferences/Title live inside the Settings screen.
            AddApp("chats", "phone.app.chats", delegate { ShowScreen(Screen.ChatList); });
            AddApp("gallery", "phone.app.gallery", delegate { ShowScreen(Screen.Gallery); });
            AddApp("contacts", "phone.app.contacts", delegate { ShowScreen(Screen.Contacts); });
            AddApp("schedule", "phone.app.schedule", delegate { ShowScreen(Screen.Schedule); });
            AddApp("notes", "phone.app.notes", delegate { ShowScreen(Screen.Notes); });
            AddApp("games", "phone.app.games", delegate { ShowScreen(Screen.Games); });
            AddApp("settings", "phone.app.settings", delegate { ShowScreen(Screen.PhoneSettings); });
        }

        void AddApp(string appId, string locKey, UnityEngine.Events.UnityAction action)
        {
            if (hiddenApps.Contains(appId)) return;
            AddAppButton(homeLayer.transform, locKey, action);
        }

        /// <summary>@phoneapp gallery off — hide/show a home screen app.</summary>
        public void SetAppHidden(string appId, bool hidden)
        {
            if (string.IsNullOrEmpty(appId)) return;
            if (hidden) hiddenApps.Add(appId);
            else hiddenApps.Remove(appId);
            if (currentScreen == Screen.Home) BuildHome();
        }

        public bool IsAppHidden(string appId)
        {
            return appId != null && hiddenApps.Contains(appId);
        }

        public List<string> GetHiddenApps()
        {
            return new List<string>(hiddenApps);
        }

        public void RestoreHiddenApps(List<string> apps)
        {
            hiddenApps.Clear();
            if (apps != null)
                foreach (var a in apps)
                    if (!string.IsNullOrEmpty(a)) hiddenApps.Add(a);
            if (currentScreen == Screen.Home) BuildHome();
        }

        // ============================== app layers ==============================

        void SetAppLayers(Screen s)
        {
            EnsureAppLayers();
            if (contactsLayer != null) contactsLayer.SetActive(s == Screen.Contacts);
            if (notesLayer != null) notesLayer.SetActive(s == Screen.Notes);
            if (scheduleLayer != null) scheduleLayer.SetActive(s == Screen.Schedule);
            if (gamesLayer != null) gamesLayer.SetActive(s == Screen.Games);
            if (contactCardLayer != null) contactCardLayer.SetActive(s == Screen.ContactCard);
            if (settingsLayer != null) settingsLayer.SetActive(s == Screen.PhoneSettings);
        }

        void EnsureAppLayers()
        {
            if (contactsLayer != null) return;
            contactsLayer = MakeAppLayer("Contacts", out contactsContent);
            notesLayer = MakeAppLayer("Notes", out notesContent);
            scheduleLayer = MakeAppLayer("Schedule", out scheduleContent);
            gamesLayer = MakeAppLayer("Games", out gamesContent);
            contactCardLayer = MakeAppLayer("ContactCard", out contactCardContent);
            settingsLayer = MakeAppLayer("PhoneSettings", out settingsContent);
        }

        GameObject MakeAppLayer(string name, out RectTransform content)
        {
            var layer = UIFactory.Rect(name, screen).gameObject;
            var scroll = UIFactory.ScrollView(layer.transform, "Scroll", out content);
            UIFactory.Stretch((RectTransform)scroll.transform);
            var rt = (RectTransform)layer.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 6f);
            rt.offsetMax = new Vector2(-6f, -HeaderHeight - 6f);
            layer.SetActive(false);
            return layer;
        }

        // ============================== contacts ==============================

        void BuildContacts()
        {
            ClearChildren(contactsContent);
            if (chatOrder.Count == 0)
            {
                UIFactory.Text(contactsContent, "Empty", VNLoc.T("phone.nocontacts"), 24,
                    TextAnchor.MiddleCenter, SubTextColor);
                return;
            }
            foreach (var id in chatOrder)
            {
                var chat = chats[id];
                var c = chat; // per-iteration capture
                string live = c.online ? "<color=#7f7>●</color> " : "";
                string rel = RelationshipLine(c.id);
                // 2.12.1: a contact opens its card (relations + actions + chat link),
                // not the chat list — "Contacts не должны открывать список чатов".
                var b = UIFactory.Button(contactsContent, "Contact." + c.id,
                    live + c.contact + rel, 24, delegate { OpenContactCard(c.id); });
                UIFactory.Layout(b.gameObject, 0f, rel.Length > 0 ? 108f : 72f);
            }
        }

        /// <summary>Relationship readout for a contact: affection.&lt;id&gt; /
        /// trust.&lt;id&gt; / reliability.&lt;id&gt; variables (shown only while
        /// non-zero — no separate relationship system).</summary>
        string RelationshipLine(string id)
        {
            if (engine == null || engine.Variables == null) return "";
            float aff = engine.Variables.GetFloat("affection." + id);
            float tr = engine.Variables.GetFloat("trust." + id);
            float rel = engine.Variables.GetFloat("reliability." + id);
            if (aff == 0f && tr == 0f && rel == 0f) return "";
            return "\n<color=#9aa>" + VNLoc.T("phone.affection") + " " + aff.ToString("0")
                 + "   " + VNLoc.T("phone.trust") + " " + tr.ToString("0")
                 + "   " + VNLoc.T("phone.reliability") + " " + rel.ToString("0") + "</color>";
        }

        // ============================== contact card (2.12.1) ==============================

        /// <summary>Contacts → персонаж: open the character card.</summary>
        public void OpenContactCard(string chatId)
        {
            if (string.IsNullOrEmpty(chatId) || !chats.ContainsKey(chatId)) return;
            contactCardId = chatId;
            ShowScreen(Screen.ContactCard);
        }

        /// <summary>Character card: relationship variables, the chat's contextual
        /// actions (same @chatActions list — tappable only while parked) and a
        /// button that opens the chat itself.</summary>
        void BuildContactCard()
        {
            if (contactCardContent == null) return;
            ClearChildren(contactCardContent);
            Chat c;
            if (string.IsNullOrEmpty(contactCardId) || !chats.TryGetValue(contactCardId, out c))
            {
                UIFactory.Text(contactCardContent, "Empty", VNLoc.T("phone.nocontacts"), 24,
                    TextAnchor.MiddleCenter, SubTextColor);
                return;
            }
            titleText.text = c.contact;

            string live = c.online ? " <color=#7f7>●</color>" : "";
            var head = UIFactory.Text(contactCardContent, "Name", c.contact + live, 32,
                TextAnchor.MiddleCenter, UIFactory.TextColor);
            head.raycastTarget = false;
            UIFactory.Layout(head.gameObject, 0f, 56f);

            if (engine != null && engine.Variables != null)
            {
                float aff = engine.Variables.GetFloat("affection." + c.id);
                float tr = engine.Variables.GetFloat("trust." + c.id);
                float rel = engine.Variables.GetFloat("reliability." + c.id);
                var relText = UIFactory.Text(contactCardContent, "Rel",
                    VNLoc.T("phone.affection") + " " + aff.ToString("0") + "\n"
                    + VNLoc.T("phone.trust") + " " + tr.ToString("0") + "\n"
                    + VNLoc.T("phone.reliability") + " " + rel.ToString("0"),
                    24, TextAnchor.MiddleCenter, SubTextColor);
                relText.raycastTarget = false;
                UIFactory.Layout(relText.gameObject, 0f, 96f);
            }

            // Actions appear only while they actually work (script parked at
            // @waitchat/@phonehub) — same rule as the chat's action bar.
            bool parked = engine != null && engine.Player != null
                && (engine.Player.State == PlayerState.WaitingChat
                    || engine.Player.State == PlayerState.WaitingChatHub);
            var visible = parked ? GetVisibleActions(c.id) : new List<VNPhoneAction>();
            if (visible.Count > 0)
            {
                var lbl = UIFactory.Text(contactCardContent, "ActionsTitle", VNLoc.T("phone.actions"), 22,
                    TextAnchor.MiddleCenter, SubTextColor);
                lbl.raycastTarget = false;
                UIFactory.Layout(lbl.gameObject, 0f, 40f);
                foreach (var a in visible)
                {
                    var act = a;
                    var ab = UIFactory.Button(contactCardContent, "Action", act.text, 24,
                        delegate { if (engine != null && engine.Player != null) engine.Player.OnChatAction(act); });
                    UIFactory.Layout(ab.gameObject, 0f, 60f);
                }
            }

            var chatBtn = UIFactory.Button(contactCardContent, "OpenChat", VNLoc.T("phone.openchat"), 24,
                delegate { OpenReadChat(c.id); });
            UIFactory.Layout(chatBtn.gameObject, 0f, 64f);
            lastBarKey = BarKey(); // Tick() rebuilds the card when the parked state flips
        }

        // ============================== phone settings (2.12.1) ==============================

        /// <summary>The Settings app absorbs the existing panels — no new settings
        /// systems. Inner entries keep their @phoneapp ids (prefs/save/load/title)
        /// so scripts can still gate them individually.</summary>
        void BuildPhoneSettings()
        {
            if (settingsContent == null) return;
            ClearChildren(settingsContent);
            AddSettingsEntry("prefs", "phone.prefs", delegate { if (engine != null) engine.OpenSettings(); });
            AddSettingsEntry("save", "phone.app.save", delegate { if (engine != null) engine.OpenSavePanel(); });
            AddSettingsEntry("load", "phone.app.load", delegate { if (engine != null) engine.OpenLoadPanel(); });
            AddSettingsEntry("title", "phone.app.title", delegate { if (engine != null) engine.ReturnToTitle(); });
        }

        void AddSettingsEntry(string id, string locKey, UnityEngine.Events.UnityAction action)
        {
            if (hiddenApps.Contains(id)) return;
            var b = UIFactory.Button(settingsContent, "Set." + id, VNLoc.T(locKey), 26, action);
            UIFactory.Layout(b.gameObject, 0f, 72f);
        }

        // ============================== notes ==============================

        void BuildNotes()
        {
            ClearChildren(notesContent);
            if (notes.Count == 0)
            {
                UIFactory.Text(notesContent, "Empty", VNLoc.T("phone.nonotes"), 24,
                    TextAnchor.MiddleCenter, SubTextColor);
                return;
            }
            foreach (var n in notes)
            {
                // 2.12.1: category / source caption (category "general" is the default
                // and stays silent, exactly like before).
                string cap = "";
                bool defCat = string.IsNullOrEmpty(n.category) || n.category == "general";
                if (!defCat)
                {
                    // canonical categories are localized; free-form ones show as-is
                    string loc = VNLoc.T("note.cat." + n.category);
                    cap = loc.StartsWith("note.cat.") ? n.category : loc;
                }
                if (!string.IsNullOrEmpty(n.source))
                    cap += (cap.Length > 0 ? " · " : "") + n.source;
                if (cap.Length > 0)
                {
                    var ct = UIFactory.Text(notesContent, "NoteCat." + n.id,
                        "<color=#9aa>" + cap + "</color>", 18, TextAnchor.UpperLeft, SubTextColor);
                    ct.raycastTarget = false;
                    UIFactory.Layout(ct.gameObject, 0f, 26f);
                }
                string body = (n.important ? "<color=#fc6>★ </color>" : "") + (n.text ?? "");
                var t = UIFactory.Text(notesContent, "Note." + n.id, body, 24,
                    TextAnchor.UpperLeft, UIFactory.TextColor);
                t.raycastTarget = false;
                t.enableWordWrapping = true;
                int lines = 1 + (n.text != null ? n.text.Length / 28 : 0);
                UIFactory.Layout(t.gameObject, 0f, 30f + 30f * lines);
            }
        }

        /// <summary>@note add "text" [id:x] [important:1] — returns the note id
        /// (auto-generated when omitted).</summary>
        public string AddNote(string text, string id, bool important)
        {
            return AddNote(text, id, important, "general", "");
        }

        /// <summary>2.12.1: @note add "text" [category:evidence] [source:rin] — the
        /// category is free-form; the six canonical ones have VNLoc captions.</summary>
        public string AddNote(string text, string id, bool important, string category, string source)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (string.IsNullOrEmpty(id)) id = NextFreeId("note", NoteIds());
            notes.Add(new VNPhoneNote
            {
                id = id, text = text, important = important,
                category = string.IsNullOrEmpty(category) ? "general" : category,
                source = source ?? ""
            });
            if (currentScreen == Screen.Notes) BuildNotes();
            return id;
        }

        public void EditNote(string id, string text)
        {
            var n = FindNote(id);
            if (n == null) { VNLog.Warn("@note edit: no note with id '" + id + "'."); return; }
            if (!string.IsNullOrEmpty(text)) n.text = text;
            if (currentScreen == Screen.Notes) BuildNotes();
        }

        /// <summary>@note star id:x [important:0|1] — toggles the star when no value given.</summary>
        public void StarNote(string id, bool? important)
        {
            var n = FindNote(id);
            if (n == null) { VNLog.Warn("@note star: no note with id '" + id + "'."); return; }
            n.important = important.HasValue ? important.Value : !n.important;
            if (currentScreen == Screen.Notes) BuildNotes();
        }

        public void RemoveNote(string id)
        {
            notes.RemoveAll(n => n.id == id);
            if (currentScreen == Screen.Notes) BuildNotes();
        }

        public void ClearNotes()
        {
            notes.Clear();
            if (currentScreen == Screen.Notes) BuildNotes();
        }

        VNPhoneNote FindNote(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var n in notes) if (n.id == id) return n;
            return null;
        }

        IEnumerable<string> NoteIds()
        {
            foreach (var n in notes) yield return n.id;
        }

        public List<VNPhoneNote> GetNotes()
        {
            var l = new List<VNPhoneNote>();
            foreach (var n in notes)
                l.Add(new VNPhoneNote
                {
                    id = n.id, text = n.text, important = n.important,
                    category = n.category, source = n.source
                });
            return l;
        }

        public void RestoreNotes(List<VNPhoneNote> list)
        {
            notes.Clear();
            if (list != null)
                foreach (var n in list)
                    if (n != null && !string.IsNullOrEmpty(n.text))
                        notes.Add(new VNPhoneNote
                        {
                            id = n.id, text = n.text, important = n.important,
                            category = n.category, source = n.source
                        });
            if (currentScreen == Screen.Notes) BuildNotes();
        }

        // ============================== schedule ==============================

        void BuildSchedule()
        {
            ClearChildren(scheduleContent);
            if (scheduleEvents.Count == 0)
            {
                UIFactory.Text(scheduleContent, "Empty", VNLoc.T("phone.noschedule"), 24,
                    TextAnchor.MiddleCenter, SubTextColor);
                return;
            }
            foreach (var e in scheduleEvents)
            {
                string body = string.IsNullOrEmpty(e.time)
                    ? e.title
                    : "<b>" + e.time + "</b>  —  " + e.title;
                var t = UIFactory.Text(scheduleContent, "Event." + e.id, body, 24,
                    TextAnchor.UpperLeft, UIFactory.TextColor);
                t.raycastTarget = false;
                t.enableWordWrapping = true;
                UIFactory.Layout(t.gameObject, 0f, 56f);
            }
        }

        /// <summary>@schedule add time:"18:00" title:"..." [id:ev1] — returns the event id.</summary>
        public string AddScheduleEvent(string time, string title, string id)
        {
            if (string.IsNullOrEmpty(title)) return null;
            if (string.IsNullOrEmpty(id)) id = NextFreeId("event", ScheduleIds());
            scheduleEvents.Add(new VNScheduleEvent { id = id, time = time ?? "", title = title });
            if (currentScreen == Screen.Schedule) BuildSchedule();
            return id;
        }

        public void RemoveScheduleEvent(string id)
        {
            scheduleEvents.RemoveAll(e => e.id == id);
            if (currentScreen == Screen.Schedule) BuildSchedule();
        }

        public void ClearSchedule()
        {
            scheduleEvents.Clear();
            if (currentScreen == Screen.Schedule) BuildSchedule();
        }

        IEnumerable<string> ScheduleIds()
        {
            foreach (var e in scheduleEvents) yield return e.id;
        }

        public List<VNScheduleEvent> GetSchedule()
        {
            var l = new List<VNScheduleEvent>();
            foreach (var e in scheduleEvents)
                l.Add(new VNScheduleEvent { id = e.id, time = e.time, title = e.title });
            return l;
        }

        public void RestoreSchedule(List<VNScheduleEvent> list)
        {
            scheduleEvents.Clear();
            if (list != null)
                foreach (var e in list)
                    if (e != null && !string.IsNullOrEmpty(e.title))
                        scheduleEvents.Add(new VNScheduleEvent { id = e.id, time = e.time, title = e.title });
            if (currentScreen == Screen.Schedule) BuildSchedule();
        }

        // ============================== gallery ==============================

        /// <summary>@gallery add cg/x [id:roof1] [locked:1] [sender:] [date:] [location:]
        /// [desc:] [tag:] [important:1]. Same id (or address when no id) again → the
        /// meta is updated, not duplicated. The id mirrors into variables:
        /// gallery.&lt;id&gt;.locked / .viewed.</summary>
        public void AddGalleryItem(VNPhoneGalleryItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.address)) return;
            if (string.IsNullOrEmpty(item.id)) item.id = NextFreeId("g", GalleryIds());
            galleryItems.RemoveAll(g => g.id == item.id || g.address == item.address);
            galleryItems.Add(item);
            SetGalleryVar(item.id, "locked", item.locked);
            if (item.viewed) SetGalleryVar(item.id, "viewed", true);
            if (currentScreen == Screen.Gallery) BuildGallery();
        }

        IEnumerable<string> GalleryIds()
        {
            foreach (var g in galleryItems) yield return g.id;
        }

        /// <summary>gallery.&lt;id&gt;.&lt;field&gt; = 1|0 — script-checkable gallery
        /// state via the variables table (save/load/rollback come for free).</summary>
        void SetGalleryVar(string id, string field, bool value)
        {
            if (engine == null || engine.Variables == null || string.IsNullOrEmpty(id)) return;
            engine.Variables.Set("gallery." + id + "." + field, VNValue.FromNumber(value ? 1 : 0));
        }

        /// <summary>@gallery lock id:x / @gallery unlock id:x — locked photos are
        /// visible as dimmed placeholders but cannot be opened.</summary>
        public void SetGalleryLocked(string id, bool locked)
        {
            if (string.IsNullOrEmpty(id)) return;
            foreach (var g in galleryItems)
            {
                if (g.id != id && g.address != id) continue;
                g.locked = locked;
                SetGalleryVar(g.id, "locked", locked);
            }
            if (currentScreen == Screen.Gallery) BuildGallery();
        }

        /// <summary>Photo viewer opened a gallery photo → mark it viewed (script can
        /// then branch on gallery.&lt;id&gt;.viewed).</summary>
        public void MarkGalleryViewed(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            foreach (var g in galleryItems)
            {
                if (g.id != id) continue;
                if (!g.viewed)
                {
                    g.viewed = true;
                    SetGalleryVar(g.id, "viewed", true);
                }
                return;
            }
        }

        public void RemoveGalleryItem(string address)
        {
            galleryItems.RemoveAll(g => g.address == address);
            if (currentScreen == Screen.Gallery) BuildGallery();
        }

        public void ClearGallery()
        {
            galleryItems.Clear();
            if (currentScreen == Screen.Gallery) BuildGallery();
        }

        /// <summary>Renders @gallery items (photo tile + meta caption) into the
        /// gallery screen. Returns true when at least one item was rendered.</summary>
        bool BuildGalleryItems(RectTransform content)
        {
            bool any = false;
            foreach (var item in galleryItems)
            {
                any = true;
                if (item.locked)
                {
                    // locked: dimmed placeholder, not clickable
                    var lt = UIFactory.Text(content, "Locked." + item.id,
                        "<color=#9aa>" + VNLoc.T("phone.locked") + "</color>", 22,
                        TextAnchor.MiddleCenter, SubTextColor);
                    lt.raycastTarget = false;
                    UIFactory.Layout(lt.gameObject, 0f, 72f);
                }
                else
                {
                    var msg = new VNPhoneMessage { incoming = true, speaker = item.sender, text = item.address, kind = 1 };
                    CreatePhotoBubble(content, msg, item.id); // click → viewer + viewed mark
                }
                string meta = GalleryMeta(item);
                if (meta.Length > 0)
                {
                    var t = UIFactory.Text(content, "Meta", meta, 20, TextAnchor.UpperLeft, SubTextColor);
                    t.raycastTarget = false;
                    t.enableWordWrapping = true;
                    int lines = 1;
                    foreach (char ch in meta) if (ch == '\n') lines++;
                    UIFactory.Layout(t.gameObject, 0f, 8f + 26f * lines);
                }
            }
            return any;
        }

        static string GalleryMeta(VNPhoneGalleryItem item)
        {
            var head = new List<string>();
            if (!string.IsNullOrEmpty(item.sender)) head.Add(item.sender);
            if (!string.IsNullOrEmpty(item.date)) head.Add(item.date);
            if (!string.IsNullOrEmpty(item.location)) head.Add(item.location);
            string s = head.Count > 0 ? string.Join(" · ", head.ToArray()) : "";
            if (!string.IsNullOrEmpty(item.desc)) s += (s.Length > 0 ? "\n" : "") + item.desc;
            if (!string.IsNullOrEmpty(item.tag)) s += (s.Length > 0 ? "  " : "") + "<color=#9aa>#" + item.tag + "</color>";
            if (item.important) s = "<color=#fc6>★</color> " + s;
            if (item.viewed) s += (s.Length > 0 ? "  " : "") + "<color=#7f7>✓ " + VNLoc.T("phone.viewed") + "</color>";
            return s;
        }

        public List<VNPhoneGalleryItem> GetGalleryItems()
        {
            var l = new List<VNPhoneGalleryItem>();
            foreach (var g in galleryItems)
                l.Add(new VNPhoneGalleryItem
                {
                    id = g.id, address = g.address, sender = g.sender, date = g.date,
                    location = g.location, desc = g.desc, tag = g.tag, important = g.important,
                    viewed = g.viewed, locked = g.locked
                });
            return l;
        }

        public void RestoreGalleryItems(List<VNPhoneGalleryItem> list)
        {
            galleryItems.Clear();
            if (list != null)
                foreach (var g in list)
                    if (g != null && !string.IsNullOrEmpty(g.address))
                        galleryItems.Add(new VNPhoneGalleryItem
                        {
                            id = g.id, address = g.address, sender = g.sender, date = g.date,
                            location = g.location, desc = g.desc, tag = g.tag, important = g.important,
                            viewed = g.viewed, locked = g.locked
                        });
            if (currentScreen == Screen.Gallery) BuildGallery();
        }

        // ============================== games ==============================

        void BuildGames()
        {
            ClearChildren(gamesContent);
            var ids = VNMinigames.GetIds();
            if (ids.Count == 0)
            {
                UIFactory.Text(gamesContent, "Empty", VNLoc.T("phone.nogames"), 24,
                    TextAnchor.MiddleCenter, SubTextColor);
                return;
            }
            foreach (var id in ids)
            {
                var gameId = id;
                float plays = engine != null && engine.Variables != null
                    ? engine.Variables.GetFloat("phoneGame." + gameId + ".plays") : 0f;
                string label = gameId;
                if (plays > 0f)
                    label += "\n<color=#9aa>" + VNLoc.T("phone.game.plays") + " " + plays.ToString("0") + "</color>";
                var b = UIFactory.Button(gamesContent, "Game." + gameId, label, 26,
                    delegate { if (engine != null) engine.StartPhoneGame(gameId); });
                UIFactory.Layout(b.gameObject, 0f, plays > 0f ? 100f : 72f);
            }
        }

        // ============================== chat actions (@chatActions) ==============================

        /// <summary>@chatActions chat:rin [once:0] "Text" goto:Label [if:expr] [do:assign] | ...
        /// Replaces the chat's offer list. once=1 (default): a picked action disappears.</summary>
        public void SetChatActions(string chatId, List<VNChoiceOption> options, bool once)
        {
            if (string.IsNullOrEmpty(chatId) || options == null) return;
            chatActions.RemoveAll(a => a.chatId == chatId);
            foreach (var o in options)
            {
                if (o == null) continue;
                chatActions.Add(new VNPhoneAction
                {
                    chatId = chatId,
                    text = o.Text ?? "",
                    label = o.GotoLabel ?? "",
                    condition = o.Condition ?? "",
                    doAssign = o.DoAssign ?? "",
                    once = once,
                    used = false
                });
            }
            actionsVersion++;
            RefreshActionBarIfViewing(chatId);
        }

        /// <summary>@chatActions chat:rin clear — drop the chat's offer list.</summary>
        public void ClearChatActions(string chatId)
        {
            chatActions.RemoveAll(a => a.chatId == chatId);
            actionsVersion++;
            RefreshActionBarIfViewing(chatId);
        }

        /// <summary>Actions currently offered in the chat: unused, condition passed.</summary>
        public List<VNPhoneAction> GetVisibleActions(string chatId)
        {
            var list = new List<VNPhoneAction>();
            if (string.IsNullOrEmpty(chatId)) return list;
            foreach (var a in chatActions)
            {
                if (a.chatId != chatId || a.used) continue;
                if (!string.IsNullOrEmpty(a.condition)
                    && engine != null && engine.Variables != null
                    && !engine.Variables.Evaluate(a.condition)) continue;
                list.Add(a);
            }
            return list;
        }

        /// <summary>The player picked a contextual action (ScriptPlayer.OnChatAction):
        /// mark it used (when once) and push its text as the player's outgoing bubble —
        /// this also marks the last tracked incoming message as answered.</summary>
        public void MarkActionUsed(VNPhoneAction a)
        {
            if (a == null) return;
            if (a.once) a.used = true;
            if (!string.IsNullOrEmpty(a.text))
                PushMessage(a.chatId, false, null, a.text, 0, null, false);
            actionsVersion++;
            RefreshActionBarIfViewing(a.chatId);
        }

        public List<VNPhoneAction> GetActions()
        {
            var l = new List<VNPhoneAction>();
            foreach (var a in chatActions)
                l.Add(new VNPhoneAction
                {
                    chatId = a.chatId, text = a.text, label = a.label,
                    condition = a.condition, doAssign = a.doAssign, once = a.once, used = a.used
                });
            return l;
        }

        public void RestoreActions(List<VNPhoneAction> list)
        {
            chatActions.Clear();
            if (list != null)
                foreach (var a in list)
                    if (a != null && !string.IsNullOrEmpty(a.chatId))
                        chatActions.Add(new VNPhoneAction
                        {
                            chatId = a.chatId, text = a.text, label = a.label,
                            condition = a.condition, doAssign = a.doAssign, once = a.once, used = a.used
                        });
            actionsVersion++;
        }

        void RefreshActionBarIfViewing(string chatId)
        {
            if (currentScreen != Screen.ReadChat || readChatId != chatId) return;
            Chat c;
            if (chats.TryGetValue(chatId, out c)) RefreshActionBar(c);
        }

        // ============================== action bar / hub continue UI ==============================

        void EnsureActionBar()
        {
            if (actionBar != null) return;
            actionBar = UIFactory.Rect("Actions", readLayer.transform).gameObject;
            var rt = (RectTransform)actionBar.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(6f, 6f);
            rt.offsetMax = new Vector2(-6f, 6f);
            var img = UIFactory.AddImage(actionBar, HeaderColor);
            img.raycastTarget = false;
            var vlg = actionBar.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            actionBarContent = rt;
            actionBar.SetActive(false);
        }

        /// <summary>Rebuild the contextual action buttons of the reading screen.
        /// Buttons exist only while the script is parked at @waitchat/@phonehub —
        /// outside the parked state the bar HIDES entirely (a visible but dead
        /// button reads as "the buttons don't work"). Tick() refreshes the bar
        /// when the player state changes mid-screen.</summary>
        void RefreshActionBar(Chat chat)
        {
            if (actionBar == null) return;
            ClearChildren(actionBarContent);
            // 2.12.2: a pending in-phone @choice owns the bar, whatever the
            // parked state is (the script waits at WaitingChoice for it).
            if (pendingChoiceTexts != null)
            {
                float ch = 16f;
                for (int i = 0; i < pendingChoiceTexts.Count; i++)
                {
                    int idx = i;
                    var b = UIFactory.Button(actionBarContent, "Choice", pendingChoiceTexts[i], 24,
                        delegate
                        {
                            var cb = pendingChoiceCb;
                            HideChoice();
                            if (cb != null) cb(idx);
                        });
                    UIFactory.Layout(b.gameObject, 0f, 60f);
                    ch += 60f + 8f;
                }
                actionBar.SetActive(true);
                var crt = (RectTransform)actionBar.transform;
                crt.offsetMin = new Vector2(6f, 6f);
                crt.offsetMax = new Vector2(-6f, 6f + ch);
                SetReadBottomInset(ch + 12f);
                lastBarKey = BarKey();
                return;
            }
            var visible = GetVisibleActions(chat != null ? chat.id : null);
            bool parked = engine != null && engine.Player != null
                && (engine.Player.State == PlayerState.WaitingChat
                    || engine.Player.State == PlayerState.WaitingChatHub);
            float h = 16f; // vertical padding
            if (parked)
                foreach (var a in visible)
                {
                    var act = a;
                    var b = UIFactory.Button(actionBarContent, "Action", act.text, 24,
                        delegate { if (engine != null && engine.Player != null) engine.Player.OnChatAction(act); });
                    UIFactory.Layout(b.gameObject, 0f, 60f);
                    h += 60f + 8f;
                }
            if (!parked || visible.Count == 0)
            {
                actionBar.SetActive(false);
                SetReadBottomInset(0f);
            }
            else
            {
                actionBar.SetActive(true);
                var rt = (RectTransform)actionBar.transform;
                rt.offsetMin = new Vector2(6f, 6f);
                rt.offsetMax = new Vector2(-6f, 6f + h);
                SetReadBottomInset(h + 12f);
            }
            lastBarKey = BarKey();
        }

        void SetReadBottomInset(float inset)
        {
            if (readScroll == null) return;
            var rt = (RectTransform)readScroll.transform;
            rt.offsetMin = new Vector2(rt.offsetMin.x, inset);
        }

        int BarKey()
        {
            int st = engine != null && engine.Player != null ? (int)engine.Player.State : -1;
            return actionsVersion * 32 + st;
        }

        void EnsureHubContinue()
        {
            if (hubContinue != null) return;
            hubContinue = UIFactory.Rect("HubContinue", listLayer.transform).gameObject;
            var rt = (RectTransform)hubContinue.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(6f, 6f);
            rt.offsetMax = new Vector2(-6f, 82f);
            var img = UIFactory.AddImage(hubContinue, HeaderColor);
            img.raycastTarget = false;
            var b = UIFactory.Button(hubContinue.transform, "Continue", VNLoc.T("phone.continue"), 26,
                delegate { if (engine != null && engine.Player != null) engine.Player.ReleaseChatHub(); });
            UIFactory.Stretch((RectTransform)b.transform);
            var rtb = (RectTransform)b.transform;
            rtb.offsetMin = new Vector2(8f, 8f);
            rtb.offsetMax = new Vector2(-8f, -8f);
            hubContinue.SetActive(false);
        }

        /// <summary>«Далее» is visible on the chat list only while the script is
        /// parked at @phonehub — pressing it releases the hub.</summary>
        void RefreshHubContinue()
        {
            if (hubContinue == null) return;
            bool show = engine != null && engine.Player != null
                && engine.Player.State == PlayerState.WaitingChatHub;
            hubContinue.SetActive(show);
            if (listScroll != null)
            {
                var rt = (RectTransform)listScroll.transform;
                rt.offsetMin = new Vector2(rt.offsetMin.x, show ? 90f : 0f);
            }
        }

        /// <summary>Per-frame upkeep (called by the engine): refreshes the action bar
        /// and the «Далее» button when the player state changed without a screen rebuild.</summary>
        public void Tick()
        {
            if (!IsMenuOpen) return;
            if (currentScreen == Screen.ChatList) RefreshHubContinue();
            else if (currentScreen == Screen.ReadChat && readChatId != null && BarKey() != lastBarKey)
            {
                Chat c;
                if (chats.TryGetValue(readChatId, out c)) RefreshActionBar(c);
            }
            else if (currentScreen == Screen.ContactCard && BarKey() != lastBarKey)
            {
                BuildContactCard(); // parked-state flip → action buttons (de)activate
            }
        }

        // ============================== message state variables ==============================

        /// <summary>2.12.1: @message expire id:x — a plain script state, not a real
        /// timer: message.x.expired = 1, after which the script branches itself.</summary>
        public void ExpireMessage(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            MarkMessageVar(id, "expired");
        }

        /// <summary>Reveal a chat screen from script flow (chat action with a goto):
        /// opens the phone menu when needed, then shows the chat so the player sees
        /// the context the branch continues in.</summary>
        public void RevealChat(string chatId)
        {
            if (string.IsNullOrEmpty(chatId)) return;
            if (!IsMenuOpen) OpenMenu();
            OpenReadChat(chatId);
        }

        /// <summary>message.&lt;id&gt;.&lt;field&gt; = 1 — script-visible message state
        /// (received / read / answered). Dotted names resolve via the variables table,
        /// so save/load and rollback cover them with no extra systems.</summary>
        void MarkMessageVar(string id, string field)
        {
            if (engine == null || engine.Variables == null || string.IsNullOrEmpty(id)) return;
            engine.Variables.Set("message." + id + "." + field, VNValue.FromNumber(1));
        }

        /// <summary>An outgoing bubble answers the newest tracked incoming message.</summary>
        void MarkLastIncomingAnswered(Chat chat)
        {
            for (int i = chat.messages.Count - 1; i >= 0; i--)
            {
                var m = chat.messages[i];
                if (m.incoming && !string.IsNullOrEmpty(m.id))
                {
                    MarkMessageVar(m.id, "answered");
                    return;
                }
            }
        }

        // ============================== misc ==============================

        void ResetAppsData()
        {
            notes.Clear();
            scheduleEvents.Clear();
            galleryItems.Clear();
            chatActions.Clear();
            hiddenApps.Clear();
            contactCardId = "";
            actionsVersion++;
        }

        static string NextFreeId(string prefix, IEnumerable<string> existing)
        {
            var set = new HashSet<string>(existing);
            int i = set.Count + 1;
            while (set.Contains(prefix + i)) i++;
            return prefix + i;
        }
    }
}
