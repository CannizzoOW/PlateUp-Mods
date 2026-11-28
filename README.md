# Cannizzo PlateUp Mods

Source code and downloadable builds for Cannizzo's PlateUp mods.

## Mods

### Hot Pot House Enhanced

Visual and gameplay-presentation fixes for Hot Pot House, including improved provider displays and one soup bowl per guest when a shared hot pot is served.

Required Workshop items: HarmonyX, KitchenLib, Soups Plus, and Hot Pot House.

### Hot Pot House - CountUp! Compatibility

Lets CountUp! display the remaining number of Hot Pot House side dishes without duplicate labels or counters.

### More Upgrades

A current rebuild of the deprecated More Upgrades mod. Adds configurable research cycles for hobs, counters, tables, mops, combiners, portioners, footwear, and player tools. Version 2.1.0 labels every option with the affected appliances and cycle order.

Required Workshop items: HarmonyX, KitchenLib, and PreferenceSystem.

### Restart Day - Soft Opening

Adds a host-only Restart Day option to the pause menu during an active day, with a confirmation screen before current-day progress is discarded.

Required Workshop items: HarmonyX and KitchenLib.

## Installer integration

[`mods.json`](mods.json) is the update manifest used by Cannizzo ModPatcher. Published DLLs are stored in each mod's `dist` directory and protected by SHA-256 checksums in the manifest.

These mods do not redistribute Hot Pot House, CountUp!, Soups Plus, KitchenLib, or HarmonyX.

## Cannizzo ModPatcher

Cannizzo ModPatcher finds PlateUp from Steam even when no `Mods` directory exists, creates it when needed, and retrieves the current mod catalog and verified DLLs from this repository. Its source is available in [`patcher`](patcher/).

Download the ready-to-run Windows build from the [latest release](https://github.com/CannizzoOW/PlateUp-Mods/releases/latest).
