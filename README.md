# Project Babel

*Working title.* A roguelite tower defense in 2D pixel art where every tower is a character.

You claim cities on a world map, defend them in wave-based matches, and use the intel from each match to track down the enemy's main base. Each tower is run by a **captain** whose approval of your decisions shapes how well their tower performs.

## The game at a glance

- **Runs:** about 30 matches on a tactics-style overworld. Global health lasts the whole run; enemies that leak through reduce it, and at zero the run is lost.
- **Matches:** 10 waves, 5–10 minutes. Place up to 5 towers, set each captain's Gold draw rate, choose soldier mixes, and relocate towers mid-match for Gold. The final match has double the waves and a very slow boss.
- **Captains:** 12 unique captains (8 per run), each with an element (Water, Fire, Earth, Air), a role (Attacker, Buffer, Healer), one special ability, and 3 likes and 3 dislikes. The role decides what the special does: an Attacker's hits enemies, a Healer's restores a share of health to every tower and soldier, and a Buffer's boosts every tower and soldier for the rest of the wave.
- **Relationships:** captains judge your play style (leaks, casualties, selling, ability use, stable or moving placement) and soldier mix. Their approval and bond rank (D → S) carry over between runs and boost their tower.
- **Soldiers:** Knights, Archers, Mages and Rogues leave their tower to attack the closest enemy, then return. Every act and respawn costs Gold.
- **Economy:** one shared Gold pool, earned from kills and carried between matches. Towers draw from it into their own reserve at Low, Normal or High rates.

## Status

Early development. One developer at 20 hours a week, with a vertical slice targeted at month 12.

| Milestone | Scope | State |
|---|---|---|
| M1: grey-box match | Path, waves, global health and Gold, tower placement, Attacker captains, basic soldiers, placeholder HUD | Done (merged) |
| M2: soldier system | Four soldier classes (Knight knockback), soldier mix, tower damage/disable/restore, relocation | Done (merged) |
| M3: roles and elements | Status effects, special abilities and hit-stop, Buffer and Healer auras, levels and XP, enemy types and weaknesses | Done (merged) |
| M4: relationships | Approval (saved between runs), like/dislike judging, requests, bond ranks D–S, walkouts | Done (merged) |
| M5: vertical slice | City map, Cinder / Marin / Ember, run setup and results, uGUI HUD, generated pixel art, stats log and tuning | Done (merged) |
| M6: world map and run | Branching world map to the enemy base, intel reveals roads, 4 city layouts, scaling difficulty, pick 3 and recruit up to 8, run saved between matches (short 7-match test run) | Done (merged) |
| M7: map stops and the boss | Elite cities, rests, shops (approval gifts) and events on the world map, health back after wins and at rests, the enemy base's very slow boss | Done (merged) |

Next up: the remaining content (toward the ~30-match run), polish, and release prep.

### How a run plays (M7)

1. **Setup:** pick 3 captains from the roster of 12.
2. **World map:** pick a stop on the next layer. Every road leads to the enemy base in the last layer. You only see the roads your intel has revealed; each stop reveals the next 2 layers beyond it. Besides plain cities, the middle layers hold:
   - **Elite cities:** play 2 layers tougher, pay double Gold for kills, and offer one extra captain to recruit.
   - **Rests:** restore 30% of max health.
   - **Shops:** buy approval gifts for party captains (75 Gold for +10 approval).
   - **Events:** a short scene with 2–3 choices that change Gold, health, the party's XP or approval.

   Rests, shops and events take the place of a match: no fight, but no match XP or Gold either.
3. **Match:** the city is played on one of 4 layouts (Riverside, Hillfort, Market Square, Long Road). Enemies get tougher and more numerous the further into the run you are. The enemy base plays its waves twice, and its boss, the Warlord, walks on with the first wave: very slow and very tough. The base falls only once every wave is cleared and the Warlord is dead; if it reaches the end of the road, the run is lost.
4. **After a win:** 2 health comes back, and up to 3 captains offer to join; recruit one or decline (the party holds 8, not counting walkouts). Health, Gold, XP and walkouts carry over.
5. Taking the enemy base wins the run; reaching 0 health loses it.

The run is saved to `run.json` in the persistent data path whenever you're on the map or have just won a city. **Resume run** on the main menu picks it up on the world map; **Start run** asks first, then deletes it. **Abandon run** on the map deletes it too, and **Main menu** on the setup, map and results screens goes back to the menu without losing it. Run length, layer sizes, intel depth, node odds, elite tuning, the boss and difficulty growth are on the match data asset under **World map**; healing, shop prices and the event pool sit under **Healing**, **Shop** and **Events**. Events are `EventData` assets in `Assets/Data/Events`.

## Getting started

**Requirements:** Unity **6000.5.5f1** with the Universal Render Pipeline (2D). Packages are listed in `Packages/manifest.json` and include Tilemap, Aseprite import and the Input System.

1. Clone the repository and open the folder in Unity Hub with editor version 6000.5.5f1.
2. Press **Play**. The game starts on the main menu (`Assets/Scenes/MainMenu.unity`) whichever scene is open, while **Babel → Play From Main Menu** is ticked; untick it to play the open scene directly, such as `Assets/Scenes/Match.unity`.
3. To regenerate the grey-box scene, use the menu **Babel → Build Grey-Box Match Scene**. Existing data assets and prefabs are kept, so your tuning survives a rebuild. **Babel → Build Main Menu Scene** regenerates the menu scene and keeps it first in Build Settings.
4. **Babel → Build Windows Player** builds a standalone player into `Builds/Windows`, starting on the main menu.
5. **Babel → Build Android APK (Fire Tablet)** builds a sideloadable APK into `Builds/Android` for an Amazon Fire tablet (needs Android Build Support from Unity Hub). It sets the Android player to IL2CPP, 32- and 64-bit ARM and landscape only. Copy the APK to the tablet with *Apps from Unknown Sources* allowed, or run `adb install -r Builds/Android/ProjectBabel.apk`.

### Tuning data in Excel

**Export** and **Import** on the toolbar of each **Babel → Data** window, or **Babel → Data → Export to Excel...** and **Import from Excel...**, move all the gameplay data through one `.xlsx` workbook. Every button covers every data type, whichever window it is in.

- **Sheets:** Captains, Abilities, Enemies, Soldiers, Elements, Events, Matches and Layouts have one row per asset, keyed by the file name in the **Asset** column. Lists of records get their own sheets (Events Choices, Matches Waves, Matches Waves Groups, Layouts Props) with one row per record, tied to the asset by **Asset** and the **#** columns. Positions must run 1, 2, 3 within a list.
- **Cell formats:** enums are written by name, colours as `r, g, b, a`, cells as `x, y`, and lists of plain values in one cell separated by `; `. A data asset reference is its name; a sprite or tile reference is its path, with `#Name` for a sprite inside a file. `None` or an empty cell clears a reference.
- **Import** updates existing assets and never creates, renames or deletes them. Adding or removing rows on a list sheet grows or shrinks that list. A bad value, unknown row or unknown column is skipped and left as it was, with a Console warning naming the sheet and row; each changed asset is logged, and **Undo** reverts the whole import. Rename an asset in Unity, not in the workbook: a changed name reads as an unknown row.

### Main menu

- **Start run:** a new run, from the captain pick. If a run is saved, you're asked first, since it is deleted.
- **Resume run:** continues the saved run on the world map (greyed out without one).
- **Settings:** resolution, window mode (fullscreen, borderless, windowed), VSync, frame-rate cap (used while VSync is off), quality level and master volume. Changes apply at once, are kept in PlayerPrefs and are applied again at every launch. The editor's Game view ignores resolution and window mode.
- **Exit:** quits the game (stops Play mode in the editor).

### Controls

| Input | Action |
|---|---|
| Left-click a free tile | Place the next captain's tower |
| Click a captain in the party bar (bottom) | On the field: fire their special when its meter is full. Not yet placed: make them the next to place |
| `M` | Move the selected tower (then click a free tile) |
| Left-click a tower | Select it |
| `1` / `2` / `3` | Set the selected tower's Gold draw: Low / Normal / High |
| `X` | Sell the selected tower (returns its reserve) |
| `Space` | Start the next wave |
| `Esc` or right-click | Deselect |
| `F1` | Cheat window, editor and development builds only (win the match, kill the boss, +500 Gold, full health, reveal the map) |

## Project layout

```
Assets/
  Scripts/
    Core/     Run-wide state (health, Gold) and the GameEvents event bus
    Data/     ScriptableObject definitions: captains, soldier classes, enemies, matches, city layouts
    Match/    Match simulation: grid, path, enemies, towers, soldiers, waves, placement
    UI/       Grey-box HUD (temporary)
  Editor/     Scene builders (grey-box match, main menu) and the Windows build
  Data/       Tunable data assets (captains, enemies, soldier classes, match setup)
  Prefabs/    Enemy, tower and soldier prefabs
  Art/        Placeholder sprites and tiles
  Scenes/     MainMenu (title, settings) and Match (setup, world map, matches and results all run in it)
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

- `py -I Tools/pixel_art.py [preview.png]` regenerates the placeholder pixel art in `Assets/Art/Sprites` from the character grids in the script (optionally writing an enlarged preview sheet). The scene builder assigns art by name to any captain, soldier class or enemy without a sprite.
- Every match logs per-wave stats to the Console and writes a CSV to `stats/` under Unity's persistent data path (`%USERPROFILE%/AppData/LocalLow/DefaultCompany/ProjectBabel` on Windows).
