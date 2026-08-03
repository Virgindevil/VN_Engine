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
        public TitleUI Title { get; private set; }
        public QuickMenuUI QuickMenu { get; private set; }
        public LoadingUI Loading { get; private set; }

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
        VNMinigame activeMinigame;
        bool hudHidden;
        bool booted;
        bool suppressRollbackCapture;

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

            Variables = new VNVariables();
            Storage = new SaveLoadManager();

            EnsureEventSystem();

            canvas = UIFactory.CreateCanvas("VNKit.Canvas", transform);
            var stageRoot = UIFactory.Rect("Stage", canvas.transform);
            UIFactory.Stretch(stageRoot);
            Backgrounds = new BackgroundManager(stageRoot, this);
            Characters = new CharacterManager(stageRoot, this);
            Cgs = new CgManager(stageRoot, this); // above characters, under the UI overlay

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
            Title = new TitleUI(overlayRT, this);
            Loading = new LoadingUI(overlayRT, gameTitle);

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

            if (VNInput.CancelPressed())
            {
                if (SettingsPanel.IsOpen) SettingsPanel.Hide();
                else if (GalleryPanel.IsOpen) GalleryPanel.Hide();
                else if (BacklogPanel.IsOpen) BacklogPanel.Hide();
                else if (SaveLoadPanel.IsOpen) SaveLoadPanel.Hide();
                else if (!Title.IsOpen && Player.State != PlayerState.Idle && Player.State != PlayerState.Ended) OpenSettings();
                return;
            }

            bool modal = IsModalOpen();
            // While any modal (Settings / Save / Load / Backlog / Gallery) or the title is open,
            // freeze advance / auto / skip so text does not progress behind the panel.
            if (modal || Title.IsOpen)
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
            if (enableRollback && !hudHidden
                && Player.State == PlayerState.WaitingInput && !Player.IsTyping
                && (VNInput.ScrollDelta() > 0.1f || VNInput.KeyPressed(Settings.rollbackKey)))
            {
                DoRollback();
                return;
            }

            if (VNInput.HidePressed()) ToggleHud();
            else if (!hudHidden
                     && Player.State != PlayerState.WaitingChoice
                     && Player.State != PlayerState.Idle
                     && Player.State != PlayerState.Ended
                     && VNInput.AdvancePressed()
                     && !VNInput.PointerOverUI())
                Player.Advance();

            Player.Tick(Time.deltaTime);
        }

        public bool IsModalOpen()
        {
            return BacklogPanel.IsOpen || SaveLoadPanel.IsOpen || SettingsPanel.IsOpen || GalleryPanel.IsOpen;
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
            rollback.Clear();
            Player.Stop();
            Audio.StopBgm(0.25f);
            Audio.StopVoice();
            Characters.ClearAll();
            Backgrounds.Clear(0f);
            Cgs.Hide(0f);

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
            Cgs.Hide(0f);
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
            if (!IsModalOpen()) GalleryPanel.Show();
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
                characters = Characters.GetStates()
            });
        }

        void DoRollback()
        {
            if (rollback.Count == 0) return;
            var snap = rollback.Pop();
            if (snap == null) return;
            StartCoroutine(RollbackRoutine(snap));
        }

        IEnumerator RollbackRoutine(VNRollback.Snapshot snap)
        {
            Player.Stop();
            Choice.Hide();
            Dialogue.Hide();

            Variables.FromEntries(snap.variables);

            // Trim the backlog back to the snapshot point.
            while (backlog.Count > snap.backlogCount)
                backlog.RemoveAt(backlog.Count - 1);

            // Background
            if (string.IsNullOrEmpty(snap.background)) Backgrounds.Restore(null, null);
            else
            {
                Sprite bg = null;
                yield return LoadBackgroundAsync(snap.background, s => bg = s);
                Backgrounds.Restore(snap.background, bg);
            }

            // Event CG
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

            // BGM
            if (!string.IsNullOrEmpty(snap.bgm))
            {
                AudioClip clip = null;
                yield return LoadBgmAsync(snap.bgm, c => clip = c);
                Audio.PlayBgm(snap.bgm, clip, 0.2f);
            }
            else Audio.StopBgm(0.1f);

            // Characters
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

            hudHidden = false;
            Dialogue.SetHudVisible(true);

            var script = GetScript(snap.scriptName);
            if (script == null) yield break;

            // Re-executing the Say/Choice pushes a fresh snapshot; suppress it so
            // consecutive wheel-ups keep stepping backwards.
            suppressRollbackCapture = true;
            Player.Play(script, snap.commandIndex);
        }

        // ============================== Save / Load ==============================

        public bool SaveGame(int slot)
        {
            if (Player.State == PlayerState.WaitingChoice) { VNLog.Warn("Cannot save during a choice."); return false; }
            if (Player.State == PlayerState.WaitingMinigame) { VNLog.Warn("Cannot save during a mini-game."); return false; }
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
                cg = Cgs.CurrentName,
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
            QuickMenu.SetVisible(true);

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

        public void OpenBacklog()   { if (!IsModalOpen() && !Title.IsOpen) BacklogPanel.Show(backlog); }
        public void OpenSavePanel() { if (!IsModalOpen() && !Title.IsOpen) SaveLoadPanel.Show(SaveLoadUI.Mode.Save); }
        public void OpenLoadPanel() { if (!IsModalOpen()) SaveLoadPanel.Show(SaveLoadUI.Mode.Load); }
        public void OpenSettings()  { if (!IsModalOpen()) SettingsPanel.Show(); }

        public void HideAllPanels()
        {
            BacklogPanel.Hide();
            SaveLoadPanel.Hide();
            SettingsPanel.Hide();
            GalleryPanel.Hide();
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