
VNKit — lightweight visual novel engine for Unity 2022.3+
==========================================================



QUICK START



-----------


1. Open the demo scene: Assets/VNKit/Demo/VNKitDemo.unity and press Play. (The demo uses generated placeholder graphics, no art required.)` 


2. In your own scene:
   GameObject > VNKit > Visual Novel Engine
   then assign a .vns script to "Start Script".

3. Write scripts as .vns files (plain text). See Assets/VNKit/Demo/Scripts for examples and the full README for the command reference.` 

RESOURCE FOLDERS (Tools > VNKit > Create Resource Folders)



----------------------------------------------------------
Assets/Resources/VN/Backgrounds/<Name>.png
Assets/Resources/VN/Characters/<CharName>/<Appearance>.png
Assets/Resources/VN/Audio/BGM|SFX|Voice/<Name>.(wav|mp3|ogg)



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
Space / Enter / Click : advance      Ctrl (hold) : skip
Right click           : hide UI      Esc          : settings
```

