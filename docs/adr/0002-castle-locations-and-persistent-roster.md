# 0002 — Castle, locations and a persistent roster

Status: accepted (2026-11-03)

## Context

The main menu used to start a run, which was one long chain of about 30 matches on one world map. Captain levels reset every run. A captain who walked out sat out only the rest of that run.

## Decision

**New flow:** Main menu → Castle → pick a location → that location's world map → matches.

- A **run** is all the matches at one location.
- There are 3 locations to start, unlocked in order. Each has its own world map, enemy mix and elements.
- The Castle starts with the shop (a Soldier Recruit tab, a Captain Recruit tab, and gifts; world-map shops don't have the recruit tabs), the inventory (Captains and Soldiers tabs), equipping soldiers to captains, and releasing captains.
- The party is up to 5 captains, picked at the Castle before leaving.

**The roster lasts between runs.**

- The starting captains are picked once, the first time the game is played (see ADR 0001).
- The roster holds up to 12 captains. New captains come from the Captain Recruit tab and from events. The player can release a captain, and the released captain's soldiers go back to the inventory.
- With 12 captains in the roster, the shop won't sell another. If an event adds a 13th during a run, the player releases either the newcomer or an existing captain.
- Captains keep their level and bond rank between runs. Soldiers and Gold also carry over. Global health resets each run.
- A walkout removes the captain from the roster for good.
- Losing a run costs 20% of the player's Gold and loses every captain in the party, along with their equipped soldiers.
- A walkout or release sends the captain's equipped soldiers back to the inventory.

**Two saves.**

- **Profile save:** roster, soldier inventory, Gold, unlocked locations.
- **Run save:** current location, world-map state, global health, current party. Quitting mid-run resumes on that world map.

## Consequences

- `RunState` splits into profile state and run state, and `RunSave` into two files. Existing saves won't load.
- The main menu leads to a new Castle scene instead of the world map.
- `WorldMap` generation takes the location as input (enemy mix, elements).
- Captain XP, approval and bond rank move from per-run to per-profile.
- The "sits out the rest of the run" walkout logic becomes permanent removal.
