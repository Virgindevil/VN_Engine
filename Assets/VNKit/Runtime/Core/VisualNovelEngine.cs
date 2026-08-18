using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VNKit
{
    /// <summary>
    /// VNKit — a lightweight, script-driven visual novel engine for Unity.
    /// Drop this component on an empty GameObject, assign a start script, press Play.
    /// The whole scene and UI are built at runtime; no scene setup required.
    /// Assets (backgrounds, characters, CGs, audio) load asynchronously via Addressables.
    /// </summary>
    [AddComponentMenu("VNKit/Visual Novel Engine")]
    public class VisualNovelEngine : MonoBehaviour
    {
        public static VisualNovelEngine Instance { get; private set; }

        [Header("Scripts")]
        public TextAsset startScript;
        [Tooltip("Extra scripts, including localized variants named 'ScriptName.lang' (e.g. Demo.ru).")]
        public List<TextAsset> additionalScripts = new List<TextAsset>();

        [Header("Content (Addressables)")]
        [Tooltip("Addressables address prefix. Assets must be marked Addressable as e.g. VN/Backgrounds/Campus")]
        public string resourcesRoot = "VN";

        [Header("Presentation")]
        public bool showTitleScreen = true;
        public string gameTitle = "My Visual Novel";
        public Sprite titleBackground;
        public Sprite titleLogo;
        [Tooltip("Optional UI theme: fonts, colors and main-menu layout.")]
        public VNUITheme uiTheme;
        [Tooltip("Generate colored placeholder sprites when an art asset is missing. Great for prototyping.")]
        public bool usePlaceholderGraphics = false;
        public Color dialoguePanelColor = new Color(0f, 0f, 0f, 0.72f);
        public Color accentColor = new Color(0.85f, 0.45f, 0.65f, 1f);
        [Tooltip("Build a monochrome emoji fallback from the OS emoji font. Turn OFF when you use a color TMP Sprite Asset for emoji (font fallbacks are searched before sprite assets).")]
        public bool useOsEmojiFont = true;

        [Header("Phone UI")]
        [Tooltip("Start the game with the phone as the in-game menu (RMB / Esc → смартфон). OFF = classic box menu. The script can switch anytime: @phoneOn / @phoneOff.")]
        public bool usePhoneMenu = false;
        [Tooltip("Custom phone body sprite (e.g. a hand holding a phone). Leave empty for the procedural flat design.")]
        public Sprite phoneSkin;
        [Tooltip("Phone body sprite for a male protagonist (hand with phone). Overrides Phone Skin when the sex variable says 'male'. Leave empty to always use Phone Skin.")]
        public Sprite phoneSkinMale;
        [Tooltip("Phone body sprite for a female protagonist (hand with phone). Overrides Phone Skin when the sex variable says 'female'. Leave empty to always use Phone Skin.")]
        public Sprite phoneSkinFemale;
        [Tooltip("Engine variable holding the protagonist's sex ('male'/'female'). The phone skin is picked from it.")]
        public string playerSexVariable = "playerSex";
        [Tooltip("Phone frame size in reference pixels; with a skin sprite the height follows the sprite aspect (width is kept).")]
        public Vector2 phoneSize = new Vector2(470f, 900f);
        [Tooltip("Normalized (0..1) chat-screen area inside the phone skin sprite — the messenger UI is drawn there.")]
        public Rect phoneScreenRect = new Rect(0.06f, 0.09f, 0.88f, 0.82f);
        [Tooltip("Priority tracking per chat: a chat that received a message awaits the player's reply; replying in chat X grants X answerPoints and every other awaiting chat gets ignorePoints. Points land in the given engine variable.")]
        public List<VNChatPriority> chatPriorities = new List<VNChatPriority>();

        [Header("Memories (gallery scene replay)")]
        [Tooltip("Unlocked CGs listed here get a replay button in the main-menu gallery: clicking jumps to the given script label.")]
        public List<VNMemoryEntry> memories = new List<VNMemoryEntry>();

        [Header("Loading")]
        [Tooltip("Show a full-screen loading overlay while Addressables initializes at boot.")]
        public bool showLoadingScreen = true;
        [Tooltip("Optional Addressables keys to preload during the boot loading screen.")]
        public List<string> preloadAddresses = new List<string>();

        [Header("Spine (optional)")]
        [Tooltip("Characters rendered as Spine skeletons; 'appearance' maps to an animation name.")]
        public List<VNSpineCharEntry> spineCharacters = new List<VNSpineCharEntry>();
        [Tooltip("Event CGs rendered as animated Spine skeletons.")]
        public List<VNSpineCgEntry> spineCgs = new List<VNSpineCgEntry>();

        [Header("CG Gallery")]
        [Tooltip("Every CG name the gallery can show. CGs unlock when seen in-game via @cg.")]
        public List<string> galleryCgs = new List<string>();

        [Header("Behavior")]
        [Tooltip("Track which lines the player has read; skip mode stops at unread text.")]
        public bool saveSeenText = true;
        [Tooltip("Mouse wheel up / rollback hotkey rewinds one line (Ren'Py / Naninovel style).")]
        public bool enableRollback = true;
        [Tooltip("Developer tools: F8 toggles the debug panel (variables, phone data). Keep OFF for release builds — the panel is never shown otherwise.")]
        public bool enableDebugTools = false;

        public VNSettings Settings = new VNSettings();

        // ---- Runtime services (created in Awake) ----
        public VNVariables Variables { get; private set; }
        public ScriptPlayer Player { get; private set; }
        public CharacterManager Characters { get; private set; }
        public BackgroundManager Backgrounds { get; private set; }
        public CgManager Cgs { get; private set; }
        public VNAudioManager Audio { get; private set; }
        public SaveLoadManager Storage { get; private set; }
        public DialogueUI Dialogue { get; private set; }
        public ChoiceUI Choice { get; private set; }
        public BacklogUI BacklogPanel { get; private set; }
        public SaveLoadUI SaveLoadPanel { get; private set; }
        public SettingsUI SettingsPanel { get; private set; }
        public CGGalleryUI GalleryPanel { get; private set; }
        public InputUI TextInputPanel { get; private set; }
        public PhoneUI Phone { get; private set; }
        public PhotoViewerUI PhotoViewer { get; private set; }
        public TitleUI Title { get; private set; }
        public QuickMenuUI QuickMenu { get; private set; }
        public LoadingUI Loading { get; private set; }
        public PauseMenuUI PauseMenu { get; private set; }
        /// <summary>2.12: developer debug panel (F8); null unless enableDebugTools.</summary>
        public DebugPanelUI DebugPanel { get; private set; }
        /// <summary>Current in-game menu style: true = phone menu, false = classic
        /// box menu. Starts from usePhoneMenu; @phoneOn/@phoneOff switch it at runtime.</summary>
        public bool PhoneMenuActive { get { return phoneMenuActive; } }
        bool phoneMenuActive;

        /// <summary>Handle unknown @commands here (cmd.Name = command name, cmd.Params = params).</summary>
        public event Action<VNCommand> CustomCommand;
        /// <summary>Fires when the script ends or @end executes.</summary>
        public event Action ScriptEnded;

        Canvas canvas;
        Transform overlayRoot;
        readonly Dictionary<string, TextAsset> scriptAssets = new Dictionary<string, TextAsset>();
        readonly Dictionary<string, VNScript> scriptCache = new Dictionary<string, VNScript>();
        readonly List<VNBacklogEntry> backlog = new List<VNBacklogEntry>();
        readonly HashSet<string> seenLines = new HashSet<string>();
        readonly HashSet<string> unlockedCgs = new HashSet<string>();
        readonly VNRollback rollback = new VNRollback();
        UnityEngine.UI.Image fadeImage;
        Coroutine fadeRoutine;
        VNMinigame activeMinigame;
        bool hudHidden;
        bool booted;
        bool suppressRollbackCapture;
        bool rollingBack;   // a RollbackRoutine is in flight — block further rollback input

        const int BacklogCapacity = 100;
        const string SettingsKey = "VNKit.Settings";
        const string SeenKey = "VNKit.Seen";
        const string CgsKey = "VNKit.CGs";

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
            ApplyVideoSettings();
            LoadSeen();
            LoadCgs();

            VNLoc.Language = Settings.language;
            UIFactory.Theme = uiTheme;
            UIFactory.UseOsEmojiFont = useOsEmojiFont;

            Variables = new VNVariables();
            Storage = new SaveLoadManager();

            EnsureEventSystem();

            canvas = UIFactory.CreateCanvas("VNKit.Canvas", transform);
            var stageRoot = UIFactory.Rect("Stage", canvas.transform);
            UIFactory.Stretch(stageRoot);
            Backgrounds = new BackgroundManager(stageRoot, this);
            Characters = new CharacterManager(stageRoot, this);
            Cgs = new CgManager(stageRoot, this); // above characters, under the UI overlay

            // Full-screen fade layer (@fadeOut/@fadeIn): above the scene, under the UI overlay,
            // so narration text stays readable on the black screen.
            var fadeRT = UIFactory.Rect("FadeLayer", canvas.transform);
            UIFactory.Stretch(fadeRT);
            fadeImage = UIFactory.AddImage(fadeRT.gameObject, new Color(0f, 0f, 0f, 0f));
            fadeImage.raycastTarget = false;

            var overlayRT = UIFactory.Rect("Overlay", canvas.transform);
            UIFactory.Stretch(overlayRT);
            overlayRoot = overlayRT;
            Dialogue = new DialogueUI(overlayRT, this);
            QuickMenu = new QuickMenuUI(overlayRT, this);
            Choice = new ChoiceUI(overlayRT, this);
            BacklogPanel = new BacklogUI(overlayRT, this);
            SaveLoadPanel = new SaveLoadUI(overlayRT, this);
            SettingsPanel = new SettingsUI(overlayRT, this);
            GalleryPanel = new CGGalleryUI(overlayRT, this);
            TextInputPanel = new InputUI(overlayRT, this);
            Phone = new PhoneUI(overlayRT, this);
            Phone.Opened += OnPhoneOpened;
            Phone.Closed += OnPhoneClosed;
            Phone.MenuClosed += OnPhoneMenuClosed;
            PhotoViewer = new PhotoViewerUI(overlayRT);
            PauseMenu = new PauseMenuUI(overlayRT, this);
            Title = new TitleUI(overlayRT, this);
            Loading = new LoadingUI(overlayRT, gameTitle);
            phoneMenuActive = usePhoneMenu;
            // 2.12: developer debug panel (F8) — created only when enabled,
            // never part of the release UI.
            if (enableDebugTools) DebugPanel = new DebugPanelUI(overlayRT, this);

            Audio = new VNAudioManager(transform, this);
            Player = new ScriptPlayer(this, VNRunner.Create("VNKit.Player", transform));

            RegisterScript(startScript);
            foreach (var a in additionalScripts) RegisterScript(a);

            booted = true;
        }

        void Start()
        {
            // Keep everything hidden until Addressables is ready.
            Dialogue.Hide();
            QuickMenu.SetVisible(false);
            Title.Hide();
            if (showLoadingScreen)
                Loading.Show(VNLoc.T("loading.init"));
            StartCoroutine(BootRoutine());
        }

        IEnumerator BootRoutine()
        {
            // 1) Initialize Addressables (catalogs, providers). Required for remote groups / WebGL.
            if (showLoadingScreen) Loading.SetProgress(0.05f, VNLoc.T("loading.init"));
            yield return VNResources.Initialize();

            // 2) Optional preload of addresses listed on the engine component.
            if (preloadAddresses != null && preloadAddresses.Count > 0)
            {
                if (showLoadingScreen) Loading.SetProgress(0.15f, VNLoc.T("loading.assets"));

                // Route by addressing convention: audio keys go through the clip loader,
                // everything else through the sprite loader (avoids InvalidKeyException
                // when audio addresses are listed for preloading).
                var spriteKeys = new List<string>();
                var clipKeys = new List<string>();
                for (int i = 0; i < preloadAddresses.Count; i++)
                {
                    string a = preloadAddresses[i];
                    if (string.IsNullOrEmpty(a)) continue;
                    if (a.Contains("/Audio/")) clipKeys.Add(a);
                    else spriteKeys.Add(a);
                }

                int total = spriteKeys.Count + clipKeys.Count;
                if (total > 0)
                {
                    float spriteShare = spriteKeys.Count / (float)total;
                    yield return VNResources.PreloadSprites(spriteKeys, (p, addr) =>
                    {
                        if (showLoadingScreen)
                            Loading.SetProgress(0.15f + 0.8f * spriteShare * p,
                                string.IsNullOrEmpty(addr) ? VNLoc.T("loading.ready") : addr);
                    });
                    yield return VNResources.PreloadClips(clipKeys, (p, addr) =>
                    {
                        if (showLoadingScreen)
                            Loading.SetProgress(0.15f + 0.8f * (spriteShare + (1f - spriteShare) * p),
                                string.IsNullOrEmpty(addr) ? VNLoc.T("loading.ready") : addr);
                    });
                }
            }
            else if (showLoadingScreen)
            {
                Loading.SetProgress(0.6f, VNLoc.T("loading.ready"));
                yield return null;
            }

            if (showLoadingScreen)
            {
                Loading.SetProgress(1f, VNLoc.T("loading.done"));
                yield return null;
                Loading.Hide();
            }

            // 3) Title screen or straight into the game.
            if (showTitleScreen)
            {
                Dialogue.Hide();
                QuickMenu.SetVisible(false);
                Title.Show();
            }
            else StartNewGame();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            VNResources.ReleaseAll();
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

            // A mini-game owns the screen (and Esc) while active.
            if (activeMinigame != null)
            {
                activeMinigame.Tick(Time.deltaTime);
                return;
            }

            if (Phone != null) Phone.Tick(); // 2.12: action bar / «Далее» upkeep

            // 2.12: developer debug panel (never in release UI — enableDebugTools only).
            if (DebugPanel != null && VNInput.KeyPressed(KeyCode.F8))
            {
                if (DebugPanel.IsOpen) DebugPanel.Hide(); else DebugPanel.Show();
                return;
            }
            if (DebugPanel != null && DebugPanel.IsOpen) { Player.SetSkipHeld(false); return; }

            // While a text input (@input) is open, every hotkey belongs to the
            // input field — typing 'a' must not toggle auto-mode etc.
            if (Player.State == PlayerState.WaitingTextInput)
            {
                Player.SetSkipHeld(false);
                return;
            }

            if (VNInput.CancelPressed())
            {
                if (SettingsPanel.IsOpen) SettingsPanel.Hide();
                else if (GalleryPanel.IsOpen) GalleryPanel.Hide();
                else if (BacklogPanel.IsOpen) BacklogPanel.Hide();
                else if (SaveLoadPanel.IsOpen) SaveLoadPanel.Hide();
                else if (PhotoViewer != null && PhotoViewer.IsOpen) PhotoViewer.Hide();
                // 2.12.2: a locked live dialogue keeps the phone on screen —
                // Esc then opens the pause menu instead (escape hatch via title).
                else if (Phone != null && Phone.IsMenuOpen)
                {
                    if (!Phone.DialogueLock) Phone.CloseMenu();
                    else if (PauseMenu != null) PauseMenu.Show();
                }
                else if (PauseMenu != null && PauseMenu.IsOpen) PauseMenu.Hide();
                else if (!Title.IsOpen && Player.State != PlayerState.Idle && Player.State != PlayerState.Ended)
                    ToggleInGameMenu();
                return;
            }

            // RMB toggles the in-game menu: the phone (when phoneMenuActive or a
            // chat-mode conversation is running) or the classic box menu.
            // Works around the modal freeze below, so it also closes the menu.
            if (VNInput.HidePressed() && !Title.IsOpen)
            {
                ToggleInGameMenu();
                return;
            }

            bool modal = IsModalOpen();
            // 2.8 chat mode: the phone menu IS the dialogue UI. While it is open the
            // game keeps running — clicks on the phone body advance, Ctrl skips,
            // and every scripted conversation plays inside the Chats tab.
            bool chatUi = Phone != null && Phone.ChatMode && Phone.IsMenuOpen;
            // While any modal (Settings / Save / Load / Backlog / Gallery) or the title is open,
            // freeze advance / auto / skip so text does not progress behind the panel.
            // Blocking panels freeze even in chat mode (they sit above the phone).
            if (IsBlockingModalOpen() || (modal && !chatUi) || Title.IsOpen)
            {
                Player.SetSkipHeld(false);
                return;
            }

            Player.SetSkipHeld(VNInput.SkipHeld(Settings.skipKey));

            // Configurable auto-mode hotkey
            if (VNInput.KeyPressed(Settings.autoKey)
                && Player.State != PlayerState.Idle && Player.State != PlayerState.Ended)
                ToggleAuto();

            // Rollback: mouse wheel up or the rollback hotkey, while waiting on a finished line.
            if (enableRollback && !hudHidden && !rollingBack && !IsModalOpen()
                && Player.State == PlayerState.WaitingInput && !Player.IsTyping
                && (VNInput.ScrollDelta() > 0.1f || VNInput.KeyPressed(Settings.rollbackKey)))
            {
                DoRollback();
                return;
            }

            if (VNInput.KeyPressed(Settings.hideKey)) ToggleHud();
            else if (!hudHidden
                     && (!IsModalOpen() || chatUi)
                     && Player.State != PlayerState.WaitingChoice
                     && Player.State != PlayerState.Idle
                     && Player.State != PlayerState.Ended
                     && VNInput.AdvancePressed()
                     && !VNInput.PointerOverUI(chatUi || (Phone != null && !Phone.IsMenuOpen) ? Phone.Root : null))
                Player.Advance();

            Player.Tick(Time.deltaTime);
        }

        public bool IsModalOpen()
        {
            return BacklogPanel.IsOpen || SaveLoadPanel.IsOpen || SettingsPanel.IsOpen || GalleryPanel.IsOpen
                || (PhotoViewer != null && PhotoViewer.IsOpen)
                || (Phone != null && Phone.IsMenuOpen)
                || (PauseMenu != null && PauseMenu.IsOpen);
        }

        /// <summary>Open/close the in-game menu: the phone (when phoneMenuActive or a
        /// chat-mode conversation is running) or the classic box menu.</summary>
        void ToggleInGameMenu()
        {
            // 2.12.2: a locked live dialogue keeps the phone on screen.
            if (Phone != null && Phone.IsMenuOpen) { if (!Phone.DialogueLock) Phone.CloseMenu(); return; }
            if (PauseMenu != null && PauseMenu.IsOpen) { PauseMenu.Hide(); return; }
            if (IsModalOpen()) return;
            if (Player.State == PlayerState.Idle || Player.State == PlayerState.Ended) return;
            if (Phone != null && (phoneMenuActive || Phone.ChatMode)) Phone.OpenMenu();
            else if (PauseMenu != null) PauseMenu.Show();
        }

        /// <summary>@phoneOn / @phoneOff — switch the in-game menu style at runtime.
        /// The messenger itself (@online/@msg/@chat) works in both modes.</summary>
        public void SetPhoneMenu(bool on)
        {
            phoneMenuActive = on;
            if (on)
            {
                if (PauseMenu != null) PauseMenu.Hide();
            }
            else
            {
                // Chat mode keeps the phone (it is the dialogue UI); a plain
                // player-opened phone menu is closed when the style switches.
                if (Phone != null && Phone.IsMenuOpen && !Phone.ChatMode) Phone.CloseMenu();
            }
        }

        /// <summary>Pick the phone skin for the current protagonist: the male/female
        /// variant by the playerSexVariable value, falling back to phoneSkin.</summary>
        public Sprite GetPhoneSkin()
        {
            string sex = Variables != null && !string.IsNullOrEmpty(playerSexVariable)
                ? Variables.GetString(playerSexVariable) : null;
            if (!string.IsNullOrEmpty(sex))
            {
                if (IsFemaleSexValue(sex) && phoneSkinFemale != null) return phoneSkinFemale;
                if (IsMaleSexValue(sex) && phoneSkinMale != null) return phoneSkinMale;
            }
            return phoneSkin;
        }

        /// <summary>Recognized female values of the sex variable (case-insensitive).</summary>
        public static bool IsFemaleSexValue(string v)
        {
            if (v == null) return false;
            switch (v.Trim().ToLowerInvariant())
            {
                case "female": case "f": case "woman": case "girl":
                case "женщина": case "женский": case "ж": case "девушка":
                    return true;
            }
            return false;
        }

        /// <summary>Recognized male values of the sex variable (case-insensitive).</summary>
        public static bool IsMaleSexValue(string v)
        {
            if (v == null) return false;
            switch (v.Trim().ToLowerInvariant())
            {
                case "male": case "m": case "man": case "boy":
                case "мужчина": case "мужской": case "м": case "парень":
                    return true;
            }
            return false;
        }

        public bool HudHidden { get { return hudHidden; } }

        public void ToggleHud()
        {
            if (Title.IsOpen || IsModalOpen()) return;
            hudHidden = !hudHidden;
            dialogueHiddenByPhone = false;
            Dialogue.SetHudVisible(!hudHidden);
        }

        // ============================== Game flow ==============================

        /// <summary>Screen fade (@fadeOut/@fadeIn). Non-blocking: the script keeps running.</summary>
        public void FadeScreen(bool toBlack, float time)
        {
            if (fadeImage == null) return;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeRoutine(toBlack ? 1f : 0f, Mathf.Max(0.01f, time)));
        }

        IEnumerator FadeRoutine(float target, float duration)
        {
            float from = fadeImage.color.a;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(from, target, Mathf.Clamp01(t / duration));
                fadeImage.color = new Color(0f, 0f, 0f, a);
                yield return null;
            }
            fadeImage.color = new Color(0f, 0f, 0f, target);
            fadeRoutine = null;
        }

        // ============================== Phone overlay ==============================

        bool dialogueHiddenByPhone;

        /// <summary>Open the full-screen photo viewer (phone chat attachments).</summary>
        public void ShowPhotoViewer(Sprite s) { if (PhotoViewer != null) PhotoViewer.Show(s); }

        /// <summary>Memory entry bound to a CG, if any (gallery scene replay).</summary>
        public VNMemoryEntry GetMemory(string cgName)
        {
            if (memories == null) return null;
            foreach (var m in memories)
                if (m != null && m.cg == cgName && !string.IsNullOrEmpty(m.script)) return m;
            return null;
        }

        /// <summary>Replay a scene from the gallery: jump straight to script.label.</summary>
        public void StartMemory(string scriptName, string label)
        {
            var s = GetScript(scriptName);
            int idx;
            if (s == null || !s.Labels.TryGetValue(label ?? "", out idx))
            {
                VNLog.Warn("Memory target not found: " + scriptName + "." + label);
                return;
            }
            Title.Hide();
            HideAllPanels();
            Choice.Hide();
            rollingBack = false;
            rollback.Clear();
            if (Phone != null) Phone.CloseMenu();
            Player.Stop();
            hudHidden = false;
            Dialogue.SetHudVisible(true);
            Dialogue.Hide();
            QuickMenu.SetVisible(false); // quick menu row is retired: menus live in the phone / pause menu
            suppressRollbackCapture = true;
            Player.Play(s, idx);
        }

        /// <summary>Phone opened: the dialogue panel hides behind the messenger —
        /// the conversation lives inside the phone now. (A narration line will
        /// re-show the panel by itself; the panel returns on phone close.)</summary>
        void OnPhoneOpened()
        {
            if (!hudHidden && Dialogue != null && Dialogue.IsOpen)
            {
                Dialogue.SetHudVisible(false);
                dialogueHiddenByPhone = true;
            }
        }

        /// <summary>Phone closed (@offline): bring the dialogue panel back.</summary>
        void OnPhoneClosed()
        {
            if (!dialogueHiddenByPhone) return;
            dialogueHiddenByPhone = false;
            if (!hudHidden && Dialogue != null) Dialogue.SetHudVisible(true);
        }

        /// <summary>Player put the phone away (menu closed). If the script is parked
        /// at a @waitchat hub with unfinished live dialogues, show the reminder
        /// phrase defined by the script (@waitchat ... remind:"...").</summary>
        void OnPhoneMenuClosed()
        {
            if (Player == null || Phone == null || Dialogue == null) return;
            if (Player.State != PlayerState.WaitingChat) return;
            string r = Player.ChatReminder;
            if (string.IsNullOrEmpty(r)) return;
            bool anyPending = false;
            foreach (var d in Phone.GetDialogues())
                if (!d.done) { anyPending = true; break; }
            if (!anyPending) return;
            if (!hudHidden)
            {
                Dialogue.PlayLine(null, r, null);
                Dialogue.CompleteLine(); // instant — this is a hint, not a script line
            }
        }

        public void StartNewGame()
        {
            if (startScript == null) { VNLog.Warn("No start script assigned to the engine."); return; }
            Title.Hide();
            HideAllPanels();
            Choice.Hide();
            Variables.Clear();
            ClearBacklog();
            rollback.Clear();
            rollingBack = false;
            Player.Stop();
            Audio.StopBgm(0.25f);
            Audio.StopVoice();
            Characters.ClearAll();
            Backgrounds.Clear(0f);
            Cgs.Hide(0f);
            if (Phone != null) Phone.ResetAll();
            if (fadeImage != null) fadeImage.color = new Color(0f, 0f, 0f, 0f);

            phoneMenuActive = usePhoneMenu; // menu style resets to the initial one
            hudHidden = false;
            Dialogue.SetHudVisible(true);
            Dialogue.Hide();
            QuickMenu.SetVisible(false); // quick menu row is retired: menus live in the phone / pause menu

            var s = GetScript(startScript.name);
            if (s != null) Player.Play(s, 0);
        }

        public void ReturnToTitle()
        {
            rollingBack = false;
            Player.Stop();
            Audio.StopBgm(0.5f);
            Audio.StopVoice();
            Characters.ClearAll();
            Backgrounds.Clear(0f);
            Cgs.Hide(0f);
            if (Phone != null) Phone.ResetAll();
            if (fadeImage != null) fadeImage.color = new Color(0f, 0f, 0f, 0f);
            HideAllPanels();
            Choice.Hide();
            Dialogue.Hide();
            QuickMenu.SetVisible(false);
            rollback.Clear();
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

        /// <summary>
        /// Returns a script by name. When a localized variant "Name.&lt;language&gt;" is registered
        /// (e.g. "Demo.ru" next to "Demo"), the variant matching Settings.language wins.
        /// </summary>
        public VNScript GetScript(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            string resolved = name;
            if (!string.IsNullOrEmpty(Settings.language)
                && scriptAssets.ContainsKey(name + "." + Settings.language))
                resolved = name + "." + Settings.language;

            VNScript s;
            if (scriptCache.TryGetValue(resolved, out s)) return s;
            TextAsset asset;
            if (!scriptAssets.TryGetValue(resolved, out asset))
            {
                VNLog.Warn("Script not found: '" + name + "'. Add it to the engine's script list.");
                return null;
            }
            s = VNScriptParser.Parse(resolved, asset.text);
            scriptCache[resolved] = s;
            return s;
        }

        // ============================== Assets (Addressables) ==============================

        public string BackgroundAddress(string name) { return resourcesRoot + "/Backgrounds/" + name; }
        public string CharacterAddress(string name, string appearance)
        {
            return resourcesRoot + "/Characters/" + name + "/" + (appearance ?? "Default");
        }
        public string CgAddress(string name)     { return resourcesRoot + "/CG/" + name; }
        public string BgmAddress(string name)    { return resourcesRoot + "/Audio/BGM/" + name; }
        public string SfxAddress(string name)    { return resourcesRoot + "/Audio/SFX/" + name; }
        public string VoiceAddress(string name)  { return resourcesRoot + "/Audio/Voice/" + name; }

        /// <summary>Async background load. Placeholder when missing and the flag is on.</summary>
        public IEnumerator LoadBackgroundAsync(string name, Action<Sprite> onDone)
        {
            Sprite s = null;
            yield return VNResources.LoadSprite(BackgroundAddress(name), x => s = x);
            if (s == null && usePlaceholderGraphics) s = PlaceholderArt.Background(name);
            if (onDone != null) onDone(s);
        }

        /// <summary>Async character sprite load.</summary>
        public IEnumerator LoadCharacterSpriteAsync(string name, string appearance, Action<Sprite> onDone)
        {
            Sprite s = null;
            yield return VNResources.LoadSprite(CharacterAddress(name, appearance), x => s = x);
            if (s == null && usePlaceholderGraphics) s = PlaceholderArt.Character(name);
            if (onDone != null) onDone(s);
        }

        /// <summary>Async event CG load.</summary>
        public IEnumerator LoadCgAsync(string name, Action<Sprite> onDone)
        {
            Sprite s = null;
            yield return VNResources.LoadSprite(CgAddress(name), x => s = x);
            // Not found under VN/CG/? Treat the name as a FULL Addressables address
            // (e.g. "VN/Characters/Rin/rin/selfie1") — handy for chat photos that live
            // outside the CG folder.
            if (s == null && !string.IsNullOrEmpty(name) && name.IndexOf('/') >= 0)
                yield return VNResources.LoadSprite(name, x => s = x);
            if (s == null && usePlaceholderGraphics) s = PlaceholderArt.Background(name);
            if (onDone != null) onDone(s);
        }

        public IEnumerator LoadBgmAsync(string name, Action<AudioClip> onDone)
        {
            yield return VNResources.LoadClip(BgmAddress(name), onDone);
        }

        public IEnumerator LoadSfxAsync(string name, Action<AudioClip> onDone)
        {
            yield return VNResources.LoadClip(SfxAddress(name), onDone);
        }

        public IEnumerator LoadVoiceAsync(string name, Action<AudioClip> onDone)
        {
            yield return VNResources.LoadClip(VoiceAddress(name), onDone);
        }

        // ============================== Spine config ==============================

        public VNSpineCharEntry GetSpineCharacter(string name)
        {
            if (spineCharacters == null || string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < spineCharacters.Count; i++)
                if (spineCharacters[i] != null && spineCharacters[i].character == name)
                    return spineCharacters[i];
            return null;
        }

        public VNSpineCgEntry GetSpineCg(string name)
        {
            if (spineCgs == null || string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < spineCgs.Count; i++)
                if (spineCgs[i] != null && spineCgs[i].cg == name)
                    return spineCgs[i];
            return null;
        }

        // ============================== CG gallery ==============================

        public bool IsCgUnlocked(string name)
        {
            return !string.IsNullOrEmpty(name) && unlockedCgs.Contains(name);
        }

        public void UnlockCg(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (unlockedCgs.Add(name)) SaveCgs();
        }

        public void OpenGallery()
        {
            if (!IsBlockingModalOpen()) GalleryPanel.Show();
        }

        /// <summary>Gallery click: load the CG and show it full-screen.</summary>
        public void OpenCgViewer(string name)
        {
            if (!IsCgUnlocked(name)) return;
            StartCoroutine(OpenCgViewerRoutine(name));
        }

        IEnumerator OpenCgViewerRoutine(string name)
        {
            Sprite s = null;
            yield return LoadCgAsync(name, x => s = x);
            GalleryPanel.ShowViewer(s);
        }

        [Serializable]
        class StringList { public List<string> items = new List<string>(); }

        void LoadCgs()
        {
            unlockedCgs.Clear();
            if (!PlayerPrefs.HasKey(CgsKey)) return;
            try
            {
                var w = JsonUtility.FromJson<StringList>(PlayerPrefs.GetString(CgsKey));
                if (w != null && w.items != null) foreach (var s in w.items) unlockedCgs.Add(s);
            }
            catch (Exception) { /* corrupted data is not fatal */ }
        }

        void SaveCgs()
        {
            var w = new StringList();
            w.items.AddRange(unlockedCgs);
            PlayerPrefs.SetString(CgsKey, JsonUtility.ToJson(w));
        }

        // ============================== Text input (@input) ==============================

        /// <summary>Shows the text-input panel; the script waits for confirmation.</summary>
        public void StartTextInput(string prompt, string defaultValue, int maxLength, Action<string> onDone)
        {
            Player.SetSkipHeld(false);
            TextInputPanel.Show(prompt, defaultValue, maxLength, onDone);
        }

        // ============================== Minigames ==============================

        /// <summary>Runs a registered mini-game as a full-screen overlay; the script waits for it.</summary>
        public void StartMinigame(VNCommand cmd, Action<bool, string> onComplete)
        {
            var game = VNMinigames.Create(cmd.Name);
            if (game == null) { if (onComplete != null) onComplete(false, null); return; }

            Player.SetSkipHeld(false);
            var ctx = new VNMinigameContext
            {
                parent = overlayRoot,
                engine = this,
                command = cmd,
                onComplete = delegate (bool success, string value)
                {
                    if (activeMinigame != null)
                    {
                        activeMinigame.Destroy();
                        activeMinigame = null;
                    }
                    if (onComplete != null) onComplete(success, value);
                }
            };
            activeMinigame = game;
            game.Start(ctx);
        }

        /// <summary>True while a mini-game overlay owns the screen.</summary>
        public bool MinigameActive { get { return activeMinigame != null; } }

        /// <summary>2.12: launch a registered mini-game from the phone Games tab.
        /// No script command involved — the result only updates the
        /// phoneGame.&lt;id&gt;.plays / .last variables.</summary>
        public void StartPhoneGame(string id)
        {
            if (activeMinigame != null || !VNMinigames.Exists(id)) return;
            var cmd = new VNCommand { Type = VNCommandType.PhoneGame, Name = id };
            StartMinigame(cmd, delegate (bool success, string value) { RecordPhoneGame(id, success, value); });
        }

        /// <summary>2.12: phoneGame.&lt;id&gt;.plays += 1; phoneGame.&lt;id&gt;.last = result
        /// (numeric when the game reports a number, text otherwise, 1/0 without a value).</summary>
        public void RecordPhoneGame(string id, bool success, string value)
        {
            if (string.IsNullOrEmpty(id) || Variables == null) return;
            Variables.Set("phoneGame." + id + ".plays",
                VNValue.FromNumber(Variables.Get("phoneGame." + id + ".plays").ToNumber() + 1));
            if (string.IsNullOrEmpty(value))
                Variables.Set("phoneGame." + id + ".last", VNValue.FromNumber(success ? 1 : 0));
            else
            {
                double d;
                if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out d))
                    Variables.Set("phoneGame." + id + ".last", VNValue.FromNumber(d));
                else
                    Variables.Set("phoneGame." + id + ".last", VNValue.FromText(value));
            }
        }

        // ============================== Rollback ==============================

        /// <summary>Called by the ScriptPlayer before every Say / Choice.</summary>
        public void CaptureRollback(string scriptName, int commandIndex)
        {
            if (!enableRollback) return;
            if (suppressRollbackCapture) { suppressRollbackCapture = false; return; }

            rollback.Push(new VNRollback.Snapshot
            {
                scriptName = scriptName,
                commandIndex = commandIndex,
                variables = Variables.ToEntries(),
                backlogCount = backlog.Count,
                background = Backgrounds.CurrentName,
                bgm = Audio.CurrentBgm,
                cg = Cgs.CurrentName,
                characters = Characters.GetStates(),
                phoneOpen = Phone != null && Phone.IsOpen,
                phoneChat = Phone != null ? Phone.CurrentChatId : null,
                phonePos = Phone != null ? Phone.Position : null,
                phoneChatStates = Phone != null ? Phone.GetChatStates() : null,
                phoneChatMode = Phone != null && Phone.ChatMode,
                phoneMenuActive = phoneMenuActive,
                phoneDialogues = Phone != null ? Phone.GetDialogues() : null,
                phoneDialogueLock = Phone != null && Phone.DialogueLock,
                chatHubReturn = Player != null ? Player.ChatHubReturn : -1,
                // 2.12: phone gameplay apps are mutated in place — snapshots need copies
                phoneNotes = Phone != null ? Phone.GetNotes() : null,
                phoneSchedule = Phone != null ? Phone.GetSchedule() : null,
                phoneGallery = Phone != null ? Phone.GetGalleryItems() : null,
                phoneActions = Phone != null ? Phone.GetActions() : null,
                phoneHiddenApps = Phone != null ? Phone.GetHiddenApps() : null
            });
        }

        void DoRollback()
        {
            if (rollingBack || rollback.Count == 0) return;
            var snap = rollback.Pop();
            if (snap == null) return;
            StartCoroutine(RollbackRoutine(snap));
        }

        IEnumerator RollbackRoutine(VNRollback.Snapshot snap)
        {
            rollingBack = true;
            Player.Stop();
            Choice.Hide();
            Dialogue.Hide();

            Variables.FromEntries(snap.variables);

            // Trim the backlog back to the snapshot point.
            while (backlog.Count > snap.backlogCount)
                backlog.RemoveAt(backlog.Count - 1);

            // Background — reload only when it actually changed, so rollback inside
            // a scene (e.g. during a phone chat) is instant instead of reloading
            // every asset through Addressables.
            if (!string.Equals(snap.background, Backgrounds.CurrentName, System.StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(snap.background)) Backgrounds.Restore(null, null);
                else
                {
                    Sprite bg = null;
                    yield return LoadBackgroundAsync(snap.background, s => bg = s);
                    Backgrounds.Restore(snap.background, bg);
                }
            }

            // Event CG
            if (!string.Equals(snap.cg, Cgs.CurrentName, System.StringComparison.Ordinal))
            {
            if (string.IsNullOrEmpty(snap.cg)) Cgs.Hide(0f);
            else
            {
                var spineCfg = GetSpineCg(snap.cg);
                if (spineCfg != null)
                {
                    UnityEngine.Object skel = null;
                    yield return VNSpineActor.LoadSkeleton(spineCfg.skeletonAddress, s => skel = s);
                    Cgs.Show(snap.cg, null, skel, spineCfg, 0f);
                }
                else
                {
                    Sprite cg = null;
                    yield return LoadCgAsync(snap.cg, s => cg = s);
                    Cgs.Show(snap.cg, cg, null, null, 0f);
                }
            }
            }

            // BGM — restart only when the track changed.
            if (!string.Equals(snap.bgm, Audio.CurrentBgm, System.StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(snap.bgm))
                {
                    AudioClip clip = null;
                    yield return LoadBgmAsync(snap.bgm, c => clip = c);
                    Audio.PlayBgm(snap.bgm, clip, 0.2f);
                }
                else Audio.StopBgm(0.1f);
            }

            // Characters — rebuild only when the cast actually changed.
            if (!CharStatesEqual(snap.characters, Characters.GetStates()))
            {
            var sprites = new List<Sprite>();
            var skels = new List<UnityEngine.Object>();
            if (snap.characters != null)
            {
                foreach (var st in snap.characters)
                {
                    if (!st.visible) continue;
                    var spineCfg = GetSpineCharacter(st.name);
                    if (spineCfg != null)
                    {
                        UnityEngine.Object skel = null;
                        yield return VNSpineActor.LoadSkeleton(spineCfg.skeletonAddress, s => skel = s);
                        sprites.Add(null);
                        skels.Add(skel);
                    }
                    else
                    {
                        Sprite spr = null;
                        yield return LoadCharacterSpriteAsync(st.name, st.appearance, s => spr = s);
                        sprites.Add(spr);
                        skels.Add(null);
                    }
                }
            }
            Characters.RestoreStates(snap.characters, sprites, skels);
            }

            // Phone messenger (chat history trims back to the snapshot point).
            if (Phone != null) Phone.RestoreSnapshot(snap.phoneOpen, snap.phoneChat, snap.phonePos, snap.phoneChatStates, snap.phoneChatMode);
            if (Phone != null) Phone.RestoreDialogues(snap.phoneDialogues);
            if (Phone != null) Phone.DialogueLock = snap.phoneDialogueLock; // 2.12.2
            if (Phone != null && snap.phoneNotes != null) Phone.RestoreNotes(snap.phoneNotes);
            if (Phone != null && snap.phoneSchedule != null) Phone.RestoreSchedule(snap.phoneSchedule);
            if (Phone != null && snap.phoneGallery != null) Phone.RestoreGalleryItems(snap.phoneGallery);
            if (Phone != null && snap.phoneActions != null) Phone.RestoreActions(snap.phoneActions);
            if (Phone != null && snap.phoneHiddenApps != null) Phone.RestoreHiddenApps(snap.phoneHiddenApps);
            if (Player != null) Player.ChatHubReturn = snap.chatHubReturn;
            SetPhoneMenu(snap.phoneMenuActive);

            hudHidden = false;
            Dialogue.SetHudVisible(true);

            var script = GetScript(snap.scriptName);
            if (script == null) { rollingBack = false; yield break; }

            // Re-executing the Say/Choice pushes a fresh snapshot; suppress it so
            // consecutive wheel-ups keep stepping backwards.
            suppressRollbackCapture = true;
            rollingBack = false;
            Player.Play(script, snap.commandIndex);
        }

        /// <summary>Structural comparison of character states (rollback skip-unchanged check).</summary>
        static bool CharStatesEqual(List<VNCharState> a, List<VNCharState> b)
        {
            if (a == null || a.Count == 0) return b == null || b.Count == 0;
            if (b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                var x = a[i]; var y = b[i];
                if (x == null || y == null) { if (x != y) return false; continue; }
                if (x.name != y.name || x.appearance != y.appearance || x.visible != y.visible) return false;
                if (x.visible && System.Math.Abs(x.pos - y.pos) > 0.001f) return false;
            }
            return true;
        }

        // ============================== Save / Load ==============================

        public bool SaveGame(int slot)
        {
            if (Player.State == PlayerState.WaitingChoice) { VNLog.Warn("Cannot save during a choice."); return false; }
            if (Player.State == PlayerState.WaitingMinigame) { VNLog.Warn("Cannot save during a mini-game."); return false; }
            if (Player.State == PlayerState.WaitingTextInput) { VNLog.Warn("Cannot save during text input."); return false; }
            if (Player.State != PlayerState.WaitingInput && Player.State != PlayerState.Running
                && Player.State != PlayerState.WaitingChat && Player.State != PlayerState.WaitingChatEnter
                && Player.State != PlayerState.WaitingChatHub)
            {
                VNLog.Warn("Nothing to save right now.");
                return false;
            }
            if (Dialogue.IsTyping) Dialogue.CompleteLine();

            // Parked at @waitchat/@phonehub / holding a chat line: resume by re-running
            // that command — the hub re-parks itself (or passes, if dialogues finished).
            int resumeIndex = Player.NextCommandIndex;
            if (Player.State == PlayerState.WaitingChat || Player.State == PlayerState.WaitingChatEnter
                || Player.State == PlayerState.WaitingChatHub)
                resumeIndex = Mathf.Max(0, resumeIndex - 1);

            var data = new VNSaveData
            {
                scriptName = Player.CurrentScriptName,
                nextCommandIndex = resumeIndex,
                background = Backgrounds.CurrentName,
                bgm = Audio.CurrentBgm,
                cg = Cgs.CurrentName,
                characters = Characters.GetStates(),
                variables = Variables.ToEntries(),
                backlog = new List<VNBacklogEntry>(backlog),
                phoneOpen = Phone != null && Phone.IsOpen,
                phonePos = Phone != null ? Phone.Position : null,
                phoneChat = Phone != null ? Phone.CurrentChatId : null,
                phoneChats = Phone != null ? Phone.GetChats() : new List<VNPhoneChat>(),
                phoneChatMode = Phone != null && Phone.ChatMode,
                phoneMenuActive = phoneMenuActive,
                phoneDialogues = Phone != null ? Phone.GetDialogues() : new List<VNChatDialogue>(),
                phoneDialogueLock = Phone != null && Phone.DialogueLock,
                chatHubReturn = Player != null ? Player.ChatHubReturn : -1,
                phoneNotes = Phone != null ? Phone.GetNotes() : new List<VNPhoneNote>(),
                phoneSchedule = Phone != null ? Phone.GetSchedule() : new List<VNScheduleEvent>(),
                phoneGallery = Phone != null ? Phone.GetGalleryItems() : new List<VNPhoneGalleryItem>(),
                phoneActions = Phone != null ? Phone.GetActions() : new List<VNPhoneAction>(),
                phoneHiddenApps = Phone != null ? Phone.GetHiddenApps() : new List<string>(),
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
            rollingBack = false;
            if (Phone != null) Phone.CloseMenu();
            Player.Stop();
            Dialogue.Hide(); // stops any in-flight typewriter coroutine
            QuickMenu.SetVisible(false);
            rollback.Clear();

            // Addressables load asynchronously — restore through a coroutine (with the loading screen).
            StartCoroutine(LoadGameRoutine(data));
            return true;
        }

        IEnumerator LoadGameRoutine(VNSaveData data)
        {
            if (showLoadingScreen && Loading != null)
            {
                Loading.Show(VNLoc.T("loading.save"));
                Loading.SetProgress(0.1f, VNLoc.T("loading.ready"));
            }

            Variables.FromEntries(data.variables);
            RestoreBacklog(data.backlog);

            // Background
            if (string.IsNullOrEmpty(data.background))
            {
                Backgrounds.Restore(null, null);
            }
            else
            {
                if (showLoadingScreen && Loading != null) Loading.SetProgress(0.25f, data.background);
                Sprite bg = null;
                yield return LoadBackgroundAsync(data.background, s => bg = s);
                Backgrounds.Restore(data.background, bg);
            }

            // Event CG
            if (string.IsNullOrEmpty(data.cg)) Cgs.Hide(0f);
            else
            {
                var spineCfg = GetSpineCg(data.cg);
                if (spineCfg != null)
                {
                    UnityEngine.Object skel = null;
                    yield return VNSpineActor.LoadSkeleton(spineCfg.skeletonAddress, s => skel = s);
                    Cgs.Show(data.cg, null, skel, spineCfg, 0f);
                }
                else
                {
                    Sprite cg = null;
                    yield return LoadCgAsync(data.cg, s => cg = s);
                    Cgs.Show(data.cg, cg, null, null, 0f);
                }
            }

            // BGM
            if (!string.IsNullOrEmpty(data.bgm))
            {
                if (showLoadingScreen && Loading != null) Loading.SetProgress(0.45f, data.bgm);
                AudioClip clip = null;
                yield return LoadBgmAsync(data.bgm, c => clip = c);
                Audio.PlayBgm(data.bgm, clip, 0.5f);
            }
            else Audio.StopBgm(0.1f);

            // Characters
            var states = data.characters;
            var sprites = new List<Sprite>();
            var skels = new List<UnityEngine.Object>();
            if (states != null)
            {
                int visibleCount = 0;
                for (int i = 0; i < states.Count; i++)
                    if (states[i].visible) visibleCount++;

                int loaded = 0;
                for (int i = 0; i < states.Count; i++)
                {
                    if (!states[i].visible) continue;
                    if (showLoadingScreen && Loading != null && visibleCount > 0)
                        Loading.SetProgress(0.55f + 0.35f * (loaded / (float)visibleCount), states[i].name);

                    var spineCfg = GetSpineCharacter(states[i].name);
                    if (spineCfg != null)
                    {
                        UnityEngine.Object skel = null;
                        yield return VNSpineActor.LoadSkeleton(spineCfg.skeletonAddress, s => skel = s);
                        sprites.Add(null);
                        skels.Add(skel);
                    }
                    else
                    {
                        Sprite spr = null;
                        yield return LoadCharacterSpriteAsync(states[i].name, states[i].appearance, s => spr = s);
                        sprites.Add(spr);
                        skels.Add(null);
                    }
                    loaded++;
                }
            }
            Characters.RestoreStates(states, sprites, skels);

            if (showLoadingScreen && Loading != null)
            {
                Loading.SetProgress(1f, VNLoc.T("loading.done"));
                yield return null;
                Loading.Hide();
            }

            hudHidden = false;
            Dialogue.SetHudVisible(true);
            phoneMenuActive = data.phoneMenuActive;
            QuickMenu.SetVisible(false); // quick menu row is retired: menus live in the phone / pause menu
            if (PauseMenu != null) PauseMenu.Hide();

            // Phone messenger state (open / contact / chat history / live dialogues).
            if (Phone != null)
            {
                Phone.CloseMenu();
                Phone.Restore(data.phoneOpen, data.phoneChat, data.phonePos, data.phoneChats, data.phoneChatMode);
                Phone.RestoreDialogues(data.phoneDialogues);
                // 2.12 phone apps (null in pre-2.12 saves → keep the safe defaults)
                Phone.RestoreNotes(data.phoneNotes);
                Phone.RestoreSchedule(data.phoneSchedule);
                Phone.RestoreGalleryItems(data.phoneGallery);
                Phone.RestoreActions(data.phoneActions);
                Phone.RestoreHiddenApps(data.phoneHiddenApps);
                Phone.DialogueLock = data.phoneDialogueLock; // 2.12.2
            }
            if (Player != null) Player.ChatHubReturn = data.chatHubReturn;

            var script = GetScript(data.scriptName);
            if (script == null)
            {
                VNLog.Warn("Saved script '" + data.scriptName + "' is missing.");
                if (showTitleScreen) Title.Show();
                yield break;
            }
            suppressRollbackCapture = true; // resumed position must not create a duplicate snapshot
            Player.Play(script, data.nextCommandIndex);
        }

        // ============================== Panels (quick menu / buttons) ==============================

        /// <summary>Modals that block opening ANOTHER panel. The phone menu is NOT
        /// blocking: its apps (save/load/settings/backlog) open on top of it.</summary>
        bool IsBlockingModalOpen()
        {
            return BacklogPanel.IsOpen || SaveLoadPanel.IsOpen || SettingsPanel.IsOpen || GalleryPanel.IsOpen
                || (PhotoViewer != null && PhotoViewer.IsOpen)
                || (PauseMenu != null && PauseMenu.IsOpen);
        }

        public void OpenBacklog()   { if (!IsBlockingModalOpen() && !Title.IsOpen) BacklogPanel.Show(backlog); }
        public void OpenSavePanel() { if (!IsBlockingModalOpen() && !Title.IsOpen) SaveLoadPanel.Show(SaveLoadUI.Mode.Save); }
        public void OpenLoadPanel() { if (!IsBlockingModalOpen()) SaveLoadPanel.Show(SaveLoadUI.Mode.Load); }
        public void OpenSettings()  { if (!IsBlockingModalOpen()) SettingsPanel.Show(); }

        public void HideAllPanels()
        {
            BacklogPanel.Hide();
            SaveLoadPanel.Hide();
            SettingsPanel.Hide();
            GalleryPanel.Hide();
            if (PhotoViewer != null) PhotoViewer.Hide();
            if (PauseMenu != null) PauseMenu.Hide();
            if (Phone != null) Phone.CloseMenu();
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

        void LoadSeen()
        {
            seenLines.Clear();
            if (!PlayerPrefs.HasKey(SeenKey)) return;
            try
            {
                var w = JsonUtility.FromJson<StringList>(PlayerPrefs.GetString(SeenKey));
                if (w != null && w.items != null) foreach (var s in w.items) seenLines.Add(s);
            }
            catch (Exception) { /* corrupted data is not fatal */ }
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
            VNLoc.Language = Settings.language;
            scriptCache.Clear(); // localized script variants resolve on next access
            if (Audio != null) Audio.ApplyVolumes();
            ApplyVideoSettings();
            PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(Settings));
        }

        void ApplyVideoSettings()
        {
            if (Settings.resolutionWidth > 0 && Settings.resolutionHeight > 0)
                Screen.SetResolution(Settings.resolutionWidth, Settings.resolutionHeight, Settings.fullscreen);
            else
                Screen.fullScreen = Settings.fullscreen;
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
