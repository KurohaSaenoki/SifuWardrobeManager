
# SifuWardrobeManager

Built in C# with Visual Studio 2022, Requires [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

Uses [UAssetAPI](https://github.com/atenfyr/UAssetAPI) to open the assets in "ORIGINALASSETS" and then expand both of them, saves them as new assets, then uses [UnrealPak](https://unrealpak.com/) to compile the mod for you.

[Newtonsoft.Json](https://www.nuget.org/packages/newtonsoft.json/) is used to read the .jsons that this tool loads to add new wardrobe slots.

Comes with .jsons setup in the "SlotsToAdd" to add all of the NPC enemies. If you want to add a modded character, the mod developer must use a unique path + upload a .json along with it to be used with this tool.

## USAGE  (SETTING EXISTING MODS ON NEW SLOTS)

### Updating File Paths from the existing mod to no longer replace the original model
- Using [UnrealPak](https://unrealpak.com/), Unpak the existing mod .pak folder
- Using my [UE-Path-Updater](https://github.com/KurohaSaenoki/UE-Path-Updater), update the mods assets so the pathing is no longer: /Game/Characters/MainChar/
    - Example: /Game/Characters/MainChar/ -> /Game/Zair_2B/
- Ensure your Unpaked Mod folder is accurate to your updated path
    - Example: {Mod-Name}/Sifu/Content/Zair_2B/
- Use UnrealPak to RePak the mod folder using these new pathways
- Place the new mod .pak into your Sifu/Sifu/Content/Paks/~mods folder
- Ensure it has a .sig by copying any other sig and renaming it to match the pak name

### Optional Step to Unlock all Vanilla Outfits
  - Open the "ORIGINALASSETS" folder
  - Rename the "BP_Menu_Outfits" to whatever
  - Rename the "BP_Menu_Outfits-UnlockAll" to "BP_Menu_Outfits"

### Using the SifuWardrobeManager to Expand the Wardrobe
- Duplicate a .json in the "SlotsToAdd" folder (or use the Template.json)
- Rename it, they get added alphabetically (hence why the NPCs use numbers at the start)
- Open your new .json, set a new Name, Description, and Paths to match the new model
- Open the SifuWardrobeManager.exe
    - The modified assets will be saved into a new folder named "SifuWardrobeManager"
    - A Pak will automatically be created using the "SifuWardrobeManager" folder, along with copying a .sig in the "ORIGINALASSETS" folder.
- Place the SifuWardrobeManager_9_P .pak and .sig into your Sifu/Sifu/Content/Paks/~mods folder

<a href="https://www.nexusmods.com/sifu/mods/118">
<img width="2558" height="1439" src="https://github.com/user-attachments/assets/3f60d073-6d93-4e58-856b-cbfb5f73fb2a" alt="2B Mod by Zair"/>
</a>

## Why did I make this?
I got Sifu from Nexus' Donation Points system recently and enjoyed it immensely. I used a mod called "[Sifu Character Selector](https://www.nexusmods.com/sifu/mods/871)", which added the NPCs as costumes shortly after beating the game for the first time, but it's outdated without the new outfits.

I decided to learn how they created the mod (adding button slots and setting the NPC models to the slot index).

After figuring it all out, I manually expanded it a few times with [UAssetGUI](https://github.com/atenfyr/UAssetGUI)
but it was too tedious to add multiple slots, so I made this tool to simplify the process and save myself a ton of time since I wanted to add over 200 entries for all the NPCs.

As an added benefit of being done automatically through .jsons, we can now additionally add/remove any slots we want without any effort and should be good in future updates - just having to update the "ORIGINALASSETS" folder.
