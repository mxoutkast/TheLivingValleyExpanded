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

1. Install/update **The Living Valley**:
   - https://github.com/ai2-claw/stardew-living-rpg-mod
2. Install/update **Stardew Valley Expanded**:
   - https://www.nexusmods.com/stardewvalley/mods/3753
   - https://github.com/FlashShifter/StardewValleyExpanded
3. Build this project and copy its output folder into your `Mods` directory.

## Build

Set `SMAPI_PATH` to your Stardew Valley install directory, then run:

```powershell
dotnet build
```

Example (PowerShell):

```powershell
$env:SMAPI_PATH = "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley"
dotnet build
```

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
- edit this file to tune personality, speech style, relationship ties, and location context.

Community localization:
- Drop locale overlays into `i18n/sve-lore.<locale>.json` (for example `i18n/sve-lore.es.json`).
- Overlays are partial and merged over base lore.
- Locale fallback is automatic (`pt-br` -> `pt` -> base).
- Full contributor instructions: `i18n/README.md`.
