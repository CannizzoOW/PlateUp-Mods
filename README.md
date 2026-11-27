# Cannizzo PlateUp Mods

Source code and downloadable builds for Cannizzo's PlateUp mods.

## Mods

### Hot Pot House Enhanced

Visual and gameplay-presentation fixes for Hot Pot House, including improved provider displays and one soup bowl per guest when a shared hot pot is served.

Required Workshop items: HarmonyX, KitchenLib, Soups Plus, and Hot Pot House.

## Installer integration

[`mods.json`](mods.json) is the update manifest used by Cannizzo ModPatcher. Published DLLs are stored in each mod's `dist` directory and protected by SHA-256 checksums in the manifest.

These mods do not redistribute Hot Pot House, CountUp!, Soups Plus, KitchenLib, or HarmonyX.

## Cannizzo ModPatcher

Cannizzo ModPatcher finds PlateUp from Steam even when no `Mods` directory exists, creates it when needed, and retrieves the current mod catalog and verified DLLs from this repository. Its source is available in [`patcher`](patcher/).

Download the ready-to-run Windows build from the [latest release](https://github.com/CannizzoOW/PlateUp-Mods/releases/latest).
