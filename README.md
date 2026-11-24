# Sapient Fix: Compat

![Sapient Fix: Compat](About/Preview.png)

Lets Big and Small's sapient animals use features other mods add to humans.

Unofficial patch, not affiliated with or endorsed by RedMattis. Preview art adapted from Big and Small - Sapient Animals by RedMattis.

## Why this exists

In my colony, sapient animals stopped eating on their own, even when ravenously hungry. I traced it to Socio Butterfly expecting a component they did not have. Looking further, the same gap was also keeping features from other mods away from them, so this patch fixes the cause instead of that one symptom.

## What it does

Big and Small builds its sapient animal defs (`HL_*`) from `Human`, but keeps only the components listed in its `compWhitelist`. Features that other mods add to `Human` are therefore missing on sapient animals, and some of those mods throw errors when the component is absent.

This mod adds `Human`'s components to that whitelist just before the sapient animals are generated. Other humanlike races are not changed.

## AI disclosure

This mod was coded with AI assistance. I directed the work, made the design decisions and tested it in my own game with about 500 mods.

## Requirements

- RimWorld 1.7
- Harmony
- Big and Small - Framework, with sapient animals enabled (for example by Big and Small - Sapient Animals)

## Excluding a component

Add its full `CompProperties` type or comp class name to a `SapientFix.SapientCompExclusionDef`:

```xml
<SapientFix.SapientCompExclusionDef>
  <defName>MyMod_SapientCompExclusions</defName>
  <excludedComps>
    <li>MyMod.CompProperties_Example</li>
  </excludedComps>
</SapientFix.SapientCompExclusionDef>
```

An exclusion only stops this mod from adding the component. Anything Big and Small already whitelists is still transferred.

## Removing the mod

This mod stores no data of its own, but the components it enables can save data on your pawns. If you remove it, sapient animals lose those components and the matching features.

## Building

Requires the .NET 10 SDK. From `Source/`:

```
dotnet build -c Release
```

The `net48` target is written to `Assemblies/`. The `net10.0` target only checks the API against RimWorld 1.7.

## License

[Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)](LICENSE). You can share and adapt this mod for non-commercial purposes, as long as you credit SirApoka and indicate any changes.
