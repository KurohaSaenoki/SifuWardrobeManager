# SifuWardrobeManager

Built in C# with Visual Studio, Requires [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

Uses [UAssetAPI](https://github.com/atenfyr/UAssetAPI) to open the assets in "ORIGINALASSETS" and then expand both of them, saves them as new assets, then uses [UnrealPak](https://unrealpak.com/) to compile the mod for you.
[Newtonsoft.Json](https://www.nuget.org/packages/newtonsoft.json/) is used to read the .jsons that contains the slots: Name, Description, and Mesh Paths.

Comes with .jsons setup in the "SlotsToAdd" to add all of the NPC enemies. If you want to add a modded character, the mod developer must use a unique path + upload a .json along with it to be used with this tool.

Additionally comes with a "BP_Menu_Outfits-UnlockAll" asset in the "ORIGINALASSETS" folder, simply rename the original "BP_Menu_Outfits" assets to use this one instead which has the vanilla outfits all unlocked.

## USAGE  (SETTING EXISTING MODS ON NEW SLOTS)

- Using [UnrealPak](https://unrealpak.com/), Unpak the existing mod .pak folder
- Using my [UE-Path-Updater](https://github.com/KurohaSaenoki/UE-Path-Updater), update the mods assets so the pathing is no longer: /Game/Characters/MainChar/
    - Example: /Game/Characters/MainChar/ -> /Game/Zair_2B/
    - Resulting in the path being: {Mod-Name}/Sifu/Content/Zair_2B/
- Ensure your Unpaked Mod folder is accurate to your updated path
- Use UnrealPak to RePak the mod folder using these new pathways


- Duplicate a .json in the "SlotsToAdd" folder (or use the Template.json)
- Rename it, they get added alphabetically (hence why the NPCs use numbers at the start)
- Open your new .json, set a new Name, Description, and Paths


- OPTIONAL: If you want to have the original outfits unlocked by default:
  - Open the "ORIGINALASSETS" folder
  - Rename the "BP_Menu_Outfits" to whatever
  - Rename the "BP_Menu_Outfits-UnlockAll" to "BP_Menu_Outfits"


- Open the SifuWardrobeManager.exe
    - The modified assets will be saved into a new folder named "SifuWardrobeManager"
    - A Pak will automatically be created using the "SifuWardrobeManager" folder, along with copying a .sig in the "ORIGINALASSETS" folder.
- Place the SifuWardrobeManager_9_P .pak and .sig into your Sifu/Sifu/Content/Paks/~mods folder


## Why did I make this?

I got Sifu from Nexus' Donation Points system recently and enjoyed it immensely. I then used a mod called "[Sifu Character Selector](https://www.nexusmods.com/sifu/mods/871)", which added the NPCs as costumes, but it's outdated without the new outfits.
I decided to learn how they added the button slots, manually expanding it a few times with [UAssetGUI](https://github.com/atenfyr/UAssetGUI), but it was too tedious to add multiple at once, so I made this tool to add multiple at once for me.

As an added benefit of being done automatically through .jsons, we can now additionally add/remove any slots we want without any effort and should be good in future updates - just having to update the "ORIGINALASSETS" folder.
