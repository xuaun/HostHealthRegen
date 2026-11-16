# HostHealthRegen

Passive health regeneration for R.E.P.O., run by the host.

Every 20 seconds each living player gets 1 health back, plus 1 for every health upgrade they own. Every number can be changed in the config.

## Who needs it

**Only the host.** The host heals everyone through the game's own heal, so friends without the mod regenerate too, and the host's config applies to the whole room.

Installing it on a client does nothing while someone else hosts. It also works in singleplayer.

## How it works

- Regenerates during levels only. The shop and the truck lobby can be turned on in the config; the arena never regenerates.
- Dead players are not healed. Regeneration keeps running for the others when the host dies.
- Health is saved and synced the same way as a health pack.

## Configuration

`BepInEx/config/Xuaun.HostHealthRegen.cfg`. Changes apply without restarting, and the options show up in REPOConfig if you have it.

| Option | Default | What it does |
|---|---|---|
| `Enabled` | `true` | Turns the regeneration on or off. |
| `IntervalSeconds` | `20` | Seconds between ticks (1 to 300). |
| `BaseAmount` | `1` | Health per tick, before the upgrade bonus (0 to 50). |
| `BonusPerHealthUpgrade` | `1` | Extra health per tick for each health upgrade the player owns (0 to 10). `0` gives everyone the same amount. |
| `DelayAfterDamageSeconds` | `0` | A player who took damage is skipped until this many seconds have passed. `0` turns it off. |
| `SlowerWithMorePlayers` | `false` | Multiplies the interval by the number of living players. |
| `MaxHealthPercent` | `100` | Regeneration stops at this percentage of max health. |
| `RegenInShop` | `false` | Also regenerate in the shop. |
| `RegenInTruckLobby` | `false` | Also regenerate in the truck lobby between levels. |
| `HealEffect` | `false` | Plays the game's heal sound and screen effect on every tick. |
| `DebugLogs` | `false` | Logs every tick to the BepInEx console. |

## Installation

Install with a mod manager, or drop `HostHealthRegen.dll` into `BepInEx/plugins`.

## Credits

Inspired by [Health Regen](https://thunderstore.io/c/repo/p/RyBird/Health_Regen/) by RyBird and [Regen](https://thunderstore.io/c/repo/p/modx/Regen/) by modx. Written from scratch; no code from either mod.
