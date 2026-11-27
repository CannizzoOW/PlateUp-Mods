# Hot Pot House Enhanced

Version 2.5.0.

- Adds the Automatic Pot Washer as a Pot Washer upgrade. It automatically cleans dirty family pots, supports grabber input/output, and cleans 15% faster than the original soaking-sink baseline.

- Removes the experimental table-persistent family-pot delivery lock. Hot Pot House's own shared-pot system remains responsible for normalising each active customer group's family-pot order, while Enhanced continues to collapse duplicate visuals.

- Moves shared-pot order deduplication into Enhanced itself, so identical unfinished hot-pot orders at the same table display only one pot without requiring the Colorblind+ compatibility mod.

- Suppresses all native Hot Pot House item, provider and order labels when the main Colorblind+ mod is installed. Colorblind+ labels and CountUp counters remain visible.

- Removes Enhanced's obsolete plate-stack TextMesh counter so CountUp compatibility is the sole count display, eliminating the small glyph artefact.

- Replaces repeated appliance-wide label scans with a constant-time creation patch, preventing the hitch when HPH side dishes are picked up or placed.

- Consolidates Hot Pot House side-dish labels into one appliance-level label on stacks, dishwashers and washbasins, for both clean and dirty dishes.

- When Colorblind+ is installed, keeps its item text and suppresses Hot Pot House's duplicate side-dish text. CountUp counters are unaffected.

- Strips label producers from the SoupsPlus Chicken Noodle Soup template itself, removing the remaining `ChiNo` label before guest bowls are cloned.

- Maps clean HPH side dishes to vanilla clean plates for yellow dishwasher LEDs, while dirty side dishes retain the purple dirty-plate LEDs.

- Strips inherited HPH ItemNameTag/NameTag and Colorblind+/TMP labels from generated guest-bowl contents, preventing labels such as `ChiNo` from appearing on bowls.

- Builds served hot-pot/dip contents once and clones the prepared bowl for additional guests, substantially reducing delivery hitches.

- Claims one visual owner per completed table order and normalises seat height before deduplication, guaranteeing at most one bowl per guest.

- Creates guest soup bowls only once per completed shared order, preventing stacked bowl layers and continuous lag while food remains on the table.

- Uses vanilla counters for regular Hot Pot House providers, refrigerators for cold providers, and the vanilla plate stack for side dishes.
- Displays the correct Hot Pot House ingredients.
- Uses red-tinted Soups Plus Miso visuals for Mala Base.
- Displays one filled soup bowl per guest after serving a shared hot pot.

The compiled DLL is available in [`dist`](dist/).
