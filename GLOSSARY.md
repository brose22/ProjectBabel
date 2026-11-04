# Glossary

Terms the code, issues and docs use. Where a term replaces an older one, the old one is listed under **Avoid**.

## Progression

**Castle** — The home base between runs. Holds the shop, the inventory, soldier equipping and captain release. The player picks the party here before leaving for a location.

**Location** — One of the places the player can explore from the Castle. Each has its own world map, enemy mix and elements. Locations unlock in order: winning one opens the next. There are 3 to start with.

**Run** — All the matches at one location, from leaving the Castle until the player wins or loses there. Global health resets at the start of each run.
_Avoid:_ using "run" for the whole game; that is the **profile**.

**Profile** — Everything that lasts between runs: the roster, the soldier inventory, Gold, unlocked locations, and each captain's level and bond rank.

**Roster** — Every captain the player owns, up to 12. Captains join through the Captain Recruit tab and events, and leave by release, by walkout, or by being lost with the party when a run is lost. The shop won't sell a captain while the roster is full. When an event would add a 13th captain during a run, the player releases either the newcomer or an existing captain.

**Party** — The captains taken on a run, up to 5, picked from the roster at the Castle. A captain who leaves (walkout or release) is taken out of the party, not kept as an inactive entry.

**Starting captains** — The 3 captains picked the first time the game is played, out of 4 rolled captains: one Knight, one Mage, one Archer and one Healer. Each comes with 2 random soldiers.

**Release** — Removing a captain from the roster for good, by the player's choice. Their soldiers go back to the inventory.

**Walkout** — An unhappy captain leaving the roster and the party for good.

## Captains

**Captain** — A tower the player places. Generated from a **captain template** when it is recruited, then kept in the roster.

**Captain template** — The data a captain is rolled from: role, a range for each stat, an ability pool, a pool of likes and dislikes, and pools of names and portraits.
_Avoid:_ treating `CaptainData` assets as fixed, named characters.

**Role** — A captain's class: Knight, Mage, Rogue, Archer or Healer. It does not fix the damage type. Rogue is both a captain role and a soldier class.
_Avoid:_ Attacker, Buffer, Thief. A captain is a buffer only because its ability buffs.

**Special class** — A captain whose rolled damage type is not the usual one for its role, such as a Magic Knight. The usual types are Knight Melee, Mage Magic, Rogue Melee, Archer Range and Healer Magic.

**Defense** — A flat reduction to the damage a captain's tower or soldiers take. It grows with level, and the captain's equipped soldiers add to it.

## Soldiers

**Soldier** — A unit the player owns as one item in the inventory. Equipped in one of a captain's 4 slots, it fights on the battlefield and adds a stat to its captain. A soldier is equipped to at most one captain at a time.

**Soldier class** — The fixed archetype of a soldier: Knight, Archer, Mage or Rogue. The class fixes the soldier's damage type and which captain stat it adds:

| Class | Damage type | Adds to captain |
|---|---|---|
| Knight | Melee | Defense |
| Archer | Range | Range |
| Mage | Magic | Power |
| Rogue | Melee | Speed |

**Rarity** — A soldier's tier: Common (white), Uncommon (green), Rare (purple), Epic (red) or Legendary (gold). Each tier's stat bonus is 25% more than the tier below's, measured against Common (×1, ×1.25, ×1.5, ×1.75, ×2). Bonus traits are deferred to polish.

**Soldier Recruit tab**, **Captain Recruit tab** — The two Castle shop tabs that sell soldiers and captains. World-map shops don't have them.

## Combat

**Damage type** — Melee, Range or Magic. Melee beats Range, Range beats Magic, Magic beats Melee. A soldier attacks with its class's damage type; a captain's ability uses the captain's damage type; enemies have one too.
_Avoid:_ armor type (`ArmorType`), which damage type replaces.

**Element** — Water, Fire, Ice or Grass, in a closed cycle: Water beats Fire, Fire beats Ice, Ice beats Grass, Grass beats Water. Water and Ice are neutral to each other, as are Fire and Grass. A soldier takes the element of the captain it is equipped to.
_Avoid:_ Earth, Air.

**Advantage** — Damage ×1.5 when the attacker beats the target's damage type or element, ×0.75 when the target beats it. The damage-type and element multipliers stack, so the result ranges from ×0.5625 to ×2.25.
