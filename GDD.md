# Project Babel: Game Design Document

*Working title. Reflects the game as built (after M12: the Castle, locations, generated captains, owned soldiers, CI tests), not the original plan. Numbers come from the data assets and rules classes and are tunable; where this document and the data disagree, the data wins. Terms follow `GLOSSARY.md`; the design decisions behind sections 4–6 are in `docs/adr/0001` and `0002`.*

## 1. Overview

**Genre:** roguelite tower defense with a tactics-style world map.
**Platform:** PC (Windows), plus Steam Deck (Linux) and Android Fire-tablet builds. Unity 6 (URP 2D), pixel art.
**Players:** single player. **Session:** a match is 5–10 minutes; a run is 7–11 matches, depending on the location.

**Pitch:** From your Castle you send a party of captains to a location, claim its cities on a world map and defend them in wave-based matches. Every tower is a character, a **captain**, whose approval of your decisions shapes how well they fight. The intel from each match reveals roads toward the location's enemy base, where a slow, very tough boss waits. Win, and the next location opens; lose, and the captains on the field are gone for good.

**Pillars**
1. **Towers are people.** Captains have likes, dislikes, personalities, rivals, requests and bonds that last for the whole profile. Managing them matters as much as placing them.
2. **Decisions, not micromanagement.** Soldiers fight on their own. The player places and moves towers, answers requests, fires specials and spends Gold.
3. **A run is a route.** The world map is a branching graph; where you go, and what you see of it, is a strategic choice.
4. **Data first.** Every tunable number lives in ScriptableObjects, editable in the Inspector, the **Babel → Data** windows or one Excel workbook.

## 2. Core loop

1. **First play:** pick 3 starting captains out of 4 rolled ones (one Knight, Mage, Archer and Healer), each with 2 random soldiers.
2. **Castle:** recruit captains and soldiers, equip soldiers, buy gifts, then set out with a party of up to 5 to an unlocked location.
3. **World map:** choose the next stop. Every road ends at the location's enemy base.
4. **Match:** place towers, run waves, answer requests, fire specials.
5. **Result:** Gold, XP and approval are banked, health returns (+2), and you pick 1 of 3 soldiers (or skip). Back to the map.
6. The run ends when the enemy base falls (win: the next location unlocks) or global health reaches 0 (loss). Either way you return to the Castle.

## 3. The Castle and the profile

### 3.1 The Castle
The Castle is the home between runs, drawn as a walled town with a building for each screen:

| Building | Does |
|---|---|
| Great Hall | The roster as cards: each captain's level, bond, stats, special and soldier slots; equip soldiers, release captains |
| Barracks | Every soldier owned and who leads them |
| Tavern | Captain Recruit tab: 3 rolled captains, 400 Gold each; not while the roster is full |
| Training Yard | Soldier Recruit tab: 6 soldiers, 100 / 160 / 250 / 360 / 550 Gold by rarity |
| Market | Gifts for the next run (40–100 Gold) |
| Storehouse | The gifts packed for the next run |
| Gate | Pick the party (up to 5), then a location on the **Tower** |

Shop stock comes from a seed that changes when a party sets out, so it refreshes after each run.

### 3.2 Locations
Locations are floors of the Tower, unlocked in order: winning a run opens the floor above. Each has its own world map, elements, enemy mix and boss.

| Location | Matches | Elements | Enemies | Boss |
|---|---|---|---|---|
| Greenmarch | 7 | Grass, Fire | Raiders and grunts, no siege engines | Bandit Chief |
| Saltmarsh | 9 | Water, Ice | Slingers and bombards | Tide Warden |
| Cinderpeak | 11 | Fire, Ice | Raiders, brutes, bombards; more gates sooner | Warlord |

### 3.3 The profile
Everything that lasts between runs: the roster (up to 12 captains), the soldier inventory, Gold (300 to start), unlocked locations, and each captain's level, XP, approval and bond rank. Global health resets every run.

- **Lost run:** costs 20% of Gold and every captain placed in the losing match, with their equipped soldiers. Party captains who were never deployed go home safely.
- **Abandoned run** (from the world map, confirmed with a second press): costs 25% of Gold and one soldier the player picks; the party is kept.
- **Release:** the player removes a captain for good; their soldiers go back to the inventory.
- **Walkout:** an unhappy captain leaves the roster for good (see 5.3).

## 4. The match

### 4.1 Setup
A city is played on one of several layouts (Riverside, Hillfort, Market Square, Long Road, Crossroads, Harbor Split, Twin Gates, Three Gates). Layouts define the grid, the enemy road(s), gates, the castle and props. Two-gate layouts appear from a location's double-gate layer (layer 1–2), three-gate ones from its triple-gate layer (4–5). Wave groups alternate between gates unless a group names one; the boss takes a random gate.

### 4.2 Flow
- Up to **5 towers**. Click a free tile to place the next captain's tower; its soldiers' home tiles are previewed.
- Towers can be **moved** (`M`) free until the first wave starts, then for 25 Gold; **sold** (`X`) for their reserve.
- Waves are started with `Space` or auto-start after 15 s. A normal match is **10 waves**; the enemy base plays its waves twice.
- Speed x1 / x2, pause, and a HUD with global health, Gold, wave progress and a preview of the next wave.
- A match is won when every wave is cleared. Enemies reaching the castle cost **leak damage** to global health.

### 4.3 Towers and soldiers
A tower does not shoot. Its captain's equipped **soldiers** leave the tower, attack the closest enemy, then return. Each captain has 4 soldier slots; a soldier is an owned item, equipped to at most one captain, and takes that captain's element.

| Class | Damage type | Attack | Respawn cost | Health | Damage × | Range | Adds to captain |
|---|---|---|---|---|---|---|---|
| Knight | Melee | Hit with knockback | 8 | 50 | 1.25 | 0.4 | Defense |
| Archer | Range | Projectile | 4 | 15 | 1.45 | 2 | Range |
| Mage | Magic | Splash | 12 | 18 | 1.4 | 1.5 | Power |
| Rogue | Melee | Hit | 4 | 26 | 1.2 | 0.4 | Speed |

- **Rarity:** Common, Uncommon, Rare, Epic, Legendary (rolled 50 / 25 / 14 / 8 / 3%). Each tier's stat bonus is ×1, ×1.25, ×1.5, ×1.75, ×2 of Common's. Bonus traits are deferred.
- Acting is free; every **respawn** costs Gold from the tower's **reserve**.
- **Knights** have a 25% chance to knock the target back along the road.
- Soldiers gain **+10% power and health per level** (max level 10); XP comes from kills plus capped support XP (60 per wave).
- **Defense:** a captain's flat damage reduction for the tower and its soldiers (minimum 1 damage taken). It grows with level, and equipped soldiers add to it.
- Each class picks its own home tile near the tower.

### 4.4 Economy
- One **Gold** purse, kept in the profile and carried between runs. In a match it is earned from kills, wave clears (1 + 1 per wave) and a 40 Gold win bonus. Rewards grow with run depth (+4% per match) and elites pay double.
- Towers **draw** from the purse into their own reserve at Low (×0.5), Normal or High (×2) rates, set through requests. Approval makes Gold go further. Unspent reserves return on a win.
- Level raises Gold costs linearly (+10% per level).

### 4.5 Enemies and matchups
| Enemy | Health | Gold | Damage type | Element | Leaks for |
|---|---|---|---|---|---|
| Grunt | 30 | 1 | Melee | Grass | 1 |
| Raider | 35 | 1 | Melee | Fire | 1 |
| Slinger | 25 | 1 | Range | Water | 1 |
| Brute | 120 | 2 | Melee | Ice | 2 |
| Bombard | 90 | 2 | Magic | Fire | 2 |
| Bandit Chief (Greenmarch boss) | 2600 | 30 | Melee | Water | 999 |
| Tide Warden (Saltmarsh boss) | 4500 | 45 | Melee | Water | 999 |
| Warlord (Cinderpeak boss) | 4500 | 60 | Melee | Water | 999 |

**Matchups** (`DamageTypeRules`, `ElementRules`): damage types run Melee > Range > Magic > Melee, elements Water > Fire > Ice > Grass > Water (Water↔Ice and Fire↔Grass are neutral). Each matchup is ×1.5 with the advantage, ×0.75 at a disadvantage, and the two multiply (×0.5625 to ×2.25). They apply both ways: soldiers hit with their class's damage type and their captain's element; an ability uses its captain's damage type and element; enemies hit soldiers and towers with their own. Burn damage takes no matchup.

Enemy health (+12% per match) and count (+8% per match) grow through the run. Elites play two layers tougher.

## 5. Captains

### 5.1 Generated captains
Captains are rolled from **captain templates**, one per role, then kept in the roster. A roll sets name, portrait, element, damage type, stats within the template's ranges, one special from the role's pool, 3 likes and 3 dislikes, and a personality trait.

| Role | Usual damage type | Special pool |
|---|---|---|
| Knight | Melee | Undertow, Rockfall, Stone Ward |
| Mage | Magic | Scorched Earth, Kindle |
| Rogue | Melee | Gale Strike, Flow State |
| Archer | Range | Flare Volley, Crosswind |
| Healer | Magic | Tidal Mend, Mend the Land, Second Wind |

Role doesn't fix damage type: a **special class** such as a Magic Knight can be rolled. A captain buffs only if its special does; there are no Attacker or Buffer roles. Captains level to 10 (XP needed: 25 × level × (level − 1)).

### 5.2 Elements and specials
**Elements and statuses:** Water slows (×0.5 speed), Fire burns (30% of power per second), Ice freezes (the enemy stands still and holds its attacks, 1.5 s), Grass tangles it in vines (a stun: attacks take 2× as long). Other statuses last 3 s. Bond rank adds +5% status chance per rank.

**Specials:**
| Special | Effect |
|---|---|
| Flare Volley | Burst area damage |
| Scorched Earth | Damage zone on the path |
| Undertow | Area damage, pulls enemies back |
| Rockfall | Single-target stun hit |
| Crosswind | Pushes back, applies Freeze |
| Gale Strike | Long-range pierce |
| Kindle | All towers +25% damage this wave |
| Flow State | All towers act 25% faster this wave |
| Stone Ward | All towers take 30% less damage this wave |
| Tidal Mend | Heals all 35% |
| Mend the Land | Heals all 25%, +15% defense this wave |
| Second Wind | Heals all 20%, +15% act speed this wave |

**Special meter:** fills mainly from the captain's soldiers fighting (damage dealt, hits on afflicted enemies, knockbacks, kills, elite and boss kills; capped at 60% per wave) plus a slow trickle over time. The player fires it by clicking the captain in the party bar. There are no passive auras. Firing triggers hit-stop and camera shake.

### 5.3 Relationships
Each captain tracks **approval** (−100 to 100) and a **bond rank** D → C (40) → B (65) → A (85) → S (100), both kept in the profile.

- **Judging:** play-style tags are judged once per wave: Leaks, Casualties, Selling, Ability, Flawless, Spending, Kills, Boss. Soldier mix is judged at match end (Knights / Archers / Mages / Rogues). A like earns +1.5, a dislike costs 3 (Casualties 1.5), winning gives +5.
- **Gain falloff:** gains shrink 15% per rank. A captain can rise at most one rank per run.
- **Disapproved mix:** if a captain dislikes more of its soldiers than it likes, approval gains are halved.
- **Gifts:** bought at the Market, packed in the Storehouse and given during a run: +15 if liked, +5 if neutral, −10 if disliked.
- **Walkouts:** a captain whose approval falls 20 below its best this run leaves the roster for good at the end of a match; their soldiers go back to the inventory.
- **Rank rewards:** extra status chance, Gold going further, faster special charge.
- **Rivals:** at most one pair of party captains per run, weighted toward clashing traits. Granting one rival's request costs the other 1 approval (2 for a Reinforce covering the other, or a MoreDraw while the other's reserve is under half).

### 5.4 Requests
Captains interrupt the match with a request; the match pauses until you answer **Yes** (grants it and pays approval) or **No** (always costs approval). Unanswered at the countdown counts as a refusal; open when the wave ends is dropped without penalty. One request is up at a time, up to 2 per wave and often none.

| Request | Trigger | Effect if granted |
|---|---|---|
| MoreDraw | A soldier fell and the reserve can't pay to respawn it | Raises the Gold draw |
| LowerDraw | Reserve sat full for a while | Sets draw to Low |
| Relocate | Nothing in range for a while | Moves the captain 10% closer to the gate, free |
| UseSpecial | Elite/boss arrived, a leak, or meter just filled | Captain fires it |
| Reinforce | Another captain fell | Captain moves to cover the fallen tower, free |
| Retreat | A Cautious captain is hurt | Captain falls back from the gate, free |

- **Cons:** each card shows what a Yes costs in this match (Gold left, captains drawing, elites to come, road left uncovered, reserve, rival approval). A request is **bad** when its cons outweigh its pros.
- **Lock-in:** a granted draw rate stays until the wave ends, and a moved captain can't be moved again until then.
- **Personality traits:** Steady (30%; only sound requests), Glory-seeker (fires the special at weak enemies while an elite or boss is to come), Greedy (High draw when Gold is short), Cautious (Retreat when hurt). Non-Steady traits make bad requests on purpose.
- **Proven right:** refuse a bad request and finish the wave with no leaks and no fallen towers, and the captain refunds the penalty plus 1 approval.

## 6. The run

### 6.1 Start and party
At the Castle Gate, pick up to 5 captains from the roster, then a location. Only 5 towers can be on the field in a match. During a run, captains join only through events; if that would make a 13th captain, the player releases the newcomer or an existing captain.

### 6.2 World map
A layered graph of 2–4 stops per layer converging on the enemy base. You see only roads your **intel** has revealed; each stop reveals the next 2 layers beyond it. Stop odds are set per location. Stop types:
- **City:** a normal match.
- **Elite city:** two layers tougher, double Gold, and the soldier reward rolls each rarity twice and keeps the better.
- **Rest:** restores 30% of max health.
- **Event:** a short scene with 2–3 choices that change Gold, health, party XP, approval or the roster (Ambush, Abandoned Armory, Field Surgeon, Refugees, Tavern, Training Grounds).
Rests and events replace a match: no fight, but no XP or Gold either. There are no world-map shops; gifts come from the Castle Market.

### 6.3 Soldier reward
After each won match that doesn't end the run, 3 random soldiers are offered; pick one into the inventory or skip. An open offer is saved with the run.

### 6.4 Health
Global health is shared across the whole run (starts at 20). Leaks reduce it, wins return 2, rests return 30%. At 0 the run is lost.

### 6.5 The enemy base and boss
The final match plays its waves twice. The location's **boss** walks on with the first wave through a random gate, very slow and very tough, and doesn't hold up the waves behind it. The base falls only when every wave is cleared and the boss is dead; if it reaches the end of the road, the run is lost.

### 6.6 Persistence
Two saves. The **profile save** holds the roster, inventory, Gold and unlocked locations; old saves are migrated forward. The **run save** holds the location, world-map state, health, party, rivals and any open soldier offer; quitting mid-run resumes on that world map. Main menu: **Resume**, **New game** (asks before wiping a profile), **Settings**, **Exit**.

## 7. Interface and controls

Left-click places the next captain's tower or selects a tower (opens its captain widget); the party bar at the bottom fires specials or chooses who to place next. Keys: `M` move, `X` sell, `Space` next wave, `Esc`/right-click cancel, `F1`/`F2` cheat and debug panels (dev builds only), `1/2/3` set draw rate in the debug panel.

**HUD:** slim top bar (health, Gold, wave progress, next-wave preview, speed, pause), contextual controls hint, a large request card, floating damage numbers, approval and level-up toasts, health bars, threat alerts, and a camera that pans and focuses on events.

**Combat feedback** (each toggleable in Settings → Gameplay): damage numbers, matchup (Strong! / Resist) labels, hit flash, stun stars, knockback effects, death burst, status icons, health-bar extras, leak edge flash, elite/boss banner.

**Menus:** Settings has Graphics, Audio and Gameplay tabs; HUD size is adjustable. Lists in the Castle can be filtered.

**Input:** mouse and keyboard, or a gamepad for the whole game (custom `UIFocus` scopes), switching to whichever was used last. All actions are in `GameInput`.

## 8. Art and audio

- **Art:** 32px PixelLab sprites for captains, soldiers, enemies and props; 16px tiles and cursors; enemy walk cycles hand-made in Aseprite. Tilemap city layouts; the Castle is a painted town. (`Tools/pixel_art.py` would overwrite the PixelLab art, so don't rerun it on those sprites.)
- **Audio:** key-based `AudioManager` with a library (channel, clips, volume, pitch variance, cooldown) and a mixer with Music, Sfx and Ui groups. Music crossfades between menu and match. All 18 keys have clips, 12 of them Kenney CC0 placeholders.

## 9. Tools and tuning

- **Excel workbook:** all gameplay data exports and imports as one `.xlsx` (not the Audio Library). Import updates only; Undo reverts it.
- **AI Run:** the AI picks a party and route, places towers, answers requests and fires specials over 1–50 runs at up to 32× speed, writing a Markdown report and per-wave CSV to `Reports/`. It uses its own saves.
- **Stats:** every match writes a per-wave CSV for balance review.
- **Tests:** pure rules live in `Babel.Core.Pure` with edit-mode tests; a play-mode smoke test plays one wave. GitHub Actions runs the edit-mode tests.
- **Builds:** Windows, Steam Deck (Linux) and Android (Fire tablet) from the **Babel** menu.

## 10. Status

Milestones M1–M12 are done: grey-box match, soldiers, roles and elements, relationships, vertical slice, world map, map stops and boss, then the Castle and locations, generated captains, owned soldiers with rarity, personality traits and rivals, the run controller split, and CI tests. Also done: main menu and settings, party bar, combat feedback, Excel tuning, AI Run, economy tuning, Android build, multi-gate layouts, damage-type and element matchups, walk cycles, the audio system, gamepad support and the PixelLab art pass.

**Open / next**
- More locations above Cinderpeak (the Tower shows sealed floors).
- Soldier bonus traits (deferred to polish).
- Level perks (deferred): milestone unlocks or perk choices on level-up.
- Sound design beyond the placeholder clips.
- Balance passes using AI Run reports, especially reserves now that acting is free.
- Gamepad on a physical pad and Steam Deck, not yet verified.
- Polish and release prep.

*Known doc drift:* `maxPartySize` in the match data is still 8 and the world map shows party as x/8, while the Castle caps the party at 5.
