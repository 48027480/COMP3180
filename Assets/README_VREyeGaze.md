# VR Eye Gaze Project

A Unity VR project for COMP3180 that explores aiming and selecting with eye gaze. It has a playground where you pick up objects by looking at them, and a shooter prototype with three timed test areas that save results to CSV.

| | |
|---|---|
| Unity | 6000.3.7f1 (URP) |
| XR | OpenXR, XR Interaction Toolkit 3.5.1, Input System 1.18 |
| Headset | HTC VIVE Focus Vision over VIVE Streaming |
| Eye data | OpenXR Eye Gaze Interaction (where you look), VIVE XR Eye Tracker (how open each eye is) |
| Main scene | `Assets/Scenes/GazePlayground.unity` |

The project also runs without a headset, using the keyboard and mouse.

## Contents

1. [How to run](#how-to-run)
2. [Controls](#controls)
3. [Folder layout](#folder-layout)
4. [Scene hierarchy](#scene-hierarchy)
5. [Logic](#logic)
6. [Scripts](#scripts)
7. [Data saving](#data-saving)
8. [Known limits](#known-limits)

## How to run

1. Open the project in Unity 6000.3.7f1.
2. Open `Assets/Scenes/GazePlayground.unity`.
3. For VR, start VIVE Streaming and connect the headset first.
4. Press Play.

Scripts choose between desktop and headset behaviour with `XRSettings.isDeviceActive`.

The VIVE OpenXR Plugin 2.5.1 is stored in `Packages/com.htc.upm.vive.openxr`, so it needs no download. Its `Samples~` and `UnityPackages~` folders were left out to save about 500 MB.

## Controls

| Action | Desktop | VR controller |
|---|---|---|
| Move | WASD | Left stick |
| Sprint | Shift | Left trigger |
| Turn or look | Hold right mouse and move | Right stick, and your head |
| Grab or drag (hold) | Left mouse | Grips, right trigger, right B, left X |
| Fire (gun equipped, in the prototype area) | Left mouse | Right trigger |
| Press a world button | Aim, then left mouse | Aim, then any grab button |
| Toggle eye gaze | Ctrl | Right A |
| Help board | H | Left stick click |
| Start Movement or Interaction tutorial | 1 or 2 | Aim at the board row, then grab |
| Toggle eye-gaze-assist | Not available | Left Y, or 3 quick winks |

### Eye-gaze-assist

With assist on, aiming always uses your eyes, and your buttons keep working.

| Wink | Result |
|---|---|
| While looking at an object | Grabs it, and it stays held with no button pressed |
| Wink again | Drops it |
| While looking at the gun or a world button | Picks up the gun or presses the button |
| With the gun in the prototype area | Fires, the same as the right trigger |

A wink is one eye closed for at least 0.1 s while the other stays open, so normal blinks don't count.

## Folder layout

```
Assets/
  GazeStudy/
    Scripts/                Playground, input and gaze study scripts
    Editor/                 Scene builders
    Materials/              Playground materials
    ShooterPrototype/
      Scripts/
        Core/               Test flow, area base class, events, CSV, gate
        Areas/              The three test areas and their spawners
        Npc/                NPC health, hit zones and walking
        Player/             Weapon
        Shooting/           Shootable base class
        UI/                 HUD and floating damage text
      Editor/               Adds the prototype to the playground scene
      Prefabs/              Target_Sphere, FloatingTarget_Sphere, NPC, FloatingText
      Materials/
  Scenes/
    GazePlayground.unity    Main scene
    ExperimentalScene.unity Separate controller vs gaze trial study
  GazeInteraction/          Cloned UGIS repository, unused, kept as proof of earlier work
Packages/
  com.htc.upm.vive.openxr/  VIVE OpenXR Plugin 2.5.1
Save/                       CSV results, created on Play
```

## Scene hierarchy

The main objects in `GazePlayground.unity`:

```
XR Origin (XR Rig)              PlayerBody, CharacterController
  Camera Offset
    Main Camera                 The head; holds TutorialPanel
    Gaze Pose                   Driven by the eye gaze action
    Left / Right Controller
  Body                          Capsule that follows the head
PlaygroundManager               GazePickupSelector, GrabInput, WinkInput (added on Play)
PlaygroundOverlay
  StatusText                    Desktop status text
Reticle                         Dot at the point being aimed at
ControllerLine                  Laser, shown when gaze is off
Ground, Wall_North/East/South/West
Station: ...                    Six stations of pickup objects
ShooterPrototype
  Gun                           PickupObject with staysInHand on
  ResetPad                      ResetPad
  PrototypeGate                 PrototypeGate
    Barrier                     Removed once the gun is equipped
  PrototypeZone                 Trigger box: inside it, the gun fires
  TJunction                     Corridor and north-south hall walls
  sign area 2, sign area 3      Hall direction signs
  Area1_AimTrainer              AimTrainerArea
  Area2_Floating                FloatingTargetArea
  Area3_Npc                     NpcArea, plus SpawnerLeft and SpawnerRight
  GameManager                   PlaytestManager, HitscanWeapon
  Canvas                        HudController: Crosshair, ModeLabel, StatsLabel
  HelpBoard                     HelpBoard, TutorialSystem; holds Board
  PlayAgainButton               WorldButton, shown after a test
```

Each area contains `Floor`, `SideWall_Left`, `SideWall_Right`, `BackWall`, `StandPoint`, `StandMarker`, `Sign`, `Targets` and an invisible `Border` trigger box. The Aim Trainer and Floating areas also have a `SpawnVolume`.

Layout: a corridor runs west from the gap in `Wall_West` into a north-south hall, forming a T. The three 30 m wide areas open off the hall's west side.

## Logic

### Aiming and selecting

`GazePickupSelector` runs this every frame:

1. Choose the aim source:

   | Situation | Ray comes from |
   |---|---|
   | Desktop, gaze on | Camera |
   | Desktop, gaze off | No ray |
   | Headset, gaze off | Right controller |
   | Headset, gaze on, eye device found | Eye tracker |
   | Headset, gaze on, no eye device | Head |

2. Raycast to the first collider, so nearer objects block those behind them.
3. Highlight the object or world button that was hit.
4. On a press or a wink: press the button, equip the gun, or start dragging.
5. While dragging, keep the object at the same distance along the ray, stopping short of walls and floors. Releasing drops it with normal physics.

With the gun equipped inside `PrototypeZone`, objects can't be grabbed, because the same input fires instead.

### Wink detection

`WinkInput` reads both eyes' openness every frame on the headset:

- An eye counts as closed below 0.25 and open above 0.6.
- The closed eye must stay closed for 0.1 s while the other stays open.
- Both eyes must reopen before another wink can count.
- Three winks, each within 0.7 s of the last, toggle assist. The third wink only toggles and has no other effect.
- Invalid eye data is ignored.

Winks act instantly, so turning assist off with winks during a test also fires 2 shots, and these count in the results.

### Test flow

```
Idle --step on StandMarker--> Running --timer reaches 0--> Ended (saved, Completed = True)
                                 |                           |
                                 |                           +--Play again--> Running
                                 +--cross the Border, or step on ResetPad--> saved, Completed = False
```

- A test lasts **30 seconds** (`testDuration` on `PlaytestManager`).
- A shot is counted when fired, and a hit when it strikes a `Shootable`. Accuracy is hits divided by shots.
- Once a test has ended, only Play again restarts that area. The reset pad returns everything to Idle.
- Leaving the area ends the test, and the Stats UI fades out over 0.5 s. It fades back in if you return.
- The area's `Sign` is hidden while its test runs and comes back when it ends.

### The three areas

The values below come from the scene and script defaults.

| Area | Behaviour |
|---|---|
| Aim Trainer | Up to 8 still spheres, 0.3 to 1.4 m wide, placed without overlapping. Replacements appear after 0.25 s. |
| Floating Targets | Up to 12 spheres, 0.5 to 1.5 m wide, one every 0.6 s. They rise at 2 to 6 m/s and vanish 25 m up. |
| NPC Range | Two spawners send NPCs walking across at 1.5 to 3 m/s, one every 2 to 4 s, with up to 4 alive per spawner. |

NPCs have 100 HP:

| Hit zone | Damage | Floating text |
|---|---|---|
| Head | 100 | HEADSHOT |
| Body | 33.34 | 33.34HP |
| Legs | 20 | 20HP |

### Gate and reset

- `PrototypeGate` blocks the corridor until the gun is equipped.
- The gun is invisible while held, so it doesn't block the view.
- Stepping on `ResetPad` returns every object and the gun to their start and stops all tests. The gate then closes again.

### Help and tutorials

- H or a left stick click opens the board in front of you. Test runs close it and hide the regular status UI.
- The three tutorials are Movement, Interaction and Eye Gaze. Each step moves on when you do what it asks.

## Scripts

### Playground: `Assets/GazeStudy/Scripts`

| Script | Function |
|---|---|
| `GazePickupSelector` | Central aim, hover, grab, drag and status logic. Exposes the ray for the weapon and owns assist's effect on gaze. |
| `GrabInput` | Reads grab and fire from the mouse and controllers. Controllers only count with a headset active. |
| `WinkInput` | Reads eye openness, detects winks, and toggles assist. Added to `PlaygroundManager` when Play starts. |
| `PlayerBody` | Walking, sprinting, turning and gravity. Fits the capsule and controller to the head position. |
| `PickupObject` | An object that can be highlighted, dragged, dropped, equipped and returned to its start. |
| `WorldButton` | A button in the world that is aimed at and pressed. |
| `ResetPad` | Floor button that resets objects and tests. |
| `HelpBoard` | Opens and closes the help board and starts tutorials. |
| `TutorialSystem` | Runs tutorial steps on a panel in front of the head. |

### Shooter prototype: `Assets/GazeStudy/ShooterPrototype/Scripts`

| Script | Function |
|---|---|
| `Core/PlaytestManager` | Starts, times and ends tests, counts shots and hits, shows Play again. |
| `Core/PlaytestArea` | Base class for areas: activation, target clean-up, border and sign. |
| `Core/PrototypeGate` | Removes the barrier while the gun is equipped. |
| `Core/GameEvents` | Static events for shots, hits and test changes. |
| `Core/TestResultsCsv` | Creates the numbered CSV and writes rows. |
| `Areas/AimTrainerArea` | Spawns still targets. |
| `Areas/FloatingTargetArea`, `Areas/FloatingTarget` | Spawn rising targets and move them up. |
| `Areas/NpcArea`, `Areas/NpcSpawner` | Run the two NPC spawners. |
| `Npc/NpcHealth` | NPC health and removal on death. |
| `Npc/NpcHitZone` | Applies a zone's damage and shows floating text. |
| `Npc/NpcWalker` | Walks an NPC to its end point, then removes it. |
| `Player/HitscanWeapon` | Fires along the current aim ray on a press or wink. |
| `Shooting/Shootable` | Base class for anything that reacts to shots. It disappears by default. |
| `UI/HudController` | Shows test stats and fades them when you leave the area. |
| `UI/FloatingText` | Rising, fading text that faces the camera. |

### Controller vs gaze study: used by `ExperimentalScene.unity`

| Script | Function |
|---|---|
| `TrialManager` | Runs one participant's practice and test blocks, with counterbalanced order. |
| `SelectionRaySource` | Chooses the ray source and computes hover for the study. Also detects the eye gaze device for the playground. |
| `TargetRing`, `SelectableTarget` | Ring of targets sized in degrees of visual angle, with shared highlight behaviour. |
| `ConfirmInput` | The study's confirm press: right trigger or Space. |
| `DataLogger` | Writes one CSV per study session. |
| `DesktopLook` | Mouse look for testing the study without a headset. |
| `GazeProbe` | Function-key checks that eye gaze is working. |
| `ExperimentTypes` | Shared enums and block settings. |

### Editor tools

| Script | Menu item under `COMP3180` | Function |
|---|---|---|
| `GazeStudySceneBuilder` | Build Gaze Study Scene | Builds `ExperimentalScene.unity`. |
| `GazePlaygroundBuilder` | Build Gaze Playground | Builds the playground. **Rebuilding overwrites the scene file.** |
| `ShooterPrototypeBuilder` | Add Shooter Prototype To Playground | Adds the prototype, or if it exists, updates it. |

Running `ShooterPrototypeBuilder` again replaces the help board and Play again button, restyles the HUD text and rebuilds the area borders. Hand edits to the board and button are lost, while other edits stay.

## Data saving

### Shooter tests

Each press of Play creates a new file, even if you run no tests:

| Build type | Location |
|---|---|
| Unity Editor | `Save/` next to `Assets` |
| PC build | `Save/` next to the `_Data` folder of the build |
| Standalone headset | `Save/` in the app's persistent data folder |

**File name:** `TestResult{number}.csv`, starting at 1 and one above the largest number in the folder. Numbering looks only at names matching that pattern. Deleted numbers are only reused if they were the largest.

**Rows:** one per finished test, written as soon as it ends.

| Column | Meaning |
|---|---|
| `DateTime` | When the test ended, `yyyy-MM-dd HH:mm:ss` |
| `Area` | Aim Trainer, Floating Targets or NPC Range |
| `Shots` | Shots fired |
| `Hits` | Shots that hit a target |
| `AccuracyPercent` | Hits divided by shots, times 100 |
| `DurationSeconds` | Time played, up to 30 |
| `Completed` | `True` if the timer reached 0, otherwise `False` |

If the file is locked, for example open in Excel, the results move to the next number and a warning appears in the Console. Nothing is lost.

### Gaze study

`ExperimentalScene` saves separately to `{persistentDataPath}/COMP3180_Data/P{participant}_{date}_{time}.csv`, with one row per trial.

## Known limits

- **Real winks are not confirmed.** The logic passed tests using simulated eye values, but your first headset attempt didn't toggle assist and the cause isn't known yet. The status text in the headset shows live values (`Eyes: L 0.92  R 0.88`) to help find out.
- **The wink thresholds are starting guesses.** They are constants at the top of `WinkInput.cs`.
- **Gaze may shift as an eye closes.** A wink uses your aim at that moment, which could put shots slightly off target.
- **The Y button and other VR inputs need headset testing.** HTC's Focus controller profile isn't enabled in OpenXR.
- **HTC lists its eye tracker data as beta.** It also recommends OpenXR 1.12.1, while this project uses 1.17.1.
- **Area numbers don't match.** The signs say Aim Trainer is Area 3, Floating Targets is Area 1 and NPC Range is Area 2. The HUD shows them as `[1]`, `[2]` and `[3]`.
- **Builds aren't set up.** Build Settings lists only `SampleScene`, so add `GazePlayground` before building.
- **The XR Device Simulator is off.** With a headset connected, it can override the real head and put the camera at floor height. It can be turned back on in XR Interaction Toolkit project settings.
