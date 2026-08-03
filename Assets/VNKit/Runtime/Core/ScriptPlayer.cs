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

        public PlayerState State { get; private set; }
        public bool AutoMode;
        public bool SkipMode;
        public bool CurrentLineSeen { get; private set; }
        public string CurrentScriptName { get { return script != null ? script.Name : null; } }
        public int NextCommandIndex { get { return index; } }
        public bool IsTyping { get { return engine.Dialogue != null && engine.Dialogue.IsTyping; } }

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
            engine.Audio.StopVoice();
            CurrentLineSeen = engine.IsLineSeen(script.Name, cmd.LineNumber);
            engine.MarkLineSeen(script.Name, cmd.LineNumber);
            engine.AddBacklog(cmd.Speaker, cmd.Text);
            engine.Dialogue.PlayLine(cmd.Speaker, cmd.Text, OnLineFinished);
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

        bool DoChoice(VNCommand cmd)
        {
            engine.CaptureRollback(script.Name, index - 1);

            currentOptions = new List<VNChoiceOption>();
            var texts = new List<string>();
            foreach (var o in cmd.Options)
            {
                if (!string.IsNullOrEmpty(o.Condition) && !engine.Variables.Evaluate(o.Condition)) continue;
                currentOptions.Add(o);
                texts.Add(o.Text);
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
