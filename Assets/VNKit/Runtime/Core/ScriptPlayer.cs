using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VNKit
{
    /// <summary>
    /// Executes a parsed VNScript command by command.
    /// Owns the play state machine, auto mode and skip mode.
    /// </summary>
    public class ScriptPlayer
    {
        readonly VisualNovelEngine engine;
        readonly VNRunner runner;

        VNScript script;
        int index;                      // next command to execute
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

        /// <summary>Player pressed advance (click / space / enter).</summary>
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
                && State != PlayerState.Idle
                && State != PlayerState.Ended;

            if (skipping)
            {
                // Toggled skip mode halts at unseen text; holding Ctrl skips everything.
                if (SkipMode && !skipHeld && !CurrentLineSeen && (IsTyping || State == PlayerState.WaitingInput))
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

        // ---------------------------------------------------------------

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

                    case VNCommandType.Char:      engine.Characters.ApplyCommand(cmd); break;
                    case VNCommandType.HideChar:  engine.Characters.Hide(cmd.Name, cmd.GetFloat("time", 0.35f)); break;
                    case VNCommandType.HideChars: engine.Characters.HideAll(cmd.GetFloat("time", 0.35f)); break;
                    case VNCommandType.Background: DoBackground(cmd); break;

                    case VNCommandType.Bgm:    DoBgm(cmd); break;
                    case VNCommandType.StopBgm: engine.Audio.StopBgm(cmd.GetFloat("fade", 1f)); break;
                    case VNCommandType.Sfx:    engine.Audio.PlaySfx(engine.LoadSfx(cmd.Name), cmd.GetFloat("vol", 1f)); break;
                    case VNCommandType.Voice:  engine.Audio.PlayVoice(engine.LoadVoice(cmd.Name)); break;
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
            if (!string.IsNullOrEmpty(cmd.Appearance) && !string.IsNullOrEmpty(cmd.Speaker))
                engine.Characters.SetAppearance(cmd.Speaker, cmd.Appearance);

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

        void DoBackground(VNCommand cmd)
        {
            string name = cmd.Name;
            if (string.IsNullOrEmpty(name) || name == "none")
                engine.Backgrounds.Clear(cmd.GetFloat("time", 0.8f));
            else
                engine.Backgrounds.Set(name, engine.LoadBackground(name), cmd.GetFloat("time", 0.8f));
        }

        void DoBgm(VNCommand cmd)
        {
            string name = cmd.Name;
            if (string.IsNullOrEmpty(name) || name == "none")
            {
                engine.Audio.StopBgm(cmd.GetFloat("fade", 1f));
                return;
            }
            engine.Audio.PlayBgm(name, engine.LoadBgm(name), cmd.GetFloat("fade", 1f));
        }

        void FinishScript()
        {
            State = PlayerState.Ended;
            engine.OnScriptEnded();
        }
    }
}
