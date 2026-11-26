# Cannizzo ModPatcher

Current release: 1.0.

GitHub-backed local installer while Workshop releases await moderation.

The patcher installs:

- Hot Pot House Enhanced 2.4.12
- Hot Pot House Colorblind+ Compatibility 1.2.1
- Hot Pot House CountUp! Compatibility 1.0.0

At startup it reads `mods.json` from GitHub and displays every available mod. It finds Steam libraries through the Windows registry, `libraryfolders.vdf`, and PlateUp's `appmanifest_1599600.acf`. The `Mods` directory is created automatically when missing. Selected DLLs are downloaded from GitHub and SHA-256 verified before installation.

Required mods still need to be installed through Steam Workshop: HarmonyX, KitchenLib, Hot Pot House, Colorblind+, and Soups Plus.

Close PlateUp before installing or removing the local mods.
