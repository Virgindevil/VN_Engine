using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VNKit
{
    /// <summary>
    /// VNKit — a lightweight, script-driven visual novel engine for Unity.
    /// Drop this component on an empty GameObject, assign a start script, press Play.
    /// The whole stage and UI are built at runtime; no scene setup required.
    /// </summary>
    [AddComponentMenu("VNKit/Visual Novel Engine")]
    public class VisualNovelEngine : MonoBehaviour
    {
        public static VisualNovelEngine Instance { get; private set; }

        [Header("Scripts")]
        public TextAsset startScript;
        public List<TextAsset> additionalScripts = new List<TextAsset>();

        [Header("Content")]
        [Tooltip("Root folder inside any Resources folder where art/audio is looked up.")]
        public string resourcesRoot = "VN";

        [Header("Presentation")]
        public bool showTitleScreen = true;
        public string gameTitle = "My Visual Novel";
        public Sprite titleBackground;
        [Tooltip("Generate colored placeholder sprites when an art asset is missing. Great for prototyping.")]
        public bool usePlaceholderGraphics = false;
        public Color dialoguePanelColor = new Color(0f, 0f, 0f, 0.72f);
        public Color accentColor = new Color(0.85f, 0.45f, 0.65f, 1f);

        [Header("Behavior")]
        [Tooltip("Track which lines the player has read; skip mode stops at unread text.")]
        public bool saveSeenText = true;

        public VNSettings Settings = new VNSettings();

        // ---- Runtime services (created in Awake) ----
        public VNVariables Variables { get; private set; }
        public ScriptPlayer Player { get; private set; }
        public CharacterManager Characters { get; private set; }
        public BackgroundManager Backgrounds { get; private set; }
        public VNAudioManager Audio { get; private set; }
        public SaveLoadManager Storage { get; private set; }
        public DialogueUI Dialogue { get; private set; }
        public ChoiceUI Choice { get; private set; }
        public BacklogUI BacklogPanel { get; private set; }
        public SaveLoadUI SaveLoadPanel { get; private set; }
        public SettingsUI SettingsPanel { get; private set; }
        public TitleUI Title { get; private set; }
        public QuickMenuUI QuickMenu { get; private set; }

        /// <summary>Handle unknown @commands here (cmd.Name = command name, cmd.Params = parameters).</summary>
        public event Action<VNCommand> CustomCommand;
        /// <summary>Fired when a script reaches its end or an @end command runs.</summary>
        public event Action ScriptEnded;

        Canvas canvas;
        readonly Dictionary<string, TextAsset> scriptAssets = new Dictionary<string, TextAsset>();
        readonly Dictionary<string, VNScript> scriptCache = new Dictionary<string, VNScript>();
        readonly List<VNBacklogEntry> backlog = new List<VNBacklogEntry>();
        readonly HashSet<string> seenLines = new HashSet<string>();
        const int BacklogCapacity = 100;
        bool hudHidden;
        bool booted;

        const string SettingsKey = "VNKit.Settings";
        const string SeenKey = "VNKit.Seen";

        // ============================== Boot ==============================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            Boot();
        }

        void Boot()
        {
            LoadSettings();
            LoadSeen();

            Variables = new VNVariables();
            Storage = new SaveLoadManager();

            EnsureEventSystem();

            canvas = UIFactory.CreateCanvas("VNKit.Canvas", transform);
            var stageRoot = UIFactory.Rect("Stage", canvas.transform);
            UIFactory.Stretch(stageRoot);
            Backgrounds = new BackgroundManager(stageRoot, this);
            Characters = new CharacterManager(stageRoot, this);

            var overlayRoot = UIFactory.Rect("Overlay", canvas.transform);
            UIFactory.Stretch(overlayRoot);
            Dialogue = new DialogueUI(overlayRoot, this);
            QuickMenu = new QuickMenuUI(overlayRoot, this);
            Choice = new ChoiceUI(overlayRoot, this);
            BacklogPanel = new BacklogUI(overlayRoot, this);
            SaveLoadPanel = new SaveLoadUI(overlayRoot, this);
            SettingsPanel = new SettingsUI(overlayRoot, this);
            Title = new TitleUI(overlayRoot, this);

            Audio = new VNAudioManager(transform, this);
            Player = new ScriptPlayer(this, VNRunner.Create("VNKit.Player", transform));

            RegisterScript(startScript);
            foreach (var a in additionalScripts) RegisterScript(a);

            booted = true;
        }

        void Start()
        {
            if (showTitleScreen)
            {
                Dialogue.Hide();
                QuickMenu.SetVisible(false);
                Title.Show();
            }
            else StartNewGame();
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#elif ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#endif
        }

        // ============================== Input ==============================

        void Update()
        {
            if (!booted || Player == null) return;

            if (VNInput.CancelPressed())
            {
                if (SettingsPanel.IsOpen) SettingsPanel.Hide();
                else if (BacklogPanel.IsOpen) BacklogPanel.Hide();
                else if (SaveLoadPanel.IsOpen) SaveLoadPanel.Hide();
                else if (!Title.IsOpen && Player.State != PlayerState.Idle && Player.State != PlayerState.Ended) OpenSettings();
                Player.Tick(Time.deltaTime);
                return;
            }

            bool modal = IsModalOpen();
            Player.SetSkipHeld(!modal && !Title.IsOpen && VNInput.SkipHeld());

            if (!modal && !Title.IsOpen)
            {
                if (VNInput.HidePressed()) ToggleHud();
                else if (!hudHidden
                         && Player.State != PlayerState.WaitingChoice
                         && Player.State != PlayerState.Idle
                         && Player.State != PlayerState.Ended
                         && VNInput.AdvancePressed()
                         && !VNInput.PointerOverUI())
                    Player.Advance();
            }

            Player.Tick(Time.deltaTime);
        }

        public bool IsModalOpen()
        {
            return BacklogPanel.IsOpen || SaveLoadPanel.IsOpen || SettingsPanel.IsOpen;
        }

        public void ToggleHud()
        {
            if (Title.IsOpen || IsModalOpen()) return;
            hudHidden = !hudHidden;
            Dialogue.SetHudVisible(!hudHidden);
            QuickMenu.SetVisible(!hudHidden);
        }

        // ============================== Game flow ==============================

        public void StartNewGame()
        {
            if (startScript == null) { VNLog.Warn("No start script assigned to the engine."); return; }
            Title.Hide();
            HideAllPanels();
            Choice.Hide();
            Variables.Clear();
            ClearBacklog();
            Player.Stop();
            Audio.StopBgm(0.25f);
            Audio.StopVoice();
            Characters.ClearAll();
            Backgrounds.Clear(0f);

            hudHidden = false;
            Dialogue.SetHudVisible(true);
            Dialogue.Hide();
            QuickMenu.SetVisible(true);

            var s = GetScript(startScript.name);
            if (s != null) Player.Play(s, 0);
        }

        public void ReturnToTitle()
        {
            Player.Stop();
            Audio.StopBgm(0.5f);
            Audio.StopVoice();
            Characters.ClearAll();
            Backgrounds.Clear(0f);
            HideAllPanels();
            Choice.Hide();
            Dialogue.Hide();
            QuickMenu.SetVisible(false);
            Title.Show();
        }

        public void OnScriptEnded()
        {
            var h = ScriptEnded;
            if (h != null) h();
            QuickMenu.SetVisible(false);
            if (showTitleScreen) ReturnToTitle();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ============================== Scripts ==============================

        public void RegisterScript(TextAsset asset)
        {
            if (asset != null) scriptAssets[asset.name] = asset;
        }

        public VNScript GetScript(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            VNScript s;
            if (scriptCache.TryGetValue(name, out s)) return s;
            TextAsset asset;
            if (!scriptAssets.TryGetValue(name, out asset))
            {
                VNLog.Warn("Script not found: '" + name + "'. Add it to the engine's script list.");
                return null;
            }
            s = VNScriptParser.Parse(name, asset.text);
            scriptCache[name] = s;
            return s;
        }

        // ============================== Resources ==============================

        public Sprite LoadBackground(string name)
        {
            var s = VNResources.LoadSprite(resourcesRoot + "/Backgrounds/" + name);
            if (s == null && usePlaceholderGraphics) s = PlaceholderArt.Background(name);
            return s;
        }

        public Sprite LoadCharacterSprite(string name, string appearance)
        {
            var s = VNResources.LoadSprite(resourcesRoot + "/Characters/" + name + "/" + appearance);
            if (s == null && usePlaceholderGraphics) s = PlaceholderArt.Character(name);
            return s;
        }

        public AudioClip LoadBgm(string name)   { return VNResources.LoadClip(resourcesRoot + "/Audio/BGM/" + name); }
        public AudioClip LoadSfx(string name)   { return VNResources.LoadClip(resourcesRoot + "/Audio/SFX/" + name); }
        public AudioClip LoadVoice(string name) { return VNResources.LoadClip(resourcesRoot + "/Audio/Voice/" + name); }

        // ============================== Save / Load ==============================

        public bool SaveGame(int slot)
        {
            if (Player.State == PlayerState.WaitingChoice) { VNLog.Warn("Cannot save during a choice."); return false; }
            if (Player.State != PlayerState.WaitingInput && Player.State != PlayerState.Running)
            {
                VNLog.Warn("Nothing to save right now.");
                return false;
            }
            if (Dialogue.IsTyping) Dialogue.CompleteLine();

            var data = new VNSaveData
            {
                scriptName = Player.CurrentScriptName,
                nextCommandIndex = Player.NextCommandIndex,
                background = Backgrounds.CurrentName,
                bgm = Audio.CurrentBgm,
                characters = Characters.GetStates(),
                variables = Variables.ToEntries(),
                backlog = new List<VNBacklogEntry>(backlog),
                preview = backlog.Count > 0 ? backlog[backlog.Count - 1].text : "",
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };
            Storage.Save(slot, data);
            Storage.CaptureThumbnail(this, SaveLoadPanel.Root, slot);
            return true;
        }

        public bool LoadGame(int slot)
        {
            var data = Storage.Load(slot);
            if (data == null) return false;

            Title.Hide();
            HideAllPanels();
            Choice.Hide();
            Player.Stop();
            Dialogue.Hide(); // stops any in-flight typewriter coroutine

            Variables.FromEntries(data.variables);
            RestoreBacklog(data.backlog);
            Backgrounds.Restore(data.background);
            if (!string.IsNullOrEmpty(data.bgm)) Audio.PlayBgm(data.bgm, LoadBgm(data.bgm), 0.5f);
            else Audio.StopBgm(0.1f);
            Characters.RestoreStates(data.characters);

            hudHidden = false;
            Dialogue.SetHudVisible(true);
            QuickMenu.SetVisible(true);

            var s = GetScript(data.scriptName);
            if (s == null) { VNLog.Warn("Saved script '" + data.scriptName + "' is missing."); return false; }
            Player.Play(s, data.nextCommandIndex);
            return true;
        }

        // ============================== Panels (used by quick menu / buttons) ==============================

        public void OpenBacklog()   { if (!IsModalOpen() && !Title.IsOpen) BacklogPanel.Show(backlog); }
        public void OpenSavePanel() { if (!IsModalOpen() && !Title.IsOpen) SaveLoadPanel.Show(SaveLoadUI.Mode.Save); }
        public void OpenLoadPanel() { if (!IsModalOpen()) SaveLoadPanel.Show(SaveLoadUI.Mode.Load); }
        public void OpenSettings()  { if (!IsModalOpen()) SettingsPanel.Show(); }

        public void HideAllPanels()
        {
            BacklogPanel.Hide();
            SaveLoadPanel.Hide();
            SettingsPanel.Hide();
        }

        public void ToggleAuto() { Player.AutoMode = !Player.AutoMode; RefreshQuickMenuToggles(); }
        public void ToggleSkip() { Player.SkipMode = !Player.SkipMode; RefreshQuickMenuToggles(); }
        public void RefreshQuickMenuToggles() { if (QuickMenu != null) QuickMenu.RefreshToggles(); }

        // ============================== Backlog / seen text ==============================

        public void AddBacklog(string speaker, string text)
        {
            backlog.Add(new VNBacklogEntry { speaker = speaker, text = text });
            if (backlog.Count > BacklogCapacity) backlog.RemoveAt(0);
        }

        public IReadOnlyList<VNBacklogEntry> Backlog { get { return backlog; } }
        void ClearBacklog() { backlog.Clear(); }

        void RestoreBacklog(List<VNBacklogEntry> entries)
        {
            backlog.Clear();
            if (entries != null) backlog.AddRange(entries);
        }

        public bool IsLineSeen(string script, int line)
        {
            return saveSeenText && seenLines.Contains(script + "#" + line);
        }

        public void MarkLineSeen(string script, int line)
        {
            if (!saveSeenText) return;
            if (seenLines.Add(script + "#" + line)) SaveSeen();
        }

        [Serializable]
        class StringList { public List<string> items = new List<string>(); }

        void LoadSeen()
        {
            seenLines.Clear();
            if (!PlayerPrefs.HasKey(SeenKey)) return;
            try
            {
                var w = JsonUtility.FromJson<StringList>(PlayerPrefs.GetString(SeenKey));
                if (w != null && w.items != null) foreach (var s in w.items) seenLines.Add(s);
            }
            catch (Exception) { /* corrupted seen data is non-fatal */ }
        }

        void SaveSeen()
        {
            var w = new StringList();
            w.items.AddRange(seenLines);
            PlayerPrefs.SetString(SeenKey, JsonUtility.ToJson(w));
        }

        // ============================== Settings ==============================

        void LoadSettings()
        {
            if (!PlayerPrefs.HasKey(SettingsKey)) return;
            try { JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(SettingsKey), Settings); }
            catch (Exception) { /* keep defaults */ }
        }

        public void ApplySettings()
        {
            if (Audio != null) Audio.ApplyVolumes();
            PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(Settings));
        }

        // ============================== Custom commands ==============================

        public void RaiseCustomCommand(VNCommand cmd)
        {
            var h = CustomCommand;
            if (h != null) h(cmd);
            else VNLog.Warn("Unknown command '@" + cmd.Name + "' (line " + cmd.LineNumber +
                            "). Subscribe to VisualNovelEngine.CustomCommand to handle it.");
        }
    }
}
