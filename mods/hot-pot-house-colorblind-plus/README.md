# Hot Pot House - Colorblind+ Compatibility

Version 1.3.2.

Disabled Hot Pot House labels are now suppressed continuously so pooled views
cannot restore old or duplicate text. Colorblind+'s `Dirty` label on Hot Pot
House side dishes has its own option and is hidden by default.
Generated guest soup bowls never show item labels, and hidden HPH label roots
are disabled so pooled customer/order objects cannot drag the text with them.
Hidden provider markers are now disabled before they can create text, and
already-created provider labels are deduplicated.

Includes separate PreferenceSystem submenus for Hot Pot House, Colorblind+, and CountUp!. Hot Pot House controls cover regular items, pot/broth items, ingredient providers, pot racks, the side-dish rack, and shared orders. Colorblind+ and CountUp! controls affect the text owned by those mods. Changes apply after restarting PlateUp.

- Hides duplicate Hot Pot House labels while Colorblind+ is active.
- Preserves Colorblind+ labels.
- Collapses duplicate unfinished shared-pot order displays.

The compiled DLL is available in [`dist`](dist/).
