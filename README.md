
VNKit — lightweight visual novel engine for Unity 2022.3+
==========================================================

Fully programmatic UI (no prefabs). Scripts are plain-text `.vns` files.
All art and audio load asynchronously via Unity Addressables.

REQUIREMENTS
------------
- Unity 2022.3 or newer
- Package: `com.unity.addressables` (Window → Package Manager)

QUICK START
-----------
1. Open the demo scene: `Assets/VNKit/Demo/VNKitDemo.unity` and press Play.
   (Enable **Use Placeholder Graphics** on the engine if you have no art yet.)

2. In your own scene:
   ```
   GameObject → VNKit → Visual Novel Engine
   ```
   Assign a `.vns` TextAsset to **Start Script**.

3. Create content folders:
   ```
   Tools → VNKit → Create Content Folders
   ```
   This creates:
   ```
   Assets/VNContent/Backgrounds/
   Assets/VNContent/Characters/
   Assets/VNContent/Audio/BGM/
   Assets/VNContent/Audio/SFX/
   Assets/VNContent/Audio/Voice/
   Assets/VNScripts/
   ```

4. Mark assets Addressable (Window → Asset Management → Addressables → Groups)
   and set their **Address** using the convention below.

ADDRESSABLES KEYS
-----------------
```
VN/Backgrounds/<Name>                  e.g. VN/Backgrounds/Campus
VN/Characters/<CharName>/<Appearance>  e.g. VN/Characters/Ayame/Smile
VN/Audio/BGM/<Name>
VN/Audio/SFX/<Name>
VN/Audio/Voice/<Name>
```
The prefix (`VN`) is configurable on the engine component (`resourcesRoot`).

BOOT & LOADING
--------------
On Play the engine shows a loading screen while Addressables initializes.
Optionally list addresses in the **Preload Addresses** field on the engine;
those assets are fetched during the boot screen.
LoadGame also uses the same loading overlay.

SCRIPT SYNTAX
-------------
```
; comment
# Label
@bg Campus time:0.8              background with crossfade
@char Hana.Happy pos:left        show/move character
@char Hana hide                  hide one character
@hideChars time:0.5              hide all characters
@bgm Theme fade:1.5              play music
@stopBgm fade:1                  stop music
@sfx Chime vol:0.8               play sound effect
@voice hana_01                   play voice clip
Hana: Dialogue line.             speaker + text
Hana.Happy: Changes + speaks.    appearance prefix
Plain narration line.
@choice "A" goto:La do:x+=1 | "B" goto:Lb if:x>0
@goto Label / @goto Script.Label
@set gold=100, affection+=2
@if affection>0 goto:Good else:Bad
@wait 1.5
@end
```

CONTROLS
--------
```
Space / Enter / Click   advance dialogue
Ctrl (hold)             skip (always, ignores "unread only")
Right click             hide / show UI
Esc                     settings (or close panels)
```
Quick menu also exposes Auto, Skip, Save, Load, Backlog, Settings, Title.

SETTINGS
--------
Tabbed UI: **Sound** / **Video** / **Game**

- Sound — master, BGM, SFX, voice volumes
- Video — resolution list + fullscreen (applied via `Screen.SetResolution`)
- Game  — text speed, auto delay, “Skip only already-seen text”, language, read-only hotkey list

Settings persist in PlayerPrefs.

FEATURES
--------
- Runtime UI built by `UIFactory` (CanvasScaler ScaleWithScreenSize, ref 1920×1080)
- Addressables-only asset pipeline with in-memory cache
- Placeholder coloured sprites for prototyping (`Use Placeholder Graphics`)
- Save / Load system
- Backlog, choices, variables & expressions, cross-script `@goto`
- Skip mode respects “seen text” when the option is enabled
- Custom `@commands` via `VisualNovelEngine.CustomCommand` event

TIPS
----
- Demo scripts live in `Assets/VNKit/Demo/Scripts/`.
- For WebGL / itch.io keep initial download small: put large groups as Remote.
- Enable **Use Placeholder Graphics** to iterate on scripts with zero art.
```