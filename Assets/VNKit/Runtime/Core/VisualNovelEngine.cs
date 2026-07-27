using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VNKit
{
    /*
    VNKit — легковесный, управляемый скриптами движок для визуальных новелл в Unity.
    Перетащите этот компонент на пустой GameObject, назначьте скрипт запуска и нажмите «Играть».
    Вся сцена и пользовательский интерфейс создаются во время выполнения; настройка сцены не требуется.
    Ресурсы (фоны, персонажи, звук) загружаются исключительно через Addressables.
    */
    [AddComponentMenu("VNKit/Visual Novel Engine")]
    public class VisualNovelEngine : MonoBehaviour
    {
        public static VisualNovelEngine Instance { get; private set; }

        [Header("Scripts")]
        public TextAsset startScript;
        public List<TextAsset> additionalScripts = new List<TextAsset>();

        [Header("Content (Addressables)")]
        [Tooltip("Addressables address prefix. Assets must be marked Addressable as e.g. VN/Backgrounds/Campus")]
        public string resourcesRoot = "VN";

        [Header("Presentation")]
        public bool showTitleScreen = true;
        public string gameTitle = "My Visual Novel";
        public Sprite titleBackground;
        public Sprite titleLogo;
        [Tooltip("Generate colored placeholder sprites when an art asset is missing. Great for prototyping.")]
        public bool usePlaceholderGraphics = false;
        public Color dialoguePanelColor = new Color(0f, 0f, 0f, 0.72f);
        public Color accentColor = new Color(0.85f, 0.45f, 0.65f, 1f);

        [Header("Loading")]
        [Tooltip("Show a full-screen loading overlay while Addressables initializes at boot.")]
        public bool showLoadingScreen = true;
        [Tooltip("Optional list of Addressables keys to preload during the boot loading screen (e.g. VN/Backgrounds/Campus).")]
        public List<string> preloadAddresses = new List<string>();

        [Header("Behavior")]
        [Tooltip("Track which lines the player has read; skip mode stops at unread text.")]
        public bool saveSeenText = true;

        public VNSettings Settings = new VNSettings();

        // ---- Службы среды выполнения (создаются в Awake) ----
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
        public LoadingUI Loading { get; private set; }

        // Обработка неизвестных @commands здесь (cmd.Name = имя команды, cmd.Params = параметры)
        public event Action<VNCommand> CustomCommand;
        // Срабатывает, когда скрипт завершается или выполняется команда @end
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
            ApplyVideoSettings();
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
            Loading = new LoadingUI(overlayRoot, gameTitle);

            Audio = new VNAudioManager(transform, this);
            Player = new ScriptPlayer(this, VNRunner.Create("VNKit.Player", transform));

            RegisterScript(startScript);
            foreach (var a in additionalScripts) RegisterScript(a);

            booted = true;
        }

        void Start()
        {
            // Скрыть всё, пока Addressables не будет готов.
            Dialogue.Hide();
            QuickMenu.SetVisible(false);
            Title.Hide();
            if (showLoadingScreen)
                Loading.Show("Initializing…");
            StartCoroutine(BootRoutine());
        }

        IEnumerator BootRoutine()
        {
            // 1) Инициализация адресуемых объектов (каталоги, поставщики). Требуется для удаленных групп / WebGL.
            if (showLoadingScreen) Loading.SetProgress(0.05f, "Initializing Addressables…");
            yield return VNResources.Initialize();

            // 2) Дополнительная предварительная загрузка адресов, указанных в компоненте движка.
            if (preloadAddresses != null && preloadAddresses.Count > 0)
            {
                if (showLoadingScreen) Loading.SetProgress(0.15f, "Preloading assets…");
                yield return VNResources.PreloadSprites(preloadAddresses, (p, addr) =>
                {
                    if (showLoadingScreen)
                        Loading.SetProgress(0.15f + 0.8f * p, string.IsNullOrEmpty(addr) ? "Almost ready…" : "Loading " + addr);
                });
            }
            else
            {
                // Небольшой искусственный шаг, чтобы панель была видна даже без списка предварительной загрузки.
                if (showLoadingScreen)
                {
                    Loading.SetProgress(0.6f, "Ready");
                    yield return null;
                }
            }

            if (showLoadingScreen)
            {
                Loading.SetProgress(1f, "Done");
                yield return null;
                Loading.Hide();
            }

            // 3) Переход на экран игры или титульный экран.
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
                return;
            }

            bool modal = IsModalOpen();
            // Пока открыто любое модальное окно (Настройки / Сохранить / Загрузить / Список дел) или заголовок,
            // заморозьте переход, автовоспроизведение и пропуск, чтобы текст не продолжал отображаться.
            if (modal || Title.IsOpen)
            {
                Player.SetSkipHeld(false);
                return;
            }

            Player.SetSkipHeld(VNInput.SkipHeld());

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

        // ============================== Assets (Addressables) ==============================

        public string BackgroundAddress(string name)
        {
            return resourcesRoot + "/Backgrounds/" + name;
        }

        public string CharacterAddress(string name, string appearance)
        {
            return resourcesRoot + "/Characters/" + name + "/" + (appearance ?? "Default");
        }

        public string BgmAddress(string name)   { return resourcesRoot + "/Audio/BGM/" + name; }
        public string SfxAddress(string name)   { return resourcesRoot + "/Audio/SFX/" + name; }
        public string VoiceAddress(string name) { return resourcesRoot + "/Audio/Voice/" + name; }

        /// <summary>Async background load. Uses placeholder when missing and flag is on.</summary>
        public System.Collections.IEnumerator LoadBackgroundAsync(string name, System.Action<Sprite> onDone)
        {
            Sprite s = null;
            yield return VNResources.LoadSprite(BackgroundAddress(name), x => s = x);
            if (s == null && usePlaceholderGraphics) s = PlaceholderArt.Background(name);
            if (onDone != null) onDone(s);
        }

        // Асинхронная загрузка спрайта персонажа
        public System.Collections.IEnumerator LoadCharacterSpriteAsync(string name, string appearance, System.Action<Sprite> onDone)
        {
            Sprite s = null;
            yield return VNResources.LoadSprite(CharacterAddress(name, appearance), x => s = x);
            if (s == null && usePlaceholderGraphics) s = PlaceholderArt.Character(name);
            if (onDone != null) onDone(s);
        }

        public System.Collections.IEnumerator LoadBgmAsync(string name, System.Action<AudioClip> onDone)
        {
            yield return VNResources.LoadClip(BgmAddress(name), onDone);
        }

        public System.Collections.IEnumerator LoadSfxAsync(string name, System.Action<AudioClip> onDone)
        {
            yield return VNResources.LoadClip(SfxAddress(name), onDone);
        }

        public System.Collections.IEnumerator LoadVoiceAsync(string name, System.Action<AudioClip> onDone)
        {
            yield return VNResources.LoadClip(VoiceAddress(name), onDone);
        }

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
            Dialogue.Hide();
            QuickMenu.SetVisible(false);

            // Загрузка адресных объектов происходит асинхронно — восстановление осуществляется через сопрограмму (с дополнительным экраном загрузки).
            StartCoroutine(LoadGameRoutine(data));
            return true;
        }

        System.Collections.IEnumerator LoadGameRoutine(VNSaveData data)
        {
            if (showLoadingScreen && Loading != null)
            {
                Loading.Show("Loading save…");
                Loading.SetProgress(0.1f, "Restoring state…");
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
                if (showLoadingScreen && Loading != null)
                    Loading.SetProgress(0.25f, "Background…");
                Sprite bg = null;
                yield return LoadBackgroundAsync(data.background, s => bg = s);
                Backgrounds.Restore(data.background, bg);
            }

            // BGM
            if (!string.IsNullOrEmpty(data.bgm))
            {
                if (showLoadingScreen && Loading != null)
                    Loading.SetProgress(0.45f, "Music…");
                AudioClip clip = null;
                yield return LoadBgmAsync(data.bgm, c => clip = c);
                Audio.PlayBgm(data.bgm, clip, 0.5f);
            }
            else Audio.StopBgm(0.1f);

            // Characters
            var states = data.characters;
            var sprites = new List<Sprite>();
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
                        Loading.SetProgress(0.55f + 0.35f * (loaded / (float)visibleCount),
                            "Character: " + states[i].name);
                    Sprite spr = null;
                    yield return LoadCharacterSpriteAsync(states[i].name, states[i].appearance, s => spr = s);
                    sprites.Add(spr);
                    loaded++;
                }
            }
            Characters.RestoreStates(states, sprites);

            if (showLoadingScreen && Loading != null)
            {
                Loading.SetProgress(1f, "Done");
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
            Player.Play(script, data.nextCommandIndex);
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
            catch (Exception) { /* Поврежденные данные не являются фатальными */}
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
            catch (Exception) { /* сохранить значения по умолчанию */ }
        }

        public void ApplySettings()
        {
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