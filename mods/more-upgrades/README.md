# More Upgrades (compatibility rebuild)

Compatibility rebuild of the deprecated PlateUp! Workshop mod **More Upgrades**
(Workshop item `2925086477`).

The original binary is used only as a behavioural reference. This rebuild uses
the current KitchenLib and PreferenceSystem APIs and preserves the original
upgrade-cycle options.

Version 2.1.0 replaces the ambiguous option-only menu with labelled appliance
paths and short explanations. Each setting now states which appliances it
changes and the exact order used by cycle mode.

## Build

The project defaults to the standard Steam paths on Windows. Override `GameDir`
and `WorkshopDir` as MSBuild properties if Steam is installed elsewhere.

```powershell
dotnet build -c Release
```

## Install

Subscribe to KitchenLib and PreferenceSystem, then put the resulting
`MoreUpgrades.dll` in a local PlateUp mod directory. Do not load it at the same
time as Workshop item `2925086477` because both use the same mod ID.
