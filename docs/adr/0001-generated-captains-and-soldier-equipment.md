# 0001 — Generated captains and soldiers as equipment

Status: accepted (2026-11-03)

## Context

Captains were 12 handmade `CaptainData` assets, each with a fixed element (Water, Fire, Earth, Air) and role (Attacker, Buffer, Healer). Soldiers were not owned: each captain had 4 slots filled with a soldier class, and the soldiers cost Gold every time they acted. Damage counters came from enemy armor (`ArmorType`) against the soldier class. Elements only applied status effects.

## Decision

**Captains are generated from templates.** A captain template holds a role, a range for each stat, an ability pool, a pool of likes and dislikes, and pools of names and portraits. A recruited captain is rolled once, then kept in the roster with its level and bond rank (see ADR 0002).

- Roles are Knight, Mage, Rogue, Archer and Healer. Attacker and Buffer are no longer roles; a captain buffs only if its ability does.
- Role does not fix damage type. The usual types are Knight Melee, Mage Magic, Rogue Melee, Archer Range and Healer Magic, but a special class such as a Magic Knight can be rolled.
- Elements are Water, Fire, Ice and Grass. Earth and Air are removed.
- Captains gain a **Defense** stat. It grows with level and is a flat reduction to the damage taken by the tower and by its soldiers.

**Soldiers are owned items that still fight.** Each soldier is one item in the inventory, equipped in one of a captain's 4 slots, and at most one captain has it at a time. Equipped soldiers walk out and fight as they do now.

- The classes stay fixed: Knight, Archer, Mage, Rogue. The class fixes the damage type (Knight Melee, Archer Range, Mage Magic, Rogue Melee) and the captain stat the soldier adds (Knight Defense, Archer Range, Mage Power, Rogue Speed).
- Melee, Range and Magic decide how a soldier attacks: a hit with knockback, a projectile, or splash damage. This replaces `AttackStyle`.
- A soldier takes the element of the captain it is equipped to, and the element changes if it moves to another captain.
- Soldiers have five rarity tiers: Common (white), Uncommon (green), Rare (purple), Epic (red), Legendary (gold). Each tier's stat bonus is 25% more than the tier below's, measured against Common: ×1, ×1.25, ×1.5, ×1.75, ×2.
- Bonus traits (a random extra trait on higher tiers) are deferred. They are not implemented now; revisit during polish.
- Acting no longer costs Gold. Respawning still does.
- Soldiers are sold on the Castle shop's Soldier Recruit tab and listed on the inventory's Soldiers tab.

**Damage uses two stacking matchups.**

- Damage type: Melee > Range > Magic > Melee.
- Element: Water > Fire > Ice > Grass > Water. Water↔Ice and Fire↔Grass are neutral.
- An advantage is ×1.5 and a disadvantage ×0.75. The two multipliers multiply together.
- Soldiers attack with their own damage type and element. A captain's ability uses the captain's damage type and element.
- Enemies get a damage type and an element in place of `ArmorType`, and the matchups apply in both directions.

**Starting captains.** The first time the game is played, the player is offered 4 rolled captains, one each of Knight, Mage, Archer and Healer, and picks 3. Each comes with 2 random soldiers, which are added to the inventory.

## Consequences

- `CaptainData` changes from a captain to a template. The roster stores generated captains (rolled stats, name, portrait, element, damage type, level, bond rank), so the save keeps captains by id, not by asset name.
- Per-captain likes, gifts and dialogue are drawn from the template pools when the captain is rolled.
- `ArmorRules` and `ArmorType` are replaced by damage-type and element rules. Enemy assets need a damage type and element.
- The per-act Gold cost goes away. The tower's Gold reserve is now used only for respawning and should be rebalanced.
- Status effects tied to Earth and Air need new homes on Ice and Grass.
- The AI party picker, debug panels and reports that rely on the old roles and elements need updating.

## Revisit during polish

- Bonus traits: which traits are in the pool and which tiers can roll one.
