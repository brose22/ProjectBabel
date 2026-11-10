# Project Babel

*Working title.* A roguelite tower defense in 2D pixel art where every tower is a character.

You claim cities on a world map, defend them in wave-based matches, and use the intel from each match to track down the enemy's main base. Each tower is run by a **captain** whose approval of your decisions shapes how well their tower performs.

## The game at a glance

- **Runs:** about 30 matches on a tactics-style overworld. Global health lasts the whole run; enemies that leak through reduce it, and at zero the run is lost.
- **Matches:** 10 waves, 5–10 minutes. Place up to 5 towers, answer captains' requests yes or no (they pause the match while they wait), and relocate towers mid-match for Gold; moves are free until the first wave starts. The final match has double the waves and a very slow boss.
- **Captains:** rolled from 5 captain templates (`Assets/Data/Captains`), one per role: Knight, Mage, Rogue, Archer, Healer. A rolled captain gets a name, portrait, element (Water, Fire, Ice, Grass), damage type, stats within the template's ranges, one special ability from its pool, 3 likes and 3 dislikes, and liked and disliked gifts. The damage type is usually the role's (Knight and Rogue Melee, Mage and Healer Magic, Archer Range); about 15% roll a special class such as a Magic Knight. The special decides whether the captain fights or supports: most hit enemies, a heal restores a share of health to every tower and soldier, and a boost helps every tower and soldier for the rest of the wave. The special's meter fills mainly from the captain's soldiers fighting (damage dealt, weakness hits, hits on afflicted enemies, knockbacks and kills, capped per wave), plus a slow trickle over time.
- **Relationships:** captains judge your play style (leaks, casualties, selling, ability use, stable or moving placement) and soldier mix. Their approval and bond rank (D → S) carry over between runs and boost their tower.
- **Soldiers:** Knights, Archers, Mages and Rogues leave their tower to attack the closest enemy, then return. A Knight's hit has a 25% chance to knock the enemy back along the road. Every act and respawn costs Gold. Each class picks its own home tile near the tower, previewed while you place or move it.
- **Economy:** one shared Gold pool, earned from kills, wave clears and a win bonus, and carried between matches. Kills and wins pay more further into the run. Towers draw from the pool into their own reserve at Low, Normal or High rates, set through captain requests, and unspent reserves return to the pool when a match is won.

## Status

| Milestone | Scope | State |
|---|---|---|
| M1: grey-box match | Path, waves, global health and Gold, tower placement, Attacker captains, basic soldiers, placeholder HUD | Done (merged) |
| M2: soldier system | Four soldier classes (Knight knockback), soldier mix, tower damage/disable/restore, relocation | Done (merged) |
| M3: roles and elements | Status effects, special abilities and hit-stop, Buffer and Healer specials, levels and XP, enemy types and weaknesses | Done (merged) |
| M4: relationships | Approval (saved between runs), like/dislike judging, requests, bond ranks D–S, walkouts | Done (merged) |
| M5: vertical slice | City map, Cinder / Marin / Ember, run setup and results, uGUI HUD, generated pixel art, stats log and tuning | Done (merged) |
| M6: world map and run | Branching world map to the enemy base, intel reveals roads, 4 city layouts, scaling difficulty, pick 3 and recruit up to 8, run saved between matches (short 7-match test run) | Done (merged) |
| M7: map stops and the boss | Elite cities, rests, shops (approval gifts) and events on the world map, health back after wins and at rests, the enemy base's very slow boss | Done (merged) |

Since M7: main menu and settings, party bar and captain widget, enemy combat feedback, Excel data tuning, the AI Run mode, Gold economy tuning, an Android build for Fire tablets, multi-gate layouts, enemy walk cycles, and the audio system (music and sound, with most sound effects still to come).

Next up: the remaining content (toward the ~30-match run), polish, and release prep.

### How a run plays (M7)

1. **Setup:** pick 3 captains from a freshly rolled offer (every role once before any repeats).
2. **World map:** pick a stop on the next layer. Every road leads to the enemy base in the last layer. You only see the roads your intel has revealed; each stop reveals the next 2 layers beyond it. Besides plain cities, the middle layers hold:
   - **Elite cities:** play 2 layers tougher, pay double Gold for kills, and offer one extra captain to recruit.
   - **Rests:** restore 30% of max health.
   - **Shops:** buy approval gifts for party captains (75 Gold for +10 approval).
   - **Events:** a short scene with 2–3 choices that change Gold, health, the party's XP or approval.

   Rests, shops and events take the place of a match: no fight, but no match XP or Gold either.
3. **Match:** the city is played on one of 6 layouts (Riverside, Hillfort, Market Square, Long Road, Twin Gates, Three Gates). Twin Gates has two gates and two roads to the one castle; Three Gates has three, with the castle in the middle of the map. Both appear from the third layer and are named in the city tip; wave groups alternate between gates unless a group picks one, and the boss takes a random gate. Enemies get tougher and more numerous the further into the run you are. The enemy base plays its waves twice, and its boss, the Warlord, walks on with the first wave: very slow and very tough. The base falls only once every wave is cleared and the Warlord is dead; if it reaches the end of the road, the run is lost.
4. **After a win:** the win bonus is paid, tower reserves return to the Gold pool, 2 health comes back, and up to 3 captains offer to join; recruit one or decline (the party holds 8, not counting walkouts). Health, Gold, XP and walkouts carry over.
5. Taking the enemy base wins the run; reaching 0 health loses it.

The run is saved to `run.json` in the persistent data path whenever you're on the map or have just won a city. **Resume run** on the main menu picks it up on the world map; **Start run** asks first, then deletes it. **Abandon run** on the map deletes it too, and **Main menu** on the setup, map and results screens goes back to the menu without losing it. Run length, layer sizes, intel depth, the first layer for multi-gate layouts, node odds, elite tuning, the boss and difficulty growth are on the match data asset under **World map**; Gold rewards are there too; healing, shop prices and the event pool sit under **Healing**, **Shop** and **Events**. Hovering a city on the map or a captain's ability shows a tip. Events are `EventData` assets in `Assets/Data/Events`.

## Getting started

**Requirements:** Unity **6000.5.5f1** with the Universal Render Pipeline (2D). Packages are listed in `Packages/manifest.json` and include Tilemap, Aseprite import and the Input System.

1. Clone the repository and open the folder in Unity Hub with editor version 6000.5.5f1.
2. Press **Play**. The game starts on the main menu (`Assets/Scenes/MainMenu.unity`) whichever scene is open, while **Babel → Play From Main Menu** is ticked; untick it to play the open scene directly, such as `Assets/Scenes/Match.unity`.
3. To regenerate the grey-box scene, use the menu **Babel → Build Grey-Box Match Scene**. Existing data assets and prefabs are kept, so your tuning survives a rebuild. **Babel → Build Main Menu Scene** regenerates the menu scene and keeps it first in Build Settings.
4. **Babel → Build Windows Player** builds a standalone player into `Builds/Windows`, starting on the main menu.
5. **Babel → Build Android APK (Fire Tablet)** builds a sideloadable APK into `Builds/Android` for an Amazon Fire tablet (needs Android Build Support from Unity Hub). It sets the Android player to IL2CPP, 32- and 64-bit ARM and landscape only. Copy the APK to the tablet with *Apps from Unknown Sources* allowed, or run `adb install -r Builds/Android/ProjectBabel.apk`.

### Tuning data in Excel

**Export** and **Import** on the toolbar of each **Babel → Data** window, or **Babel → Data → Export to Excel...** and **Import from Excel...**, move all the gameplay data through one `.xlsx` workbook. Every button covers every data type, whichever window it is in, except the Audio Library, which stays out of the workbook.

- **Sheets:** Captains, Abilities, Enemies, Soldiers, Elements, Events, Matches and Layouts have one row per asset, keyed by the file name in the **Asset** column. Lists of records get their own sheets (Events Choices, Matches Waves, Matches Waves Groups, Layouts Props) with one row per record, tied to the asset by **Asset** and the **#** columns. Positions must run 1, 2, 3 within a list.
- **Cell formats:** enums are written by name, colours as `r, g, b, a`, cells as `x, y`, and lists of plain values in one cell separated by `; `. A data asset reference is its name; a sprite or tile reference is its path, with `#Name` for a sprite inside a file. `None` or an empty cell clears a reference.
- **Import** updates existing assets and never creates, renames or deletes them. Adding or removing rows on a list sheet grows or shrinks that list. A bad value, unknown row or unknown column is skipped and left as it was, with a Console warning naming the sheet and row; each changed asset is logged, and **Undo** reverts the whole import. Rename an asset in Unity, not in the workbook: a changed name reads as an unknown row.

### Audio

`AudioManager` plays every sound and music track by key, such as `AudioManager.Play(AudioKeys.EnemyKilled)`. It creates itself before the first scene, survives scene loads and owns the game's only `AudioListener`, so don't add one to a camera.

- **Library:** **Babel → Data → Audio Library** edits `Assets/Resources/AudioLibrary.asset`. Each key has a channel (Music, Sfx or Ui), one or more clips (picked at random), a volume, a pitch variance and a cooldown, with a **Preview** button. After adding or renaming keys, press **Generate AudioKeys** to rewrite `Assets/Scripts/Core/AudioKeys.cs`.
- **Clips:** source files are in `Assets/Audio`. 6 of the 18 keys have clips so far: both music tracks, `ui.click`, `enemy.killed`, `tower.placed` and `ability.used`. The rest stay silent and log one Console warning per session.
- **Events:** `AudioEvents` turns `GameEvents` into sounds and picks the music for each scene, so gameplay code makes no audio calls. Music crossfades between the menu and match tracks.
- **Mixer:** `Assets/Data/Audio/GameAudio.mixer` has a group for each channel, driven by the volumes in Settings.

### Main menu

- **Start run:** a new run, from the captain pick. If a run is saved, you're asked first, since it is deleted.
- **Resume run:** continues the saved run on the world map (greyed out without one).
- **AI Run:** the AI plays 1–50 whole runs at 1x–8x speed, then writes a report (see [AI Run](#ai-run)).
- **Settings:** three tabs. Changes apply at once, are kept in PlayerPrefs and are applied again at every launch.
  - **Graphics:** resolution, window mode (fullscreen, borderless, windowed), VSync, frame-rate cap (used while VSync is off) and quality level. The editor's Game view ignores resolution and window mode.
  - **Audio:** separate Music, Sound effects and Interface volumes.
  - **Gameplay:** on/off toggles for each piece of enemy combat feedback: damage numbers, Weak! / Resist labels, hit flash, stun stars, knockback effects, death burst, status icons, health bar extras, the leak edge flash and the elite / boss banner.
- **Exit:** quits the game (stops Play mode in the editor).

### AI Run

The AI picks a party and route, then places towers, answers requests and fires specials by itself. An overlay shows what it is doing, with a speed pick and **Stop and write report**. A batch ends when its runs are done or you stop it.

- **Saves:** it uses its own `run.json` in an `ai` folder under the persistent data path, reset at the start of each batch, so your run is untouched. It writes no match CSVs to `stats/`.
- **Report:** a Markdown report and a per-wave CSV go to `Reports/` in the project folder, named `AiRun_<date>_<time>.md` and `AiRun_<date>_<time>_waves.csv`.
- **Code:** the decisions (`AiRoutePicker`, `AiPartyPicker`, `AiStopChoice`, `AiPlacementScorer`, `AiAbilityRules`) and `AiReportFormatter` are plain classes in `Assets/Scripts/Core/Pure` with EditMode tests. `AiPlayer` in `Assets/Scripts/AI` drives the game with them.

### Controls

| Input | Action |
|---|---|
| Left-click a free tile | Place the next captain's tower |
| Click a captain in the party bar (bottom) | On the field: fire their special when its meter is full. Not yet placed: make them the next to place |
| `M` | Move the selected tower (then click a free tile); free until the first wave starts |
| Left-click a tower | Select it and open its captain widget |
| `X` | Sell the selected tower (returns its reserve) |
| `Space` | Start the next wave |
| `Esc` or right-click | Deselect, or cancel a move |
| **Speed** button (top bar) | Switch the game speed between x1 and x2 |
| **Pause** button (top bar) | Open the pause menu (`Esc` resumes) |
| `F1` | Cheat window, editor and development builds only (win the match, kill the boss, +500 Gold, full health, reveal the map, soldiers can't die) |
| `F2` | Full captain debug panel, editor and development builds only (likes and dislikes, Gold draw, soldier class switching) |
| `1` / `2` / `3` | While the `F2` panel is open: set the selected tower's Gold draw to Low / Normal / High |

**Gamepad:** a full run can be played on a controller. Whichever device was used last is active: the mouse cursor hides while the gamepad is in use, and icons of its buttons (Xbox, PlayStation, Switch or Steam Deck) appear next to what they control. In menus, the left stick or D-pad moves focus, A confirms, B goes back and the right stick scrolls lists. In a match:

| Button | Action |
|---|---|
| Left stick / D-pad | Move the tile cursor |
| A | Place or select; on the selected tower, go into its widget (Use, Restore, Move, Sell); Yes on a request |
| B | Deselect or cancel a move; leave the widget; No on a request |
| X | Move the selected tower |
| Y | Start the next wave |
| LB / RB | Step through the party: a captain on the field moves the cursor to them, one not yet placed becomes next to place |
| RT | Fire the highlighted captain's special |
| Right stick | Pan the camera |
| LT + right stick up/down | Zoom |
| R3 | Recenter the camera |
| View | Switch the game speed |
| L3 | Show or hide the controls hint |
| Start | Pause (Start or B resumes); the Stats list is in the pause menu |

On a Steam Deck, launch the game through Steam (as a Non-Steam Game), since Desktop Mode normally treats the Deck's controls as mouse and keyboard. There is no in-game rebinding, and the `F1` and `F2` panels stay keyboard only. `GameInput` holds every action and binding; `UIFocus` and `UIFocusScope` handle menu focus, a new screen or window joins in with `UIKit.Focus`, and `UIKit.PadBadge` puts a button's icon on a widget. The icons come from the pack in `Assets/Art/Controller` through `Assets/Resources/PadIconLibrary.asset`; after changing which art is used, run **Babel → Build Pad Icon Library**.

## Project layout

```
Assets/
  Scripts/
    AI/       AI Run driver, recorder and overlay
    Core/     Run-wide state (health, Gold), saves, settings, audio and the GameEvents event bus
      Pure/   Rules with no scene dependencies (Gold, charge, bonds, world map, AI decisions, audio), unit tested
    Data/     ScriptableObject definitions: captains, soldier classes, enemies, matches, city layouts, audio library
    Match/    Match simulation: grid, path, enemies, towers, soldiers, waves, placement
    UI/       uGUI built at runtime: menu, HUD, party bar, captain widget, run screens, cheats
  Editor/     Scene builders, Windows and Android builds, bond tools, and the Babel > Data windows and Excel workbook
  Data/       Tunable data assets (captains, enemies, soldier classes, match setup) and the audio mixer
  Resources/  The audio library, loaded at runtime
  Prefabs/    Enemy, tower and soldier prefabs
  Art/        Placeholder sprites and tiles
  Audio/      Music and sound effect clips
  Scenes/     MainMenu (title, settings) and Match (setup, world map, matches and results all run in it)
  Tests/      EditMode tests
```

### Conventions

- **Data first:** all tunable numbers live in ScriptableObjects under `Assets/Data` and are edited in the Inspector, the **Babel → Data** windows or Excel.
- **Events for play style:** gameplay raises events such as leaks, soldier deaths, selling and moving through `GameEvents`. Later systems like approval and requests listen to them instead of being wired into the gameplay code.
- **Run vs match:** `RunState` holds what persists across a run. Each match scene only owns its own simulation.

## Contributing workflow

Each milestone is split into one branch per task (`m3/05-…`, `m3/06-…`), each based on the one before it, so every task stays traceable in history. Branches are merged into `main` in order with a merge commit per branch.

### Checking compilation without Unity

`Tools/compile_check.py` compiles a checkout or worktree with the compiler Unity ships, using the settings Unity cached the last time it compiled this project. It's useful for branches that aren't open in the editor.

```
py -I Tools/compile_check.py <checkout root>
```

EditMode tests live in `Assets/Tests/EditMode` and run from Unity's Test Runner.

### Art and stats

- `py -I Tools/pixel_art.py [preview.png]` regenerates the placeholder pixel art in `Assets/Art/Sprites` from the character grids in the script (optionally writing an enlarged preview sheet). The scene builder assigns art by name to any soldier class or enemy without a sprite, and the shared generic sprite to captain templates. **Babel → Build Data Assets Only** rebuilds the data assets (templates included) without regenerating the scene.
- Enemy walk frames (`enemy_*_walk1.png`, `enemy_*_walk2.png`) are hand-made in Aseprite and `pixel_art.py` doesn't write them, so redo them if an enemy's palette or body changes.
- Every match logs per-wave stats to the Console and writes a CSV to `stats/` under Unity's persistent data path (`%USERPROFILE%/AppData/LocalLow/DefaultCompany/ProjectBabel` on Windows). AI Run batches report to `Reports/` instead.
- Approval and bond rank live on each rolled captain and are saved with the run, so a new run starts every captain at 0.

## Continuous integration

`.github/workflows/tests.yml` runs the edit-mode tests (`Assets/Tests/EditMode`) with GameCI on every push to `main` and on pull requests. The Unity version is read from `ProjectSettings/ProjectVersion.txt`.

The job needs a Unity licence, so until the secrets exist it skips with a notice instead of failing. Add these under the repository's **Settings → Secrets and variables → Actions → New repository secret**:

| Secret | Value |
|---|---|
| `UNITY_LICENSE` | Contents of the activated `.ulf` licence file (Personal licences: follow the GameCI "Activation" guide at game.ci/docs/github/activation to produce it) |
| `UNITY_EMAIL` | Email of the Unity account |
| `UNITY_PASSWORD` | Password of that Unity account |
