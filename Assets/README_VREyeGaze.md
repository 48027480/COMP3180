# VR Eye Gaze Project

A Unity VR project for COMP3180 that explores aiming and selecting with gaze. It has a playground where you pick up objects by looking at them, a shooter prototype with three timed test areas, and seven gaze test playgrounds. Every test saves its results to CSV.

**Read this first: everything tested so far used head gaze, not eye gaze.** The eye-gaze code is written, but the project can't yet read eye data from the headset. See [Project status](#project-status).

| | |
|---|---|
| Unity | 6000.3.7f1 (URP) |
| XR | OpenXR, XR Interaction Toolkit 3.5.1, Input System 1.18 |
| Headset | HTC VIVE Focus Vision over VIVE Streaming |
| Gaze used today | Head gaze (the direction the headset faces) |
| Eye data, written but not working yet | OpenXR Eye Gaze Interaction (where you look), VIVE XR Eye Tracker (how open each eye is) |
| Main scene | `Assets/Scenes/GazePlayground.unity` |

The project also runs without a headset, using the keyboard and mouse.

## Contents

1. [Project status](#project-status)
2. [How to run](#how-to-run)
3. [Controls](#controls)
4. [Folder layout](#folder-layout)
5. [Scene hierarchy](#scene-hierarchy)
6. [Logic](#logic)
7. [The ten playgrounds](#the-ten-playgrounds)
8. [Scripts](#scripts)
9. [Data saving](#data-saving)
10. [Known limits](#known-limits)

## Project status

### What I found

- I described this project as being about eye gaze. After evaluating it, I realised that what I had actually built and tested is **head gaze**: the aim ray follows the headset, not the eyes.
- The eye-gaze feature is written. `GazePickupSelector` aims with the eye tracker when an eye gaze device is present, and `WinkInput` reads each eye's openness. But I could not get eye data out of the headset over VIVE Streaming, and I don't know the cause yet.
- When no eye device is found, the playground falls back to the head, so every playtest so far is a head-gaze result.

### What I did about it

- I did not give up on the project. I built three playgrounds (the shooter areas) that playtest the build with head gaze now and will work the same way with eye gaze.
- Because I can't continue the eye-gaze work until the data problem is solved, I am building the test playgrounds first, so they are ready when eye gaze works.
- The plan is **ten playgrounds**. All ten are now in the scene. Playgrounds 4 to 10 were written for the eye-gaze feature I will implement in the future.

### Where each part stands

| Part | State |
|---|---|
| Head gaze aiming, grabbing, shooting | Working and playtested |
| Playgrounds 1 to 3 (shooter areas) | Working and playtested with head gaze |
| Playgrounds 4 to 10 (gaze tests) | Built. An automated desktop run started each one, ran it to the end and saved its log without errors. Not yet played by a person, on desktop or in the headset |
| Eye gaze aiming | Code written. No eye data arrives from the headset |
| Wink input and eye-gaze-assist | Code written and checked with simulated values. Needs eye data to work |

All ten playgrounds use the same aim ray. They run on head gaze today and switch to the eye tracker on their own once an eye device is found. Every row of the gaze log records which source was used (`AimSource`), so head-gaze and eye-gaze results can't be mixed up later.

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
| Confirm in a gaze test | Left mouse | Any grab button |
| Toggle gaze aiming | Ctrl | Right A |
| Help board | H | Left stick click |
| Start Movement or Interaction tutorial | 1 or 2 | Aim at the board row, then grab |
| Toggle eye-gaze-assist | Not available | Left Y, or 3 quick winks |

On desktop, "gaze" is the centre of the camera. With gaze aiming off there is no aim ray on desktop, so the gaze tests need it on.

### Eye-gaze-assist

With assist on, aiming always uses your eyes, and your buttons keep working. It depends on eye data, so it doesn't work in the headset yet.

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
        Areas/              The three shooter areas and their spawners
        Npc/                NPC health, hit zones and walking
        Player/             Weapon
        Shooting/           Shootable base class
        UI/                 HUD and floating damage text
      Editor/               Adds the prototype to the playground scene
      Prefabs/              Target_Sphere, FloatingTarget_Sphere, NPC, FloatingText
      Materials/
    GazeTests/
      Scripts/
        Core/               Gaze test base class, gaze target, gaze log CSV
        Areas/              The seven gaze playgrounds (4 to 10)
      Editor/               Adds the gaze playgrounds to the playground scene
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
ShooterPrototype                West wing: playgrounds 1 to 3
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
  GameManager                   PlaytestManager (runs all ten areas), HitscanWeapon
  Canvas                        HudController: Crosshair, ModeLabel, StatsLabel
  HelpBoard                     HelpBoard, TutorialSystem; holds Board
  PlayAgainButton               WorldButton, shown after a test
GazeTests                       Playgrounds 4 to 10, grouped by category
  East_EyeMovementBasics        Wing through Wall_East
    Wall_East_OtherHalf, CategorySign, TJunction
    Area4_SaccadeGrid           SaccadeGridArea
    Area5_SmoothPursuit         SmoothPursuitArea
    Area6_FixationDwell         FixationDwellArea
  South_AttentionAndPerception  Wing through Wall_South
    Wall_South_OtherHalf, CategorySign, TJunction
    Area7_VisualSearch          VisualSearchArea
    Area8_PeripheralAwareness   PeripheralAwarenessArea
    Area10_MemoryAttention      MemoryGazeArea
  North_GazeUI                  Wing through Wall_North
    Wall_North_OtherHalf, CategorySign, TJunction
    Area9_GazeMenu              GazeMenuArea
```

Each area contains `Floor`, `SideWall_Left`, `SideWall_Right`, `BackWall`, `StandPoint`, `StandMarker`, `Sign`, `Targets` and an invisible `Border` trigger box. The Aim Trainer and Floating areas also have a `SpawnVolume`. The gaze areas create their targets under `Targets` when a test starts and remove them when it ends.

### Layout

The room is 30 m square. Each wall has a 3 m gap that leads down a 10 m corridor into a hall, forming a T. The test areas open off the far side of the hall.

| Wall | Category | Areas | Area size |
|---|---|---|---|
| West | Shooter prototype | 1 Aim Trainer, 2 Floating Targets, 3 NPC Range | 30 m wide |
| East | Eye movement basics | 4 Saccade Jump Grid, 5 Smooth Pursuit Tracker, 6 Fixation and Dwell | 14 m wide, 16 m deep |
| South | Attention and perception | 7 Visual Search, 8 Peripheral Awareness, 10 Memory and Attention | 14 m wide, 16 m deep |
| North | Gaze UI | 9 Gaze Menu Room | 14 m wide, 16 m deep |

The gaze areas are narrower than the shooter areas so the four wings don't run into each other. Only the west wing has a gate, since only the shooter areas need the gun. A sign above each corridor names the category and its areas.

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

   The eye tracker row has not been reached on the real headset yet, so "gaze on" in the headset has meant the head.

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

All ten areas share one flow, run by `PlaytestManager`:

```
Idle --step on StandMarker--> Running --timer reaches 0--> Ended (saved, Completed = True)
                                 |                           |
                                 |                           +--Play again--> Running
                                 +--cross the Border, or step on ResetPad--> saved, Completed = False
```

- A shooter test lasts **30 seconds** (`testDuration` on `PlaytestManager`). A gaze test sets its own length (`testSeconds` on the area): 45 s for areas 4 to 6, 60 s for 7 and 8, and 90 s for 9 and 10.
- The HUD shows two counts and their ratio. For the shooter areas they are shots and hits. For the gaze areas they are trials and correct trials, except Smooth Pursuit, which shows gaze samples and samples on target.
- Once a test has ended, only Play again restarts that area. The reset pad returns everything to Idle.
- Leaving the area ends the test, and the Stats UI fades out over 0.5 s. It fades back in if you return.
- The area's `Sign` is hidden while its test runs and comes back when it ends.

### How a gaze test works

`GazeTestArea` is the base class for areas 4 to 10:

- When the test starts, it notes where the head is and which way the bay faces. All targets are placed around that point, so stay on the marker during the test.
- Every frame it takes the selector's aim ray and raycasts for a `GazeTarget`. That target counts as "looked at".
- Dwell: a target fills (grows and turns green) while looked at and resets if the gaze leaves. Once it fires, it can't fire again until the gaze has left it.
- Confirm is a grab press, or a wink under assist.
- It logs every event, and a gaze sample 20 times a second, to the gaze log.

### Gate and reset

- `PrototypeGate` blocks the west corridor until the gun is equipped.
- The gun is invisible while held, so it doesn't block the view.
- Stepping on `ResetPad` returns every object and the gun to their start and stops all tests. The gate then closes again.
- `PrototypeZone` now ends at the west hall instead of the room wall, so the gun fires from the hall onwards and not in the corridor. This keeps it out of the south wing.

### Help and tutorials

- H or a left stick click opens the board in front of you. Test runs close it and hide the regular status UI.
- The three tutorials are Movement, Interaction and Eye Gaze. Each step moves on when you do what it asks.

## The ten playgrounds

The values below come from the scene and script defaults. Each gaze area's values can be changed on its component in the Inspector.

### 1 to 3: shooter areas (west)

| Area | Behaviour |
|---|---|
| 1 Aim Trainer | Up to 8 still spheres, 0.3 to 1.4 m wide, placed without overlapping. Replacements appear after 0.25 s. |
| 2 Floating Targets | Up to 12 spheres, 0.5 to 1.5 m wide, one every 0.6 s. They rise at 2 to 6 m/s and vanish 25 m up. |
| 3 NPC Range | Two spawners send NPCs walking across at 1.5 to 3 m/s, one every 2 to 4 s, with up to 4 alive per spawner. |

NPCs have 100 HP:

| Hit zone | Damage | Floating text |
|---|---|---|
| Head | 100 | HEADSHOT |
| Body | 33.34 | 33.34HP |
| Legs | 20 | 20HP |

### 4 to 6: eye movement basics (east)

**4 Saccade Jump Grid.** A 5 by 4 grid of dots, 6 m away. One dot is lit, and looking at it is a hit. The light then jumps to a random dot, so the jump distance and direction vary. Each dot must be reached within 1, 1.5 or 2 s, or it is a miss.

- Measures: latency (light on to gaze arriving), landing error, and overshoot (how far the gaze went past the dot along the line of the jump, watched for 0.25 s after landing).

**5 Smooth Pursuit Tracker.** One orb moves along a circle, then a figure-8, then a sine wave, 12 s each. Its speed jumps between 0.6, 1 and 1.6 times normal every 3 to 5 s, and it turns invisible for 0.5 s every 5 to 8 s while it keeps moving. Each 3 s with the gaze on the orb fills a ring around it.

- Measures: time on target, tracking error, and the gaze and orb speeds in every sample, which give pursuit gain.

**6 Fixation and Dwell.** Ten targets around the player, in sizes of 0.3, 0.5 and 0.8 m. One is blue: hold the gaze on it for the dwell time shown (0.5, 1 or 2 s). Any target held for that long activates, so activating a grey one is a false trigger. After every 4 activations the spacing switches between 24° and 12° and the sizes are redrawn.

- Measures: false triggers (the Midas touch problem), dwell success rate, aborted dwells on the goal, and time to activate.

### 7, 8 and 10: attention and perception (south)

**7 Visual Search.** A curved wall of 12, 24 or 40 blue spheres, out to 45° either side, hides one odd object. Easy is a red cube, medium is a blue cube, hard is a sphere in a slightly different blue. Confirm by holding the gaze on it for 1 s or by pressing grab. A wrong pick turns dark and the search goes on.

- Measures: search time, the order objects were looked at (scan path), how many were looked at, and revisits.

**8 Peripheral Awareness.** Keep looking at the centre cross. A white flash lasts 0.2 s at 10°, 20° or 40° from the centre, in one of 8 directions, every 1.5 to 3 s. React within 1.5 s by pressing grab or by looking at where it was. The cross also changes colour every 3 to 7 s. Counting those changes is the second task: ask the player for their count and compare it with `colourChanges` in the Summary row.

- Measures: detection by angle, reaction time, false alarms, and drift from the centre (the angular error of every sample).
- Flashes that would land below the floor are redrawn, so the downward directions only appear at 10°.

**10 Memory and Attention.** Two halves of 45 s each.

- Sequence: six objects flash yellow in an order, starting with 3. Look at them in the same order (0.5 s dwell, or grab). A right answer makes the next sequence one longer and adds purple flashes to ignore.
- Guard: a guard walks back and forth. Keep watching it. It turns yellow for 0.7 s as a warning, then red for 1.5 to 2.5 s. While it is red, the gaze must be on the safe zone or the player is seen. Each escape makes the guard faster.
- Measures: sequence accuracy and longest sequence, time to reach the safe zone, times seen, and the share of patrol time spent watching the guard.

### 9: gaze UI (north)

**9 Gaze Menu Room.** A panel 3.5 m away with number buttons 1 to 9, a slider with stops 0 to 10, and an A to Z grid. Each block gives one task: select three numbers in order, type a word, or set the slider. Blocks rotate through the input methods, first with large buttons (0.26 m, 0.06 m gap) and then small ones (0.16 m, 0.04 m gap).

| Method | How to select |
|---|---|
| Dwell | Hold the gaze on a button for 0.8 s |
| Gaze + click | Look at a button and press grab |
| Controller ray + click | Aim the controller laser and press grab. Headset only |

- Measures: time per selection, error rate, and time per task, each tagged with the method and button size.
- Comfort isn't measured by the project. Ask the player after each run.
- The test turns gaze aiming on or off to suit the method and puts it back when the test ends.

## Scripts

### Playground: `Assets/GazeStudy/Scripts`

| Script | Function |
|---|---|
| `GazePickupSelector` | Central aim, hover, grab, drag and status logic. Exposes the ray and its source for the weapon and the gaze tests, and owns assist's effect on gaze. |
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
| `Core/PlaytestManager` | Starts, times and ends tests in all ten areas, counts shots and hits, shows Play again. |
| `Core/PlaytestArea` | Base class for areas: activation, target clean-up, border and sign. An area can set its own test length and HUD labels. |
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

### Gaze tests: `Assets/GazeStudy/GazeTests/Scripts`

| Script | Function |
|---|---|
| `Core/GazeTestArea` | Base class for areas 4 to 10: aim ray, looked-at target, dwell, target and label creation, logging. |
| `Core/GazeTarget` | A thing to look at, with an ID, a colour and the dwell fill effect. |
| `Core/GazeLogCsv` | Creates the numbered gaze log and writes rows when a test ends. |
| `Areas/SaccadeGridArea` | Playground 4. |
| `Areas/SmoothPursuitArea` | Playground 5. |
| `Areas/FixationDwellArea` | Playground 6. |
| `Areas/VisualSearchArea` | Playground 7. |
| `Areas/PeripheralAwarenessArea` | Playground 8. |
| `Areas/GazeMenuArea` | Playground 9. |
| `Areas/MemoryGazeArea` | Playground 10. |

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
| `GazePlaygroundBuilder` | Build Gaze Playground | Builds the playground, the shooter prototype and the gaze tests. **Rebuilding overwrites the scene file.** |
| `ShooterPrototypeBuilder` | Add Shooter Prototype To Playground | Adds the prototype, or if it exists, updates it. |
| `GazeTestsBuilder` | Add Gaze Tests To Playground | Adds the three gaze wings and their seven areas. Needs the shooter prototype in the scene. |

Running `ShooterPrototypeBuilder` again replaces the help board and Play again button, restyles the HUD text and rebuilds the area borders. Hand edits to the board and button are lost, while other edits stay.

Running `GazeTestsBuilder` again deletes and rebuilds everything under `GazeTests`, so hand edits there are lost, including changed values on the area components. It also resizes `Wall_East`, `Wall_South` and `Wall_North` to make the openings and sets where `PrototypeZone` ends.

## Data saving

All files go to the `Save` folder:

| Build type | Location |
|---|---|
| Unity Editor | `Save/` next to `Assets` |
| PC build | `Save/` next to the `_Data` folder of the build |
| Standalone headset | `Save/` in the app's persistent data folder |

Files are numbered from 1, one above the largest number already in the folder for that name. Deleted numbers are only reused if they were the largest. If a file is locked, for example open in Excel, the rows move to the next number and a warning appears in the Console. Nothing is lost.

### Test results: `TestResult{number}.csv`

Each press of Play creates a new file, even if you run no tests. It gets one row per finished test, in any of the ten areas, written as soon as the test ends.

| Column | Meaning |
|---|---|
| `DateTime` | When the test ended, `yyyy-MM-dd HH:mm:ss` |
| `Area` | The area's name, for example Aim Trainer or Visual Search |
| `Shots` | Shots fired. For a gaze area, trials (or gaze samples in Smooth Pursuit) |
| `Hits` | Shots that hit a target. For a gaze area, correct trials (or samples on target) |
| `AccuracyPercent` | Hits divided by shots, times 100 |
| `DurationSeconds` | Time played, up to the test's length |
| `Completed` | `True` if the timer reached 0, otherwise `False` |

### Gaze log: `GazeLog{number}.csv`

Created the first time a gaze test ends in a play session, then added to by each gaze test after it. It has one row per event and one per gaze sample (20 a second).

| Column | Meaning |
|---|---|
| `Timestamp` | `yyyy-MM-dd HH:mm:ss.fff` |
| `TestSeconds` | Seconds since the test started |
| `Area` | The area's name |
| `Event` | What the row is. See the table below |
| `AimSource` | `Eye tracker`, `Head (no eye device)`, `Controller` or `Desktop camera` |
| `GazeOriginX/Y/Z`, `GazeDirX/Y/Z` | The aim ray in world space. Empty with no ray |
| `TargetId`, `TargetX/Y/Z` | The target the row is about and its world position |
| `Hit` | `True` or `False`. In a Sample row, whether the gaze was on the target |
| `ReactionTimeSeconds` | Time taken, where the event has one |
| `AngularErrorDeg` | Degrees between the aim ray and the target's centre |
| `Difficulty` | The settings in force, as `name=value` pairs split by `;` (size, speed, distance and so on) |
| `Measures` | Extra results for the event, in the same format |

| Area | Events besides `Start`, `Sample` and `Summary` |
|---|---|
| 4 Saccade Jump Grid | `Saccade` |
| 5 Smooth Pursuit Tracker | `SpeedChange`, `Disappear`, `PathChange` |
| 6 Fixation and Dwell | `Activate`, `FalseTrigger`, `DwellAborted` |
| 7 Visual Search | `Found`, `WrongPick` |
| 8 Peripheral Awareness | `Flash`, `ColourChange`, `FalseAlarm` |
| 9 Gaze Menu Room | `Select`, `TaskDone` |
| 10 Memory and Attention | `Recall`, `GuardModeStart`, `GuardLook` |

The `Summary` row closes each test with its totals in `Measures`.

### Gaze study

`ExperimentalScene` saves separately to `{persistentDataPath}/COMP3180_Data/P{participant}_{date}_{time}.csv`, with one row per trial.

## Known limits

- **No eye data reaches the project.** Eye gaze aiming, winks and eye-gaze-assist are written but don't work in the headset over VIVE Streaming, and the cause isn't known. All results so far are head gaze. The status text in the headset shows the aim source and live eye values (`Eyes: L 0.92  R 0.88`, or `Eyes: no data`) to help find out.
- **Playgrounds 4 to 10 are not playtested by a person.** An automated desktop run with a still camera showed that each one starts, runs, ends and writes its log without errors. It could not check correct answers, how the tests look, or anything in the headset. Expect sizes, timings and text positions to need tuning.
- **Head gaze hides what the gaze tests are for.** Saccades, smooth pursuit and peripheral drift are eye movements. With head gaze, those tests measure head movement, so they only check that the test and its logging work. The real measurements need the eye tracker.
- **Hits use the exact ray.** A target counts as looked at only when the ray touches its collider. Eye tracker noise may call for a tolerance later.
- **Targets are placed around the head at the start.** Walking off the marker during a gaze test changes the angles, and the log doesn't correct for it.
- **The wink thresholds are starting guesses.** They are constants at the top of `WinkInput.cs`.
- **Gaze may shift as an eye closes.** A wink uses your aim at that moment, which could put shots slightly off target.
- **The Y button and other VR inputs need headset testing.** HTC's Focus controller profile isn't enabled in OpenXR.
- **HTC lists its eye tracker data as beta.** It also recommends OpenXR 1.12.1, while this project uses 1.17.1.
- **Area numbers on the west signs don't match.** The signs say Aim Trainer is Area 3, Floating Targets is Area 1 and NPC Range is Area 2. The HUD shows them as `[1]`, `[2]` and `[3]`. The gaze areas show `[4]` to `[10]`, matching their signs.
- **Builds aren't set up.** Build Settings lists only `SampleScene`, so add `GazePlayground` before building.
- **The XR Device Simulator is off.** With a headset connected, it can override the real head and put the camera at floor height. It can be turned back on in XR Interaction Toolkit project settings.
