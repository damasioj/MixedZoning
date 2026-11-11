# Mixed Zoning (Cities: Skylines 1)

Adds 7 paintable zone types. Each one grows a configurable mix of vanilla buildings
(residential, commercial, office, industrial). This is a new implementation; it contains no
code or art from Klyte45's *Custom Zone Mixer*, whose license doesn't allow redistribution.

## Status

| Piece | State |
|---|---|
| Project, mod entry, Harmony bootstrap | Done, builds |
| Zone-mix config (XML + options screen) | Done, untested in game |
| Growth patch (`ZoneBlock.SimulationStep` + `CheckBlock`) | Written; IL checked offline |
| Zoning check patch (`Building.CheckZoning`) | Written; IL checked offline |
| Painting guard (`ZoneTool.UsedZone`) | Written |
| Zoning-panel buttons (generated checkerboard icons) | Written; targets checked offline |
| Terrain overlay (two-colour checkerboards, drawn as diamonds) | Written; logic checked offline |
| In-game test | Not done |

### How growth works

Only three `ZoneBlock.GetZone` call sites are swapped for helpers in `Zones/MixedZoneResolver.cs`:

1. **`ZoneBlock.SimulationStep`**: the origin cell's mixed zone becomes one vanilla zone,
   picked by demand (district-aware). If both densities of a category are allowed, high density
   or low density is picked at random (50/50) by default. In Mod Settings you can switch on a demand
   threshold instead: high density once that category's demand reaches it. The rest of the method
   only sees 0–7.
2. **`ZoneBlock.CheckBlock`**: lot sizing joins only cells painted with the same mixed zone.
3. **`Building.CheckZoning`**: a mixed cell counts as matching if its mix allows the
   building's zone, so grown buildings aren't abandoned.

Each transpiler checks it found exactly one `GetZone` call. If the game changes, all patches are
removed together and the game runs unmodified (logged as `[MixedZoning] ... failed`).

### UI and overlay

- **Buttons** ("Mixed Zone 1" to "Mixed Zone 7") are appended after the game fills the zoning panel, *not* added to its
  zone list: the game feeds that list into milestone/unlock lookups sized for 8 zones. Clicks and
  tooltip hovers on our buttons are handled by the mod for the same reason.
- **Editing in game**: right-click a mixed-zone button to open its settings window (the six
  building types; the low/high density setting is in Mod Settings). Left-clicking any zoning button closes it. Changes
  apply and save immediately.
- **Fixed look, independent of configuration.** Each zone has its own checkerboard of two colours
  (one colour per zone square) and a numbered icon, and none of it changes when you reconfigure a zone:

  | Zone | Colours |
  |---|---|
  | 1 | light purple (solid) |
  | 2 | light green + dark green |
  | 3 | light blue (commercial) + dark blue |
  | 4 | light green + light blue (commercial) |
  | 5 | dark green + light blue (office) |
  | 6 | light blue (office) + yellow |
  | 7 | light purple + yellow |

  On the map, mixed-zone squares are drawn turned 45° (as diamonds), so they stand apart from
  vanilla zones even though they use the same colours. The checkerboard is laid out on the zone
  squares themselves, so it follows the road and lines up across neighbouring blocks. Each
  square's exact position comes from the zone block that owns it, because the overlay data stores
  it rounded.

  The terrain zone shader has only 8 colour slots (`_ZoneColor0..7`). The mod borrows slot 1
  ("Distant", which the game never stores in zone cells) for the light purple while a city is loaded
  and restores it on unload. The other colours are the vanilla zone colours because the shader has
  no other slots. They identify the zone and say nothing about what it grows.
- A mix never grows a zone type the player hasn't unlocked yet. A mix button is disabled until
  at least one of its zone types is unlocked.

### Compatibility with other zoning mods

- **81 Tiles 2** (algernon): works. It only rewrites zone-grid constants (150 → 270) in the
  same methods, and Harmony applies its transpilers and ours together. The overlay reads the zone
  grid size from the game, so it works with 81 Tiles' larger grid.
- **Building Themes**: works. It replaces `ZoneBlock.SimulationStep` with its own copy
  (`ZoneBlockDetour.SimulationStep`) when a city loads; `Patches/BuildingThemesCompat.cs`
  applies the same origin-zone swap to that copy. If the copy ever changes shape, only that
  patch is skipped (logged) and the rest of the mod keeps working.
- **Original 81 Tiles** (EightyOne, superseded by 81 Tiles 2): replaces zone growth with its own
  code, so mixed zones won't grow with it. Use 81 Tiles 2.

## Build

Requires the .NET SDK (any recent version) and the game installed.

```bash
cd src
dotnet build -c Release \
  -p:CSManagedDir="$HOME/Library/Application Support/Steam/steamapps/common/Cities_Skylines/Cities.app/Contents/Resources/Data/Managed"
```

Add `-p:Deploy=true` to copy `MixedZoning.dll` and `CitiesHarmony.API.dll` into
`~/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods/MixedZoning`.

Dependency for players: the **Harmony** mod (Workshop 2040656402) must be subscribed.

## How it stores data

Zone cells use 4 bits, so values 0–15 exist. Vanilla uses 0–7 and 15; this mod uses 8–14.
The game saves cells as raw bit masks, so no custom save data is needed. Removing the mod
leaves those cells as unknown zone values; a cleanup option to convert them back is planned.

## Save-crash note (old mod)

The old mod crashed when saving. Reading the game's code shows two rules this mod follows:
- `ZoneManager.m_goodAreaFound` / `m_zoneNotUsed` are fixed 8-entry arrays, and the save
  code writes exactly 8. The old mod resized `m_zoneNotUsed` to 15.
- `ZoneBlock.SimulationStep` indexes `m_goodAreaFound` by zone, so mixed zones must be
  turned into a vanilla zone before that point.

The exact cause of the old crash isn't confirmed; it needs `~/Library/Logs/Unity/Player.log`
from a crashing session.

## License

MIT, see [LICENSE](LICENSE). Cities: Skylines and its DLLs belong to Colossal Order / Paradox and are not included.
