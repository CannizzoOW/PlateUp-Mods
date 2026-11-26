# Hot Pot House - Colorblind+ Compatibility

Version 1.7.4.

Each generated bowl and bowl holder is processed once for text suppression,
removing the remaining per-update traversal while food stays on the table.

Hot-pot delivery no longer scans every transform in the restaurant. Soup-bowl
text cleanup is restricted to the single affected order view, removing the
severe delivery hitch in large restaurants.

Dirty text is now detected directly from Colorblind+'s rendered label. The
menu controls that label, while Hot Pot House's smaller duplicate Dirty label
remains suppressed in both menu states.

The Dirty item text option now covers vanilla dirty plates and pots as well as
Hot Pot House dishes, including items held inside dishwashers and washbasins.

Served soup-bowl text and dirty side-dish text now have separate live menu
controls. Soup-bowl text is also suppressed after Enhanced creates the guest
bowls, covering labels generated after the normal item-view update.

Dirty Hot Pot House side dishes are now recognised inside dishwashers and
washbasins as well as on counters. The single dirty-text option controls both
Colorblind+ text and Hot Pot House's original smaller label.

CountUp! provider counters are now split into separate controls for ingredient
providers and other providers such as plate, dish and pot racks. Live toggles
restore only text objects hidden by this compatibility mod, preventing hidden
Colorblind+ internal labels such as `Me` and `O` from appearing across the map.

Disabled Hot Pot House labels are now suppressed continuously so pooled views
cannot restore old or duplicate text. Colorblind+'s `Dirty` label on Hot Pot
House side dishes has its own option and is hidden by default.
Generated guest soup bowls never show item labels, and hidden HPH label roots
are disabled so pooled customer/order objects cannot drag the text with them.
Hidden provider markers are now disabled before they can create text, and
already-created provider labels are deduplicated.
Dirty Hot Pot House side dishes are detected by their GDO or prefab identity,
and every Colorblind+ text child on that item view is suppressed.
The same option also suppresses Hot Pot House's separate `Dirty` ItemNameTag.
The PreferenceSystem manager is registered only once, preventing duplicate
`Text Display Controls` entries after KitchenLib system injection.
All settings have visible descriptive labels. Hot Pot House settings are split
into Items, Providers and Orders. Changes refresh active HPH, Colorblind+ and
CountUp! views immediately; no PlateUp restart is required.

Includes separate PreferenceSystem submenus for Hot Pot House, Colorblind+, and CountUp!. Hot Pot House controls cover regular items, pot/broth items, ingredient providers, pot racks, the side-dish rack, and shared orders. Colorblind+ and CountUp! controls affect the text owned by those mods. Changes apply immediately.

- Hides duplicate Hot Pot House labels while Colorblind+ is active.
- Preserves Colorblind+ labels.
- Collapses duplicate unfinished shared-pot order displays.

The compiled DLL is available in [`dist`](dist/).
