# 04 · Module: Data (RoyalSiege.Data)

**Status: CODE + ASSETS COMPLETE — 16 Jul (see TASKS.md)**

## Responsibilities
ScriptableObject class definitions + authored asset instances under `Assets/TowerDefense/Data/`. Full catalog in `02_ARCHITECTURE.md`; values ONLY from `01_DESIGN_CURRENT.md`.

## Assets to author
- `GameConfig.asset`, `EconomyConfig.asset`
- Cards: `Card_Cannon`, `Card_Tesla`, `Card_XBow`, `Card_Arrows`, `Card_Fireball`, `Card_Freeze`, `Card_Lightning` (+ their SpellEffect assets)
- Enemies: `Enemy_Goblin`, `Enemy_Mage`, `Enemy_Ogre`, `Enemy_OgreWarlord`
- `Deck_Default.asset` (7 cards)
- `Waves_Level1.asset` (12 waves per design table) — Day 2: `Waves_Level2` variant if second environment gets its own timeline
- `OnValidate` sanity checks where cheap (e.g., warn if a spell damage ≥ its counter-target HP — the no-one-shot invariant)

## Acceptance
- All numbers in the 01 tables exist as assets; a `BalanceInvariantTests` (editor test or menu item) asserts: Arrows < Goblin HP, Fireball < Mage HP, Lightning < Mage HP, Freeze duration < its cooldown, X-Bow range + deploy radius < map radius.

## Notes / changes
- (log changes here)
