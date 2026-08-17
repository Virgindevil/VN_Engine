using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VNKit
{
    /// <summary>
    /// Executes a parsed VNScript command by command.
    /// Owns the playback state machine, auto mode, skip mode and async asset waits.
    /// </summary>
    public class ScriptPlayer
    {
        readonly VisualNovelEngine engine;
        readonly VNRunner runner;

        VNScript script;
        int index;
        bool finishWait;
        bool skipHeld;
        float skipTimer;
        float autoTimer;
        List<VNChoiceOption> currentOptions;
        // 2.11: a chat-mode line held back until the player actually enters the chat
        VNCommand pendingSay;
        // 2.11: script position of the @waitchat hub — @chatend returns here
        int hubReturnIndex = -1;
        // 2.11.1: target chat of the last @msg — @photo without chat: lands there
        string lastMsgChatId;

        public PlayerState State { get; private set; }
        public bool AutoMode;
        public bool SkipMode;
        public bool CurrentLineSeen { get; private set; }
        public string CurrentScriptName { get { return script != null ? script.Name : null; } }
        public int NextCommandIndex { get { return index; } }
        public bool IsTyping { get { return engine.Dialogue != null && engine.Dialogue.IsTyping; } }
        /// <summary>@waitchat hub position (save/load support).</summary>
        public int ChatHubReturn { get { return hubReturnIndex; } set { hubReturnIndex = value; } }
        /// <summary>Reminder phrase from @waitchat ... remind:"..." — shown when the
        /// player puts the phone away with unfinished live dialogues pending.</summary>
        public string ChatReminder { get; set; }

        public ScriptPlayer(VisualNovelEngine engine, VNRunner runner)
        {
            this.engine = engine;
            this.runner = runner;
            State = PlayerState.Idle;
        }

        public void Play(VNScript s, int startIndex)
        {
            Stop();
            script = s;
            index = Mathf.Clamp(startIndex, 0, s.Commands.Count);
            State = PlayerState.Running;
            Step();
        }

        public void Stop()
        {
            if (runner != null) runner.StopAllCoroutines();
            finishWait = false;
            currentOptions = null;
            script = null;
            State = PlayerState.Idle;
        }

        public void SetSkipHeld(bool held) { skipHeld = held; }

        /// <summary>The player pressed "advance" (click / Space / Enter).</summary>
        public void Advance()
        {
            if (IsTyping) { engine.Dialogue.CompleteLine(); return; }
            if (State == PlayerState.WaitingInput)
            {
                State = PlayerState.Running;
                Step();
            }
            else if (State == PlayerState.WaitingTimer)
            {
                finishWait = true;
            }
        }

        public void CompleteTyping()
        {
            if (IsTyping) engine.Dialogue.CompleteLine();
        }

        public void Tick(float dt)
        {
            bool skipping = (SkipMode || skipHeld)
                            && State != PlayerState.WaitingChoice
                            && State != PlayerState.WaitingAsset
                            && State != PlayerState.WaitingMinigame
                            && State != PlayerState.WaitingTextInput
                            && State != PlayerState.Idle
                            && State != PlayerState.Ended;

            if (skipping)
            {
                // When skipUnreadOnly is on, toggle-skip stops at unread text.
                // Holding the skip key always skips everything.
                bool stopAtUnread = engine.Settings.skipUnreadOnly
                                    && SkipMode && !skipHeld
                                    && !CurrentLineSeen
                                    && (IsTyping || State == PlayerState.WaitingInput);
                if (stopAtUnread)
                {
                    SkipMode = false;
                    engine.RefreshQuickMenuToggles();
                }
                else if (IsTyping || State == PlayerState.WaitingInput)
                {
                    skipTimer += dt;
                    if (skipTimer >= 0.05f) { skipTimer = 0f; Advance(); }
                }
            }
            else skipTimer = 0f;

            if (AutoMode && State == PlayerState.WaitingInput && !IsTyping)
            {
                autoTimer += dt;
                if (autoTimer >= engine.Settings.autoDelay) { autoTimer = 0f; Advance(); }
            }
            else autoTimer = 0f;
        }

        void Step()
        {
            while (State == PlayerState.Running)
            {
                if (script == null || index >= script.Commands.Count) { FinishScript(); return; }

                var cmd = script.Commands[index++];
                switch (cmd.Type)
                {
                    case VNCommandType.Say:    DoSay(cmd); return;
                    case VNCommandType.Wait:   DoWait(cmd); return;
                    case VNCommandType.Choice: if (DoChoice(cmd)) return; break;
                    case VNCommandType.End:    FinishScript(); return;

                    case VNCommandType.Char:
                        State = PlayerState.WaitingAsset;
                        runner.StartCoroutine(CoChar(cmd));
                        return;
                    case VNCommandType.HideChar:  engine.Characters.Hide(cmd.Name, cmd.GetFloat("time", 0.35f)); break;
                    case VNCommandType.HideChars: engine.Characters.HideAll(cmd.GetFloat("time", 0.35f)); break;
                    case VNCommandType.Background:
                        State = PlayerState.WaitingAsset;
                        runner.StartCoroutine(CoBackground(cmd));
                        return;

                    case VNCommandType.Cg:
                        State = PlayerState.WaitingAsset;
                        runner.StartCoroutine(CoCg(cmd));
                        return;

                    case VNCommandType.Input:  DoTextInput(cmd); return;
                    case VNCommandType.Phone:  DoPhone(cmd); break;
                    case VNCommandType.Photo:  DoPhoto(cmd); return;
                    case VNCommandType.PhoneMsg: if (DoMsg(cmd)) return; break;
                    case VNCommandType.ChatTarget: DoChat(cmd); break;
                    case VNCommandType.PhoneMenuToggle: DoPhoneMenuToggle(cmd); break;
                    case VNCommandType.WaitChat: if (DoWaitChat(cmd)) return; break;
                    case VNCommandType.ChatEnd:  DoChatEnd(cmd); return;
                    case VNCommandType.Typing: DoTyping(cmd); return;
                    case VNCommandType.Fade:   DoFade(cmd); break;
                    case VNCommandType.Minigame:
                        DoMinigame(cmd);
                        return;

                    case VNCommandType.Bgm:
                        State = PlayerState.WaitingAsset;
                        runner.StartCoroutine(CoBgm(cmd));
                        return;
                    case VNCommandType.StopBgm: engine.Audio.StopBgm(cmd.GetFloat("fade", 1f)); break;
                    case VNCommandType.Sfx:
                        State = PlayerState.WaitingAsset;
                        runner.StartCoroutine(CoSfx(cmd));
                        return;
                    case VNCommandType.Voice:
                        State = PlayerState.WaitingAsset;
                        runner.StartCoroutine(CoVoice(cmd));
                        return;
                    case VNCommandType.StopVoice: engine.Audio.StopVoice(); break;

                    case VNCommandType.Set: engine.Variables.Apply(cmd.Assignments); break;

                    case VNCommandType.Goto:
                        if (!DoGoto(cmd.GotoLabel)) { FinishScript(); return; }
                        break;

                    case VNCommandType.If:
                    {
                        bool r = engine.Variables.Evaluate(cmd.Expression);
                        string lbl = r ? cmd.GotoLabel : cmd.ElseLabel;
                        if (!string.IsNullOrEmpty(lbl) && !DoGoto(lbl)) { FinishScript(); return; }
                        break;
                    }

                    case VNCommandType.Custom:
                        engine.RaiseCustomCommand(cmd);
                        break;
                }
            }
        }

        void DoSay(VNCommand cmd)
        {
            // Rollback snapshot: capture the state right before this line is shown.
            engine.CaptureRollback(script.Name, index - 1);

            // Appearance change may need an async asset load first.
            if (!string.IsNullOrEmpty(cmd.Appearance) && !string.IsNullOrEmpty(cmd.Speaker))
            {
                State = PlayerState.WaitingAsset;
                runner.StartCoroutine(CoSayWithAppearance(cmd));
                return;
            }

            PlaySay(cmd);
        }

        IEnumerator CoSayWithAppearance(VNCommand cmd)
        {
            Sprite spr = null;
            Object skel = null;
            if (engine.GetSpineCharacter(cmd.Speaker) != null)
                yield return VNSpineActor.LoadSkeleton(engine.GetSpineCharacter(cmd.Speaker).skeletonAddress, s => skel = s);
            else
                yield return engine.LoadCharacterSpriteAsync(cmd.Speaker, cmd.Appearance, s => spr = s);
            engine.Characters.SetAppearance(cmd.Speaker, cmd.Appearance, spr, skel);
            State = PlayerState.Running; // same as the sync DoSay path while the typewriter runs
            PlaySay(cmd);
        }

        void PlaySay(VNCommand cmd)
        {
            // 2.11: in chat mode a line (dialogue or narration) plays only while the
            // player is INSIDE the current chat — otherwise it is held until the
            // chat is opened (no scrolling the conversation from outside).
            if (engine.Phone != null && engine.Phone.ChatMode
                && !engine.Phone.IsViewingChat(engine.Phone.CurrentChatId))
            {
                pendingSay = cmd;
                State = PlayerState.WaitingChatEnter;
                return;
            }
            engine.Audio.StopVoice();
            CurrentLineSeen = engine.IsLineSeen(script.Name, cmd.LineNumber);
            engine.MarkLineSeen(script.Name, cmd.LineNumber);
            // {variable} references in speaker names and text are expanded here,
            // so the backlog stores the already-expanded text.
            string speaker = engine.Variables.Expand(cmd.Speaker);
            string text = engine.Variables.Expand(cmd.Text);
            engine.AddBacklog(speaker, text);
            // While the phone messenger is open, spoken lines become chat bubbles
            // (narration — lines without a speaker — stays on the dialogue panel).
            if (engine.Phone != null && engine.Phone.IsOpen && !string.IsNullOrEmpty(speaker))
            {
                engine.Phone.AddMessage(!IsSelfSpeaker(speaker), speaker, text);
                State = PlayerState.WaitingInput;
                return;
            }
            engine.Dialogue.PlayLine(speaker, text, OnLineFinished);
        }

        /// <summary>
        /// Outgoing chat bubble detection: the speaker is the player when it matches
        /// the {playerName} variable or one of the fixed self-aliases.
        /// </summary>
        bool IsSelfSpeaker(string speaker)
        {
            string pn = engine.Variables.GetString("playerName");
            if (!string.IsNullOrEmpty(pn) && speaker == pn) return true;
            string s = speaker.Trim().ToLowerInvariant();
            return s == "me" || s == "я" || s == "гг" || s == "mc" || s == "player";
        }

        void OnLineFinished()
        {
            State = PlayerState.WaitingInput;
        }

        void DoWait(VNCommand cmd)
        {
            float d = cmd.GetFloat("time", -1f);
            if (d < 0f && !string.IsNullOrEmpty(cmd.Name))
            {
                if (!float.TryParse(cmd.Name, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out d))
                    d = 1f;
            }
            if (d < 0f) d = 1f;

            State = PlayerState.WaitingTimer;
            finishWait = false;
            runner.StartCoroutine(WaitRoutine(d));
        }

        IEnumerator WaitRoutine(float duration)
        {
            float t = 0f;
            while (t < duration && !finishWait)
            {
                if (SkipMode || skipHeld) finishWait = true;
                t += Time.deltaTime;
                yield return null;
            }
            finishWait = false;
            State = PlayerState.Running;
            Step();
        }

        // ============================== Phone messenger ==============================

        void DoPhone(VNCommand cmd)
        {
            if (engine.Phone == null) return;
            string action = (cmd.Name ?? "open").Trim().ToLowerInvariant();
            float time = cmd.GetFloat("time", 0.4f);
            if (action == "close" || action == "off" || action == "hide")
                engine.Phone.Close(time);
            else if (action == "reset")
                engine.Phone.ResetAll();
            else if (action == "chats" || action == "menu")
                // @phone chats — open the phone straight into the chat list;
                // all scripted dialogues then play inside the Chats tab.
                engine.Phone.OpenChats(time);
            else
            {
                // @phone open contact:"Макс 🐶" chat:max pos:left  /  @phone open Макс
                string contact = engine.Variables.Expand(cmd.Get("contact", cmd.Pos));
                string dlg = engine.Variables.Expand(cmd.Get("goto"));
                if (!string.IsNullOrEmpty(dlg))
                {
                    // @online ... goto:Label (2.11): the contact comes online with a
                    // pending live dialogue — no forced navigation; the player picks
                    // the chat in the messenger, the script jumps to the label then.
                    // notify:0 suppresses the "online" toast.
                    engine.Phone.RegisterDialogue(engine.Variables.Expand(cmd.Get("chat")),
                        contact, dlg, cmd.GetFloat("notify", 1f) != 0f);
                }
                else
                    engine.Phone.Open(engine.Variables.Expand(cmd.Get("chat")), contact, time, cmd.Get("pos"));
            }
        }

        /// <summary>@chat max contact:"Макс 🐶" — aim the script at a chat: following
        /// spoken lines and @photo land there. Pair with @phone chats / @online so
        /// several dialogues in one .vns reach different characters' chats.</summary>
        void DoChat(VNCommand cmd)
        {
            engine.CaptureRollback(script.Name, index - 1);
            if (engine.Phone == null) { VNLog.Warn("@chat requires the phone UI. Line " + cmd.LineNumber); return; }
            string id = engine.Variables.Expand(cmd.Get("chat", cmd.Name));
            string contact = engine.Variables.Expand(cmd.Get("contact", cmd.Pos));
            engine.Phone.SetActiveChat(id, contact);
        }

        /// <summary>2.11: the player opened a chat screen. Resume a held chat line or,
        /// while the script is parked at @waitchat, jump to the chat's pending
        /// live dialogue (@online ... goto:Label).</summary>
        public void OnChatEntered(string chatId)
        {
            if (engine.Phone == null || script == null) return;
            if (State == PlayerState.WaitingChatEnter && pendingSay != null
                && engine.Phone.CurrentChatId == chatId)
            {
                var cmd = pendingSay;
                pendingSay = null;
                PlaySay(cmd); // now viewing → the line lands in the chat
                return;
            }
            if (State == PlayerState.WaitingChat)
            {
                string label = engine.Phone.PendingDialogueLabel(chatId);
                if (!string.IsNullOrEmpty(label))
                {
                    hubReturnIndex = index - 1; // re-run @waitchat after @chatend
                    engine.Phone.SetActiveChat(chatId, null);
                    if (DoGoto(label)) { State = PlayerState.Running; Step(); }
                }
            }
        }

        /// <summary>@waitchat [chat:]max,exes — the messenger hub: park the script
        /// until the listed chats' live dialogues are done (empty = all registered).
        /// Returns true while parked.</summary>
        bool DoWaitChat(VNCommand cmd)
        {
            if (engine.Phone == null) return false;
            // @waitchat ... remind:"Я ещё не со всеми поговорил" — phrase the player
            // sees if they pocket the phone while story dialogues are still pending.
            string remind = cmd.Get("remind");
            if (!string.IsNullOrEmpty(remind)) ChatReminder = engine.Variables.Expand(remind);
            // Forms: @waitchat chat:max,exes  /  @waitchat max,exes  /  @waitchat max exes
            string raw = engine.Variables.Expand(cmd.Get("chat"));
            if (string.IsNullOrEmpty(raw))
            {
                raw = engine.Variables.Expand(cmd.Name ?? "");
                string pos = engine.Variables.Expand(cmd.Pos ?? "");
                if (!string.IsNullOrEmpty(pos)) raw = string.IsNullOrEmpty(raw) ? pos : raw + "," + pos;
            }
            var ids = new List<string>();
            if (!string.IsNullOrEmpty(raw))
                foreach (var p in raw.Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
                    ids.Add(p.Trim());
            // No explicit list → wait for every registered dialogue.
            if (ids.Count == 0)
                foreach (var d in engine.Phone.GetDialogues())
                    if (!d.done) ids.Add(d.chatId);
            foreach (var id in ids)
                if (!engine.Phone.IsDialogueDone(id))
                {
                    State = PlayerState.WaitingChat;
                    return true;
                }
            return false;
        }

        /// <summary>@chatend [goto:Label] — finish the current chat's live dialogue
        /// and return to the @waitchat hub (or jump elsewhere / just continue).</summary>
        void DoChatEnd(VNCommand cmd)
        {
            engine.CaptureRollback(script.Name, index - 1);
            if (engine.Phone != null) engine.Phone.CompleteDialogue(engine.Phone.CurrentChatId);
            string gt = engine.Variables.Expand(cmd.Get("goto"));
            if (!string.IsNullOrEmpty(gt))
            {
                hubReturnIndex = -1;
                if (!DoGoto(gt)) { FinishScript(); return; }
            }
            else if (hubReturnIndex >= 0)
            {
                index = hubReturnIndex;
            }
            State = PlayerState.Running;
            Step();
        }

        /// <summary>@phoneOn / @phoneOff — switch the in-game menu style at runtime:
        /// phone menu (RMB/Esc → смартфон) vs the classic box menu.</summary>
        void DoPhoneMenuToggle(VNCommand cmd)
        {
            engine.CaptureRollback(script.Name, index - 1);
            engine.SetPhoneMenu(cmd.Name == "on");
        }

        /// <summary>@photo SunsetPic sender:"Макс" [chat:exes] — photo attachment.
        /// Without chat: goes to the active chat (or to the last @msg target —
        /// handy for background pre-fill blocks where no chat is active yet).</summary>
        void DoPhoto(VNCommand cmd)
        {
            engine.CaptureRollback(script.Name, index - 1);
            State = PlayerState.WaitingAsset;
            runner.StartCoroutine(CoPhoto(cmd));
        }

        IEnumerator CoPhoto(VNCommand cmd)
        {
            string address = cmd.Name;
            Sprite s = null;
            if (!string.IsNullOrEmpty(address))
                yield return engine.LoadCgAsync(address, x => s = x);

            string sender = engine.Variables.Expand(cmd.Get("sender", cmd.Pos));
            bool outgoing = !string.IsNullOrEmpty(sender) && IsSelfSpeaker(sender);
            string chatId = engine.Variables.Expand(cmd.Get("chat"));
            if (string.IsNullOrEmpty(chatId) && engine.Phone != null
                && !string.IsNullOrEmpty(engine.Phone.CurrentChatId))
                chatId = engine.Phone.CurrentChatId;
            if (string.IsNullOrEmpty(chatId)) chatId = lastMsgChatId;

            if (engine.Phone == null || string.IsNullOrEmpty(chatId))
            {
                VNLog.Warn("@photo: no target chat — add chat: or send a @msg first. Line " + cmd.LineNumber);
                State = PlayerState.Running;
                Step();
                yield break;
            }
            bool notify = cmd.GetFloat("notify", 1f) != 0f;
            engine.Phone.PushMessage(chatId, !outgoing, sender, address, 1, s, notify);
            engine.AddBacklog(sender, VNLoc.T("phone.photo"));

            // A photo into the chat the player is watching = a beat to click through;
            // background delivery = continue instantly (pre-fill chains need no clicks).
            if (engine.Phone.IsViewingChat(chatId)) State = PlayerState.WaitingInput;
            else { State = PlayerState.Running; Step(); }
        }

        // @msg — a message to ANY chat without switching the active one:
        //   @msg chat:family from:"Мама" "Ты где?"      (incoming → unread badge)
        //   @msg chat:family from:me "Скоро буду"        (outgoing, priority tracking)
        //   @msg chat:exes from:"Рин" photo:cg/selfie    (photo attachment)
        //   @msg chat:exes notify:0 from:"Рин" "..."     (silent: no toast banner)
        //   @msg "Привет"                                 (to the active chat)
        // Background messages land instantly without a click; a message into the
        // chat the player is watching is a beat (WaitingInput) as before.
        // Returns true while parked (WaitingInput / WaitingAsset) — Step() returns;
        // false = continue the command loop immediately.
        bool DoMsg(VNCommand cmd)
        {
            engine.CaptureRollback(script.Name, index - 1);
            string photo = engine.Variables.Expand(cmd.Get("photo"));
            if (!string.IsNullOrEmpty(photo))
            {
                State = PlayerState.WaitingAsset;
                runner.StartCoroutine(CoMsg(cmd, photo));
                return true;
            }
            DeliverMsg(cmd, null);
            return State != PlayerState.Running;
        }

        IEnumerator CoMsg(VNCommand cmd, string address)
        {
            Sprite s = null;
            yield return engine.LoadCgAsync(address, x => s = x);
            DeliverMsg(cmd, s);
            if (State == PlayerState.Running) Step();
        }

        void DeliverMsg(VNCommand cmd, Sprite sprite)
        {
            if (engine.Phone == null)
            {
                VNLog.Warn("@msg requires the phone UI. Line " + cmd.LineNumber);
                State = PlayerState.WaitingInput;
                return;
            }
            string chatId = engine.Variables.Expand(cmd.Get("chat"));
            if (string.IsNullOrEmpty(chatId)) chatId = engine.Phone.CurrentChatId;
            if (string.IsNullOrEmpty(chatId))
            {
                VNLog.Warn("@msg: no target chat — add chat: first. Line " + cmd.LineNumber);
                State = PlayerState.WaitingInput;
                return;
            }
            lastMsgChatId = chatId;
            string sender = engine.Variables.Expand(cmd.Get("from", cmd.Get("sender")));
            bool incoming = string.IsNullOrEmpty(sender) || !IsSelfSpeaker(sender);
            bool notify = cmd.GetFloat("notify", 1f) != 0f;
            if (sprite != null)
            {
                engine.Phone.PushMessage(chatId, incoming, sender,
                    engine.Variables.Expand(cmd.Get("photo")), 1, sprite, notify);
                engine.AddBacklog(sender, VNLoc.T("phone.photo"));
            }
            else
            {
                string text = engine.Variables.Expand(cmd.Name ?? "");
                engine.Phone.PushMessage(chatId, incoming, sender, text, 0, null, notify);
                engine.AddBacklog(sender, text);
            }
            if (engine.Phone.IsViewingChat(chatId)) State = PlayerState.WaitingInput;
            else State = PlayerState.Running; // caller: break out and keep stepping
        }

        void DoTyping(VNCommand cmd)
        {
            float d = cmd.GetFloat("time", -1f);
            if (d < 0f && !string.IsNullOrEmpty(cmd.Name))
            {
                if (!float.TryParse(cmd.Name, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out d))
                    d = -1f;
            }
            if (d < 0f) d = 1.2f;
            if (engine.Phone != null && engine.Phone.IsOpen) engine.Phone.ShowTyping();
            State = PlayerState.WaitingTimer;
            finishWait = false;
            runner.StartCoroutine(TypingRoutine(d));
        }

        IEnumerator TypingRoutine(float duration)
        {
            float t = 0f;
            while (t < duration && !finishWait)
            {
                if (SkipMode || skipHeld) finishWait = true;
                t += Time.deltaTime;
                yield return null;
            }
            finishWait = false;
            if (engine.Phone != null) engine.Phone.HideTyping();
            State = PlayerState.Running;
            Step();
        }

        void DoFade(VNCommand cmd)
        {
            float time = cmd.GetFloat("time", -1f);
            if (time < 0f && !string.IsNullOrEmpty(cmd.Name))
            {
                if (!float.TryParse(cmd.Name, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out time))
                    time = -1f;
            }
            if (time < 0f) time = 1f;
            engine.FadeScreen(cmd.Get("dir", "out") == "out", time);
        }

        bool DoChoice(VNCommand cmd)
        {
            engine.CaptureRollback(script.Name, index - 1);

            currentOptions = new List<VNChoiceOption>();
            var texts = new List<string>();
            foreach (var o in cmd.Options)
            {
                if (!string.IsNullOrEmpty(o.Condition) && !engine.Variables.Evaluate(o.Condition)) continue;
                currentOptions.Add(o);
                texts.Add(engine.Variables.Expand(o.Text));
            }
            if (currentOptions.Count == 0)
            {
                VNLog.Warn("Choice at line " + cmd.LineNumber + " has no visible options; skipped.");
                return false;
            }
            State = PlayerState.WaitingChoice;
            engine.Choice.Show(texts, NotifyChoicePicked);
            return true;
        }

        public void NotifyChoicePicked(int optionIndex)
        {
            if (currentOptions == null || optionIndex < 0 || optionIndex >= currentOptions.Count) return;
            var opt = currentOptions[optionIndex];
            currentOptions = null;
            engine.Choice.Hide();

            if (!string.IsNullOrEmpty(opt.DoAssign)) engine.Variables.Apply(opt.DoAssign);

            State = PlayerState.Running;
            if (!string.IsNullOrEmpty(opt.GotoLabel) && !DoGoto(opt.GotoLabel)) { FinishScript(); return; }
            Step();
        }

        /// <summary>
        /// @input var:playerName prompt:"Sign the form" default:"Alex" max:18
        /// or positional: @input playerName "Sign the form"
        /// Pauses the script (like a mini-game) until the player confirms the text;
        /// the value lands in the given variable — use it later as {playerName}.
        /// Without default: an empty input is stored as "" — the script can then
        /// branch on it (@if playerName=="" ...).
        /// </summary>
        void DoTextInput(VNCommand cmd)
        {
            string varName = cmd.Get("var");
            if (string.IsNullOrEmpty(varName)) varName = cmd.Name; // позиционная форма
            if (string.IsNullOrEmpty(varName))
            {
                VNLog.Warn("@input without a target variable (line " + cmd.LineNumber +
                           "); use @input var:name ... or @input name \"Prompt...\" — skipped.");
                return; // continue with the next command
            }

            State = PlayerState.WaitingTextInput;
            string prompt = cmd.Get("prompt");
            if (string.IsNullOrEmpty(prompt)) prompt = cmd.Pos; // второй позиционный токен
            if (string.IsNullOrEmpty(prompt)) prompt = VNLoc.T("input.prompt");
            prompt = engine.Variables.Expand(prompt);
            string def = cmd.Get("default"); // null → пустой ввод сохранится как ""
            int max = (int)cmd.GetFloat("max", 18f);

            engine.StartTextInput(prompt, def, max, delegate (string value)
            {
                engine.Variables.Set(varName, VNValue.FromText(value));
                State = PlayerState.Running;
                Step();
            });
        }

        void DoMinigame(VNCommand cmd)
        {
            if (!VNMinigames.Exists(cmd.Name))
            {
                VNLog.Warn("Unknown minigame '" + cmd.Name + "' (line " + cmd.LineNumber +
                           "). Register it via VNMinigames.Register.");
                return; // continue with the next command
            }

            State = PlayerState.WaitingMinigame;
            string varName = cmd.Get("var");
            engine.StartMinigame(cmd, delegate (bool success, string value)
            {
                if (!string.IsNullOrEmpty(varName))
                    engine.Variables.Apply(varName + "=" + (string.IsNullOrEmpty(value) ? (success ? "1" : "0") : value));
                State = PlayerState.Running;
                Step();
            });
        }

        /// <summary>Supports "Label" and "OtherScript.Label". Returns false when unresolvable.</summary>
        bool DoGoto(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                VNLog.Warn("Empty goto label.");
                return false;
            }

            string scriptName = null, labelName = label;
            int dot = label.IndexOf('.');
            if (dot >= 0)
            {
                scriptName = label.Substring(0, dot);
                labelName = label.Substring(dot + 1);
            }

            VNScript target = script;
            if (!string.IsNullOrEmpty(scriptName) && (script == null || scriptName != script.Name))
            {
                target = engine.GetScript(scriptName);
                if (target == null) return false;
            }

            int li = target.FindLabel(labelName);
            if (li < 0)
            {
                VNLog.Warn("Label not found: '" + labelName + "' in script '" + (target != null ? target.Name : "?") + "'.");
                return false;
            }

            script = target;
            index = li;
            return true;
        }

        IEnumerator CoBackground(VNCommand cmd)
        {
            string name = cmd.Name;
            float time = cmd.GetFloat("time", 0.8f);
            if (string.IsNullOrEmpty(name) || name == "none")
            {
                engine.Backgrounds.Clear(time);
            }
            else
            {
                Sprite spr = null;
                yield return engine.LoadBackgroundAsync(name, s => spr = s);
                engine.Backgrounds.Set(name, spr, time);
            }
            State = PlayerState.Running;
            Step();
        }

        IEnumerator CoCg(VNCommand cmd)
        {
            string name = cmd.Name;
            float fade = cmd.GetFloat("fade", cmd.GetFloat("time", 0.6f));
            if (string.IsNullOrEmpty(name) || name == "off" || name == "none")
            {
                engine.Cgs.Hide(fade);
            }
            else
            {
                var spineCfg = engine.GetSpineCg(name);
                if (spineCfg != null)
                {
                    Object skel = null;
                    yield return VNSpineActor.LoadSkeleton(spineCfg.skeletonAddress, s => skel = s);
                    engine.Cgs.Show(name, null, skel, spineCfg, fade);
                }
                else
                {
                    Sprite spr = null;
                    yield return engine.LoadCgAsync(name, s => spr = s);
                    engine.Cgs.Show(name, spr, null, null, fade);
                }
                engine.UnlockCg(name);
            }
            State = PlayerState.Running;
            Step();
        }

        IEnumerator CoBgm(VNCommand cmd)
        {
            string name = cmd.Name;
            float fade = cmd.GetFloat("fade", 1f);
            if (string.IsNullOrEmpty(name) || name == "none")
            {
                engine.Audio.StopBgm(fade);
            }
            else
            {
                AudioClip clip = null;
                yield return engine.LoadBgmAsync(name, c => clip = c);
                engine.Audio.PlayBgm(name, clip, fade);
            }
            State = PlayerState.Running;
            Step();
        }

        IEnumerator CoSfx(VNCommand cmd)
        {
            AudioClip clip = null;
            yield return engine.LoadSfxAsync(cmd.Name, c => clip = c);
            engine.Audio.PlaySfx(clip, cmd.GetFloat("vol", 1f));
            State = PlayerState.Running;
            Step();
        }

        IEnumerator CoVoice(VNCommand cmd)
        {
            AudioClip clip = null;
            yield return engine.LoadVoiceAsync(cmd.Name, c => clip = c);
            engine.Audio.PlayVoice(clip);
            State = PlayerState.Running;
            Step();
        }

        IEnumerator CoChar(VNCommand cmd)
        {
            // Parse id the same way CharacterManager does, so we know what to load.
            string id = cmd.Name ?? "";
            string name = id, appearance = null;
            int dot = id.IndexOf('.');
            if (dot >= 0)
            {
                name = id.Substring(0, dot);
                appearance = id.Substring(dot + 1);
            }

            Sprite spr = null;
            Object skel = null;
            if (cmd.GetBool("visible", true) && !string.IsNullOrEmpty(name))
            {
                var spineCfg = engine.GetSpineCharacter(name);
                if (spineCfg != null)
                    yield return VNSpineActor.LoadSkeleton(spineCfg.skeletonAddress, s => skel = s);
                else
                    yield return engine.LoadCharacterSpriteAsync(name, appearance ?? "Default", s => spr = s);
            }

            engine.Characters.ApplyCommand(cmd, spr, skel);
            State = PlayerState.Running;
            Step();
        }

        void FinishScript()
        {
            State = PlayerState.Ended;
            engine.OnScriptEnded();
        }
    }
}
