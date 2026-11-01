# Project Babel: Game Design Document

*Working title. Reflects the game as built (after the audio system), not the original plan. Numbers come from the data assets and rules classes and are tunable; where this document and the data disagree, the data wins.*

## 1. Overview

**Genre:** roguelite tower defense with a tactics-style world map.
**Platform:** PC (Windows), plus Steam Deck (Linux) and Android Fire-tablet builds. Unity 6 (URP 2D), pixel art.
**Players:** single player. **Session:** a match is 5–10 minutes; a full run is about 30 matches (the current test run is 7).

**Pitch:** You claim cities on a world map and defend them in wave-based matches. Every tower is a character, a **captain**, whose approval of your decisions shapes how well they fight. The intel from each match reveals roads toward the enemy's main base, where a very slow, very tough Warlord waits.

**Pillars**
1. **Towers are people.** Captains have likes, dislikes, requests and bonds that last between runs. Managing them matters as much as placing them.
2. **Decisions, not micromanagement.** Soldiers fight on their own. The player places and moves towers, answers requests, fires specials and spends a shared Gold pool.
3. **A run is a route.** The world map is a branching graph; where you go, and what you see of it, is a strategic choice.
4. **Data first.** Every tunable number lives in ScriptableObjects, editable in the Inspector, the **Babel → Data** windows or one Excel workbook.

## 2. Core loop

1. **Setup:** pick 3 captains from the 12 (8 are in play in a run; see Roster).
2. **World map:** choose the next stop. Every road ends at the enemy base.
3. **Match:** place towers, run waves, answer requests, fire specials.
4. **Result:** Gold, XP and approval are banked, health returns (+2), and you may recruit a new captain. Back to the map.
5. The run ends when the enemy base falls (win) or global health reaches 0 (loss). Captain approval and bond ranks persist across runs.

## 3. The match

### 3.1 Setup
A city is played on one of several layouts (Riverside, Hillfort, Market Square, Long Road, Twin Gates, Three Gates; Crossroads and Harbor Split also exist as assets). Layouts define the grid, the enemy road(s), gates, the castle and props. Twin Gates has two gates and roads to one castle from layer 3; Three Gates has three, castle central, from layer 6. Wave groups alternate between gates unless a group names one; the boss takes a random gate.

### 3.2 Flow
- Up to **5 towers**. Click a free tile to place the next captain's tower; its soldiers' home tiles are previewed.
- Towers can be **moved** (`M`) free until the first wave starts, then for 25 Gold; **sold** (`X`) for their reserve.
- Waves are started with `Space` or auto-start after 15 s. A normal match is **10 waves**; the enemy base plays its waves twice.
- Speed x1 / x2, pause, and a HUD with global health, Gold, wave progress and a preview of the next wave.
- A match is won when every wave is cleared. Enemies reaching the castle cost **leak damage** to global health.

### 3.3 Towers and soldiers
A tower does not shoot. Its captain's **soldiers** leave the tower, attack the closest enemy, then return.

| Class | Style | Act / respawn cost | Health | Damage × | Range |
|---|---|---|---|---|---|
| Knight | Melee | 2 / 8 | 50 | 1.1 | 0.4 |
| Archer | Ranged | 2 / 4 | 12 | 1.2 | 2 |
| Mage | Splash | 3 / 12 | 18 | 2.0 | 1.5 |
| Rogue | Melee | 1 / 4 | 20 | 1.0 | 0.4 |

- A tower holds 2 soldiers at bond rank D, 3 at C/B, 4 at A/S. The player can switch a soldier slot's class at any time.
- Every attack run ("act") and every respawn costs Gold from the tower's **reserve**.
- **Knights** have a 25% chance to knock the target back along the road.
- Soldiers gain **+10% power and health per level** (max level 10); XP comes from kills plus capped support XP.
- Each class picks its own home tile near the tower.

### 3.4 Economy
- One shared **Gold pool** per run, earned from kills, wave clears (5 + 2 per wave) and a 50 Gold win bonus. Rewards scale up with run depth (+7% per match) and elites pay double.
- Towers **draw** from the pool into their own reserve at Low (×0.5), Normal or High (×2) rates, set through requests. Approval makes Gold go further. Unspent reserves return to the pool on a win.
- Level raises Gold costs linearly (+10% per level).

### 3.5 Enemies and armor
| Enemy | Health | Gold | Armor | Notes |
|---|---|---|---|---|
| Grunt | 30 | 7 | Unarmored | Basic melee |
| Raider | 35 | 9 | Unarmored | Basic melee |
| Slinger | 25 | 8 | Swift | Ranged (2 tiles) |
| Brute | 120 | 15 | Armored | Heavy melee, leaks for 2 |
| Bombard | 90 | 15 | Armored | Long ranged attack (2.75), leaks for 2 |
| Warlord (boss) | 6000 | 200 | Armored | Very slow, leaks for 999 |

**Armor counters** (damage multiplier by soldier class, Knight / Archer / Mage / Rogue):
- Unarmored: 1 / 1 / 1 / 1
- Armored: 0.65 / 1 / 0.65 / 1.4 (Rogues cut it open)
- Swift: 1 / 0.65 / 1.4 / 0.65 (Mages' splash catches it)

Because soldier classes can be changed live, the counter is actionable mid-match. Elements no longer change damage; they bring statuses only. Enemy health (+12% per match) and count (+8% per match) grow through the run. Elites play two layers tougher.

## 4. Captains

### 4.1 Element, role and special
Each captain has an **element**, a **role**, one **special** with a meter, and 3 likes and 3 dislikes.

**Elements and statuses:** Water slows (×0.5 speed), Fire burns (30% of power per second), Earth stuns (attacks take 2× as long), Air makes the enemy dizzy (walks the wrong way, 1.5 s). Statuses last 3 s. Bond rank adds +5% status chance per rank.

**Roles** decide what the special does:
- **Attacker:** an offensive effect (area burst, zone, pierce, stun hit).
- **Healer:** restores a share of health to every tower and soldier.
- **Buffer:** boosts every tower and soldier for the rest of the wave.

**Special meter:** fills mainly from the captain's soldiers fighting (damage dealt, hits on afflicted enemies, knockbacks, kills, capped per wave) plus a slow trickle over time. The player fires it by clicking the captain in the party bar. There are no passive auras. Firing triggers hit-stop and camera shake.

### 4.2 Roster

| Captain | Element | Role | Special |
|---|---|---|---|
| Cinder | Fire | Attacker | Flare Volley: burst area damage |
| Pyre | Fire | Attacker | Scorched Earth: damage zone on the path |
| Brine | Water | Attacker | Undertow: area damage, pulls enemies back |
| Slate | Earth | Attacker | Rockfall: single-target stun hit |
| Skye | Air | Attacker | Crosswind: pushes back, applies Dizzy |
| Zephyr | Air | Attacker | Gale Strike: long-range pierce |
| Ember | Fire | Buffer | Kindle: all towers +25% damage this wave |
| Rill | Water | Buffer | Flow State: all towers act 25% faster this wave |
| Boulder | Earth | Buffer | Stone Ward: all towers take 30% less damage this wave |
| Marin | Water | Healer | Tidal Mend: heals all 35% |
| Terra | Earth | Healer | Mend the Land: heals all 25%, +15% defense this wave |
| Gust | Air | Healer | Second Wind: heals all 20%, +15% act speed this wave |

### 4.3 Relationships
Each captain tracks **approval** (−100 to 100), saved between runs, and a **bond rank** D → C (40) → B (65) → A (85) → S (100).

- **Judging:** play-style tags are judged once per wave: Leaks, Casualties, Selling, Ability, Flawless, Spending, Kills, Boss. Soldier mix is judged at match end (Knights / Archers / Mages / Rogues). A like earns +1.5, a dislike costs 3 (Casualties 1.5), winning gives +5.
- **Gain falloff:** gains shrink 15% per rank. A captain can rise at most one rank per run.
- **Disapproved mix:** if a captain dislikes more of its soldiers than it likes, approval gains are halved.
- **Walkouts:** a captain whose approval falls 20 below its best this run leaves at the end of a match and sits out the rest of the run.
- **Rank rewards:** more soldiers per tower, extra status chance, Gold going further, faster special charge.

### 4.4 Requests
Captains interrupt the match with a request; the match pauses until you answer **Yes** (grants it and pays approval) or **No** (always costs approval). Unanswered at the countdown counts as a refusal; open when the wave ends is dropped without penalty. One request is up at a time, up to 2 per wave and often none.

| Request | Trigger | Effect if granted |
|---|---|---|
| MoreDraw | A soldier fell and the reserve can't pay to respawn it | Raises the Gold draw |
| LowerDraw | Reserve sat full for a while | Sets draw to Low |
| Relocate | Nothing in range for a while | Moves the captain 10% closer to the gate, free |
| NewMix | Levelled up and a liked class isn't in every slot | Switches a slot |
| UseSpecial | Elite/boss arrived, a leak, or meter just filled | Captain fires it |
| Reinforce | Another captain fell | Captain moves to cover the fallen tower, free |

## 5. The run

### 5.1 Start and party
Pick 3 captains. After each win up to 3 captains offer to join (elites offer one extra); recruit one or decline. The party holds up to 8, not counting walkouts. Only 5 towers can be on the field in a match.

### 5.2 World map
A layered graph of 2–3 stops per layer converging on the enemy base. You see only roads your **intel** has revealed; each stop reveals the next 2 layers beyond it. Stop types:
- **City:** a normal match.
- **Elite city:** two layers tougher, double Gold from kills, one extra recruit option.
- **Rest:** restores 30% of max health.
- **Shop:** buy approval gifts for party captains (75 Gold for +10 approval); a stock of 6 from 10 gift types (Honey Cake, Ember Charm, Iron Whetstone, Sea Shell, Wind Chime and others), each matched to captain likes.
- **Event:** a short scene with 2–3 choices that change Gold, health, party XP or approval (Ambush, Abandoned Armory, Field Surgeon, Refugees, Tavern, Training Grounds).
Rests, shops and events replace a match: no fight, but no XP or Gold either.

### 5.3 Health
Global health is shared across the whole run (starts at 20). Leaks reduce it, wins return 2, rests return 30%. At 0 the run is lost.

### 5.4 The enemy base and boss
The final match plays its waves twice. The **Warlord** (6000 health) walks on with the first wave, very slow and very tough, through a random gate. The base falls only when every wave is cleared and the Warlord is dead; if he reaches the end of the road, the run is lost.

### 5.5 Persistence
The run is saved to `run.json` whenever you are on the map or have just won a city; approval and bonds are saved separately and persist across runs. **Resume run** continues; **Start run** asks before deleting a save.

## 6. Interface and controls

Left-click places the next captain's tower or selects a tower (opens its captain widget); the party bar at the bottom fires specials or chooses who to place next. Keys: `M` move, `X` sell, `Space` next wave, `Esc`/right-click cancel, `F1`/`F2` cheat and debug panels (dev builds only), `1/2/3` set draw rate in the debug panel.

**HUD:** slim top bar (health, Gold, wave progress, next-wave preview, speed, pause), contextual controls hint, a large request card, floating damage numbers, approval and level-up toasts, health bars, threat alerts, and a camera that pans and focuses on events.

**Combat feedback** (each toggleable in Settings → Gameplay): damage numbers, armor labels, hit flash, stun stars, dizzy wobble, knockback effects, death burst, status icons, health-bar extras, leak edge flash, elite/boss banner.

**Menus:** Start run, Resume run, AI Run, Settings (Graphics, Audio, Gameplay tabs), Exit. HUD size is adjustable.

## 7. Art and audio

- **Art:** placeholder-to-final pixel art generated by `Tools/pixel_art.py` for captains, soldiers and enemies; enemy walk cycles are hand-made in Aseprite. Tilemap city layouts.
- **Audio:** key-based `AudioManager` with a library (channel, clips, volume, pitch variance, cooldown) and a mixer with Music, Sfx and Ui groups. Music crossfades between menu and match. 6 of 18 keys have clips so far (both music tracks, UI click, enemy killed, tower placed, ability used).

## 8. Tools and tuning

- **Excel workbook:** all gameplay data exports and imports as one `.xlsx` (not the Audio Library). Import updates only; Undo reverts it.
- **AI Run:** the AI picks a party and route, places towers, answers requests and fires specials over 1–50 runs at up to 8× speed, writing a Markdown report and per-wave CSV to `Reports/`. It uses its own saves.
- **Stats:** every match writes a per-wave CSV for balance review.
- **Builds:** Windows, Steam Deck (Linux) and Android (Fire tablet) from the **Babel** menu.

## 9. Status

Milestones M1–M7 are done (grey-box match, soldiers, roles and elements, relationships, vertical slice, world map, map stops and boss), plus main menu and settings, party bar, combat feedback, Excel tuning, AI Run, economy tuning, Android build, multi-gate layouts, armor types, walk cycles and the audio system.

**Open / next**
- Content toward the full ~30-match run (currently 7).
- Remaining 12 audio keys and sound design.
- Level perks (deferred): milestone unlocks or perk choices on level-up.
- Carry-over between runs beyond approval (deferred).
- Balance passes using AI Run reports.
- Polish and release prep.

*Known doc drift:* the README still mentions "weakness hits" and element weakness; the code now uses armor types (Section 3.5). Layout assets Crossroads and Harbor Split exist but are not listed in the README's six.
