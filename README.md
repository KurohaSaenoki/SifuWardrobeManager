
# SifuWardrobeManager

Built in C# with Visual Studio 2026, Requires [.NET 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

Uses [UnrealPak](https://unrealpak.com/) to Unpackage Replacer mods.
Modifies those assets using [UAssetAPI](https://github.com/atenfyr/UAssetAPI).
Modifies the assets in the "ORIGINALASSETS" folder to add new select-able slots based on .jsons inside the "SlotsToAdd" folder.
Recompiles the assets into a .pak paired with a .sig, ready to be inserted into the game.

[Newtonsoft.Json](https://www.nuget.org/packages/newtonsoft.json/) is used to read the .jsons that this tool loads to add new wardrobe slots and is also required to use UAssetAPI.

Comes with .jsons setup in the "SlotsToAdd" to add all of the NPC enemies.
Most mods should outright work with this manager as it will add the mods based on the inserted .pak name, then replace the original Sifu pathways with it's own pathways.

If a mod developer wants to make a mod require this, just make your mod in a unique pathway then include a .json that properly links to your model.
This way you have more control over what endusers would see Name and Description wise.

## USAGE
- Insert any Replacer Mod .paks into the "Mods" folder
- Launch SifuWardrobeManager.exe
- Type Y/N (Yes or No) for Unlocking all the Vanilla Outfits by default
- Generate a .json for the new mod inserts
    - Input a Sort Number
    - Input a Name
    - Input a Description (Please properly credit modders)
- The program should finish up and automatically create a new .pak/.sig
- Place the SifuWardrobeManager_9_P .pak/.sig inside of "Sifu/Sifu/Content/Paks/~mods"
    - Create a new "~mods" folder if it doesn't already exist

<a href="https://www.nexusmods.com/sifu/mods/118">
<img width="2558" height="1439" src="https://github.com/user-attachments/assets/3f60d073-6d93-4e58-856b-cbfb5f73fb2a" alt="2B Mod by Zair"/>
</a>

## Why did I make this?
I got Sifu from Nexus' Donation Points system recently and enjoyed it immensely. I used a mod called "[Sifu Character Selector](https://www.nexusmods.com/sifu/mods/871)", which added the NPCs as costumes shortly after beating the game for the first time, but it's outdated without the new outfits.

I decided to learn how they created the mod (adding button slots and setting the NPC models to the slot index).

After figuring it all out, I manually expanded it a few times with [UAssetGUI](https://github.com/atenfyr/UAssetGUI)
but it was too tedious to add multiple slots, so I made this tool to simplify the process and save myself a ton of time since I wanted to add over 200 entries for all the NPCs.

As an added benefit of being done automatically through .jsons, we can now additionally add/remove any slots we want without any effort and should be good in future updates - just having to update the "ORIGINALASSETS" folder.
