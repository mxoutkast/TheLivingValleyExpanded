# Lore Localization Guide

This mod supports community-contributed lore localization overlays.

Base lore:
- `assets/sve-lore.json` (English source of truth)

Localization overlay files:
- `i18n/sve-lore.<locale>.json`
- examples:
  - `i18n/sve-lore.es.json`
  - `i18n/sve-lore.pt-br.json`
  - `i18n/sve-lore.fr.json`

Fallback behavior:
- If game locale is `pt-BR`, the mod loads:
  1. `i18n/sve-lore.pt-br.json`
  2. `i18n/sve-lore.pt.json`
- Then falls back to base `assets/sve-lore.json` for any missing fields.
- If present, `assets/sve-lore.override.json` is applied last so local edits always win.

Overlay format:
- Same shape as `assets/sve-lore.json`.
- You can include only the NPCs/locations and fields you want to override.
- Empty fields are ignored.

Minimal example:

```json
{
  "npcs": {
    "Sophia": {
      "Speech": "Tono suave, tímido y afectuoso."
    },
    "Lance": {
      "Persona": "Valiente, calmado y estratégico."
    }
  },
  "locations": {
    "blue_moon_vineyard": "Lugar personal de Sophia, íntimo y emotivo."
  }
}
```

Optional config:
- `config.json` -> `LoreLocaleOverride`
- set to a locale string (for example `es`, `pt-br`) to force that overlay regardless of game locale.
