# Cannizzo PlateUp Mods

Source code and downloadable builds for Cannizzo's PlateUp mods.

## Mods

### Hot Pot House Enhanced

Visual and gameplay-presentation fixes for Hot Pot House, including improved provider displays and one soup bowl per guest when a shared hot pot is served.

Required Workshop items: HarmonyX, KitchenLib, Soups Plus, and Hot Pot House.

### Hot Pot House - Colorblind+ Compatibility

Removes Hot Pot House's duplicate labels when Colorblind+ is installed and collapses duplicate unfinished shared-pot order displays.

Required Workshop items: HarmonyX, KitchenLib, Hot Pot House, and Colorblind+.

## Installer integration

[`mods.json`](mods.json) is the update manifest used by Cannizzo ModPatcher. Published DLLs are stored in each mod's `dist` directory and protected by SHA-256 checksums in the manifest.

These compatibility mods do not redistribute Hot Pot House, Colorblind+, Soups Plus, KitchenLib, or HarmonyX.

## Cannizzo ModPatcher

Cannizzo ModPatcher finds the local PlateUp installation and installs or removes the supported local mods. Its source is available in [`patcher`](patcher/).

Download the ready-to-run Windows build from the [latest release](https://github.com/CannizzoOW/PlateUp-Mods/releases/latest).
