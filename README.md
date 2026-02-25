# The Living Valley Expanded Compatibility Patch

This is a SMAPI compatibility mod for:
- The Living Valley (`mx146323.StardewLivingRPG`)
- Stardew Valley Expanded (SVE)

It extends The Living Valley's internal NPC targeting so SVE villagers are recognized by:
- NPC roster expansion
- social-visit quest target validation
- town-event NPC inference
- compact prompt canon NPC list
- lore-aware Player2 prompt injection (`SVE_NPC_LORE` + `SVE_LOCATION_LORE`)

## Install

1. Download and install **The Living Valley** from NexusMods:
   - https://www.nexusmods.com/stardewvalley/mods/42597
2. Download and install **Stardew Valley Expanded** from NexusMods:
   - https://www.nexusmods.com/stardewvalley/mods/3753
3. Download and install **The Living Valley Expanded Compatibility Patch** from NexusMods:
   - https://www.nexusmods.com/stardewvalley/mods/42699
4. Ensure each mod folder is inside your Stardew Valley `Mods` directory.
5. Launch the game with SMAPI.

## Config

`config.json`:

- `EnableSveLoreInjection`:
  when `true`, injects SVE lore snippets into Player2 `game_state_info`.
- `LoreLocaleOverride`:
  optional locale override (for example `es`, `pt-br`). Empty = use current game locale.
- `IncludeFriendshipNpcsWhenSVEInstalled`:
  when `true`, friendship NPC names from the loaded save are auto-added.
- `AdditionalNpcNamesCsv`:
  comma-separated extra NPC names to force-add.

Lore data source:
- `assets/sve-lore.json`
- `assets/sve-lore.override.json` (optional, loaded after locale overlays)
- edit `assets/sve-lore.override.json` to tune personality, speech style, relationship ties, and location context without changing base lore files.
- sample template: `assets/sve-lore.override.example.json`

Override quick start:
1. Copy `assets/sve-lore.override.example.json` to `assets/sve-lore.override.json`.
2. Change only the NPC/location fields you want to override.
3. Restart the game.

Community localization:
- Drop locale overlays into `i18n/sve-lore.<locale>.json` (for example `i18n/sve-lore.es.json`).
- Overlays are partial and merged over base lore.
- Locale fallback is automatic (`pt-br` -> `pt` -> base).
- Full contributor instructions: `i18n/README.md`.
