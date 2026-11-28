# Cannizzo ModPatcher

Current release: 2.0.1.

GitHub-backed local installer while Workshop releases await moderation.

The patcher installs:

- Hot Pot House Enhanced 2.5.0
- Hot Pot House CountUp! Compatibility 1.0.0
- More Upgrades 2.1.0
- Restart Day - Soft Opening 1.0.0

At startup it reads `mods.json` from GitHub and displays every available mod. It finds Steam libraries through the Windows registry, `libraryfolders.vdf`, and PlateUp's `appmanifest_1599600.acf`. The `Mods` directory is created automatically when missing. Selected DLLs are downloaded from GitHub and SHA-256 verified before installation.

Version 1.2 adds a dark WebView interface, verified launcher self-updates, Update All, SHA-256 repair, automatic backups and rollback, Workshop dependency checks, mod details and release notes, live PlateUp status, direct game launch, and quick access to the Mods folder. It is distributed as a self-contained .NET 8 single-file executable.

Required mods still need to be installed through Steam Workshop: HarmonyX, KitchenLib, PreferenceSystem, Hot Pot House, CountUp!, Soups Plus, and their dependencies.

Close PlateUp before installing or removing the local mods.
