VNKit 2.12.1 — lightweight visual novel engine for Unity 2022.3+
==============================================================

REQUIREMENTS
------------
- Unity 2022.3 LTS
- Addressables package (Window > Package Manager > Addressables) — REQUIRED
- TextMeshPro: Window > TextMeshPro > Import TMP Essential Resources
- Optional: spine-unity runtime (enables Spine characters/CGs automatically)

QUICK START
-----------
1. Open the demo scene: Assets/VNKit/Demo/VNKitDemo.unity and press Play.
   (The demo uses generated placeholder graphics, no art required.)

2. In your own scene: create an empty GameObject, then
   Add Component > VNKit > Visual Novel Engine
   and assign a .vns script to "Start Script".

3. Mark your assets Addressable using this address scheme
   (prefix configurable via "Resources Root"):
   VN/Backgrounds/<Name>          background sprite/texture
   VN/Characters/<Char>/<Look>    character sprite/texture
   VN/CG/<Name>                   event CG
   VN/Audio/BGM|SFX|Voice/<Name>  audio clips

NEW IN 2.0
----------
- Fixed zero-size dialogue UI; layouts adapt to any device resolution
- Addressables + async loading + boot loading screen with progress
- Fixed invisible backlog (RectMask2D clipping)
- Tabbed settings: Sound / Video (resolution, fullscreen) / Game
  (text speed, skip mode, language, REBINDABLE hotkeys)
- Spine support: animated characters (appearance = animation) and animated CGs
- @cg event CGs + unlockable CG gallery
- VNUITheme asset: fonts, colors, main-menu layout without code
- Minigames: @minigame framework + built-in Skyrim-style Lockpick
- Rollback (mouse wheel up / PageUp), Ren'Py / Naninovel style
- Localization: script variants (Demo.ru.vns) + engine UI in EN/RU
- TextMeshPro text everywhere

SCRIPT SYNTAX
-------------
; comment
# Label
@bg Campus time:0.8              background with crossfade
@char Hana.Happy pos:left        show/move character
@char Hana hide                  hide one character
@hideChars time:0.5              hide all characters
@cg Sunset fade:1.2              full-screen event CG (unlocks in gallery)
@cg off fade:0.8                 hide CG
@bgm Theme fade:1.5              play music
@stopBgm fade:2                  stop music
@sfx Chime vol:0.8               one-shot sound
@voice hana_01                   voice line
@stopVoice
@minigame Lockpick difficulty:1 picks:3 var:lockResult
@set gold=100, affection+=2
@if affection>0 goto:Good else:Normal
@goto OtherScript.Label
@choice "Text A" goto:A do:a+=1 if:score>0 | "Text B" goto:B
@wait 1.5
@end

Hana: Line with a speaker name.
Hana.Smile: Change appearance/animation, then speak.
Narration without a speaker prefix.

HOTKEYS (defaults, rebindable in Settings > Game)
-------------------------------------------------
Advance        Space / Enter / LMB
Skip (hold)    Ctrl
Auto mode      A
Rollback       Mouse wheel up / PageUp
Hide UI        RMB
Cancel/Close   Esc

MINIGAMES
---------
Built-in: Lockpick (Skyrim-style). Your own:
  class MyGame : VNMinigame { ... Complete(true, "1"); }
  VNMinigames.Register("MyGame", () => new MyGame());
then use @minigame MyGame var:result in a script.

LOCALIZATION
------------
Put Demo.vns and Demo.ru.vns side by side and register both;
the engine picks the variant matching Settings > Game > Language.
Engine UI strings: VNLoc.Add("ja", yourDictionary).

Full documentation: README.md (next to the Assets folder / in the zip).
