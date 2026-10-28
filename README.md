# Project Babel

*Working title.* A roguelite tower defense in 2D pixel art where every tower is a character.

You claim cities on a world map, defend them in wave-based matches, and use the intel from each match to track down the enemy's main base. Each tower is run by a **captain** whose approval of your decisions shapes how well their tower performs.

## The game at a glance

- **Runs:** about 30 matches on a tactics-style overworld. Global health lasts the whole run; enemies that leak through reduce it, and at zero the run is lost.
- **Matches:** 10 waves, 5–10 minutes. Place up to 5 towers, set each captain's Gold draw rate, choose soldier mixes, and relocate towers mid-match for Gold. The final match has double the waves and a very slow boss.
- **Captains:** 12 unique captains (8 per run), each with an element (Water, Fire, Earth, Air), a role (Attacker, Buffer, Healer), one special ability, and 3 likes and 3 dislikes.
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

Later phases cover the world map and full run, the remaining content, polish, and release prep.

## Getting started

**Requirements:** Unity **6000.5.5f1** with the Universal Render Pipeline (2D). Packages are listed in `Packages/manifest.json` and include Tilemap, Aseprite import and the Input System.

1. Clone the repository and open the folder in Unity Hub with editor version 6000.5.5f1.
2. Open `Assets/Scenes/Match.unity` and press **Play**.
3. To regenerate the grey-box scene, use the menu **Babel → Build Grey-Box Match Scene**. Existing data assets and prefabs are kept, so your tuning survives a rebuild.

### Controls

| Input | Action |
|---|---|
| Left-click a free tile | Place the next captain's tower |
| `Tab` | Choose which captain to place next |
| `Q` | Fire the selected tower's special when its meter is full |
| `M` | Move the selected tower (then click a free tile) |
| Left-click a tower | Select it |
| `1` / `2` / `3` | Set the selected tower's Gold draw: Low / Normal / High |
| `X` | Sell the selected tower (returns its reserve) |
| `Space` | Start the next wave |
| `Esc` or right-click | Deselect |

## Project layout

```
Assets/
  Scripts/
    Core/     Run-wide state (health, Gold) and the GameEvents event bus
    Data/     ScriptableObject definitions: captains, soldier classes, enemies, matches
    Match/    Match simulation: grid, path, enemies, towers, soldiers, waves, placement
    UI/       Grey-box HUD (temporary)
  Editor/     Scene builder for the grey-box match
  Data/       Tunable data assets (captains, enemies, soldier classes, match setup)
  Prefabs/    Enemy, tower and soldier prefabs
  Art/        Placeholder sprites and tiles
  Scenes/     Match scene
```

### Conventions

- **Data first:** all tunable numbers live in ScriptableObjects under `Assets/Data` and are edited in the Inspector.
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
