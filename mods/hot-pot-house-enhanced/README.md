# Hot Pot House Enhanced

Version 2.4.20.

- Consolidates Hot Pot House side-dish labels into one appliance-level label on stacks, dishwashers and washbasins, for both clean and dirty dishes.

- When Colorblind+ is installed, suppresses its per-dish text for Hot Pot House side dishes without affecting the appliance-level HPH label or CountUp counters.

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
