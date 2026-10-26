using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace SifuWardrobeManager
{
    internal class Program
    {
        public static string? name;
        public static string? desc;
        public static string? male;
        public static string? female;
        public static int bloodSlotIndex = 0;
        public static int slotCount = 0;
        public static bool ModifiedAssets = false;
        public static string? pakfile;

        //Check Mesh if Materials exist in mod folder, before editing the Name Maps for it

        static void Main(string[] args)
        {
            string FinalizedModFolder = "SifuCustomWardrobe";
            string OrigAssetFolder = Path.Combine(Directory.GetCurrentDirectory(), "ORIGINALASSETS");
            string OutputFolder = Path.Combine(Directory.GetCurrentDirectory(), FinalizedModFolder);
            string JSONFolder = Path.Combine(Directory.GetCurrentDirectory(), "SlotsToAdd");
            string ModFolder = Path.Combine(Directory.GetCurrentDirectory(), "Mods");
            string PakPath = Path.Combine(Directory.GetCurrentDirectory(), FinalizedModFolder + "_9_P.pak");
            string NewSig = Path.Combine(Directory.GetCurrentDirectory(), FinalizedModFolder + "_9_P.sig");

            try
            {
                if (File.Exists(PakPath))
                {
                    File.Delete(PakPath);
                }
                if (File.Exists(NewSig))
                {
                    File.Delete(NewSig);
                }
            }
            catch
            {
                Console.WriteLine("Couldn't Delete Existing Pak/Sig");
                Proceed();
                return;
            }

            if (!Directory.Exists(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
            }
            if (!Directory.Exists(ModFolder))
            {
                Directory.CreateDirectory(ModFolder);
            }

            string TempFolder = Path.Combine(OutputFolder, "Temp");
            string ModBasePath = Path.Combine(OutputFolder, "Sifu", "Content");
            string ModOutfitButtonPath = Path.Combine(ModBasePath, "UI", "Blueprints", "Menus", "Outfits");
            string ModFightingPlayerPath = Path.Combine(ModBasePath, "Blueprints");

            string[] NecessaryFiles = {
                Path.Combine(OrigAssetFolder, "BP_Menu_Outfits.uasset"),
                Path.Combine(OrigAssetFolder, "BP_Menu_Outfits.uexp"),
                Path.Combine(OrigAssetFolder, "BP_FightingPlayer.uasset"),
                Path.Combine(OrigAssetFolder, "BP_FightingPlayer.uexp"),
            };

            bool DoFilesExist = NecessaryFiles.All(File.Exists);
            if (DoFilesExist)
            {
                ClearFolder(OutputFolder);

                UAsset MenuOutfitAsset = new UAsset(NecessaryFiles[0], EngineVersion.VER_UE4_26);
                UAsset PlayerAsset = new UAsset(NecessaryFiles[2], EngineVersion.VER_UE4_26);

                NormalExport OutfitsScrollExport = (NormalExport)MenuOutfitAsset["OutfitsScroll"];
                if (OutfitsScrollExport == null)
                {
                    Console.WriteLine("Missing the OutfitsScroll Export. Please ensure the asset is correct.");
                    Proceed();
                    return;
                }

                AllowUserToUnlockOutfits(MenuOutfitAsset, OutfitsScrollExport);

                string[] ModFileArray = GatherFiles(ModFolder, "*.pak");
                foreach (string FilePath in ModFileArray)
                {
                    string PossibleSig = FilePath.Replace(".pak", ".sig");
                    if (File.Exists(PossibleSig))
                    {
                        File.Delete(PossibleSig);
                    }

                    if (FilePath.Contains(" "))
                    {
                        string NoSpacePath = FilePath.Replace(" ", "_");
                        File.Move(FilePath, NoSpacePath);
                    }
                }
                ModFileArray = GatherFiles(ModFolder, "*.pak");

                //Remove Sort IDs for now so can detect .Pak mods that have already had a .json generated for them
                string[] JSONFileArray = GatherFiles(JSONFolder, "*.json");
                for (int i = 0; i < JSONFileArray.Length; i++)
                {
                    string SortIDToRemove = ReturnFileNameWithoutSort(Path.GetFileNameWithoutExtension(JSONFileArray[i]));
                    JSONFileArray[i] = Path.GetFileNameWithoutExtension(JSONFileArray[i]).Substring(SortIDToRemove.Length);
                }

                //If UnrealPak exists, unpak all mods in the "Mods" folder, update their paths/name maps, then add them to the wardrobe mod
                string UnrealPakPath = Directory.GetCurrentDirectory() + "\\UnrealPak.exe";
                if (File.Exists(UnrealPakPath))
                {
                    if (ModFileArray.Length > 0)
                    {
                        Console.WriteLine($"============= Detected UnrealPak, Unpacking Mods =============");
                        Console.WriteLine();

                        foreach (string ModPath in ModFileArray)
                        {
                            string CurrentOutputFolder = Path.Combine(TempFolder, Path.GetFileNameWithoutExtension(ModPath));
                            Console.WriteLine($"========================== Unpacking Mod: {Path.GetFileNameWithoutExtension(ModPath)} ==========================");

                            //Unpak Mods
                            ProcessStartInfo startInfo = new ProcessStartInfo();
                            startInfo.FileName = "UnrealPak.exe";
                            startInfo.Arguments = $"\"{ModPath}\" -extract \"{CurrentOutputFolder}\"";

                            // Capture the process instance and wait for it to exit
                            using (var process = Process.Start(startInfo))
                            {
                                if (process != null)
                                {
                                    process.WaitForExit();
                                }
                                else
                                {
                                    Console.WriteLine("UnrealPak failed to launch...?");
                                    Proceed();
                                }
                            }
                            Console.WriteLine();
                            Console.WriteLine();
                        }

                        Console.WriteLine();
                        Console.WriteLine("============= Finished Unpacking Mods =============");
                        Console.WriteLine();
                        Console.WriteLine("============= Creating List of All Materials and Textures so the Meshes will be properly linked =============");
                        Console.WriteLine();

                        //Sorting them based on Imports would be better than this.. but that sounds like it would be a pain to do first before editing any meshes, am sure this is good enough
                        var enumOptions = new EnumerationOptions
                        {
                            IgnoreInaccessible = true,
                            RecurseSubdirectories = true,
                            MatchCasing = MatchCasing.CaseSensitive
                        };

                        var MaterialFolders = Directory.EnumerateDirectories(OutputFolder, "*", enumOptions)
                            .Where(dir => {
                                string folderName = Path.GetFileName(dir);
                                return folderName.Contains("Material", StringComparison.OrdinalIgnoreCase) ||
                                       folderName.Equals("Mat", StringComparison.OrdinalIgnoreCase);
                            }).SelectMany(dir => Directory.EnumerateFiles(dir, "*"));

                        var Materials = Directory.EnumerateFiles(OutputFolder, "M_*", enumOptions);
                        var MaterialInstances = Directory.EnumerateFiles(OutputFolder, "MI_*", enumOptions);

                        string[] MaterialArray = MaterialFolders
                            .Union(Materials, StringComparer.OrdinalIgnoreCase)
                            .Union(MaterialInstances, StringComparer.OrdinalIgnoreCase)
                            .Where(file => Path.GetExtension(file).Equals(".uasset", StringComparison.OrdinalIgnoreCase))
                            .ToArray();

                        for (int i = 0; i < MaterialArray.Length; i++)
                        {
                            if (MaterialArray[i].StartsWith(TempFolder))
                            {
                                MaterialArray[i] = MaterialArray[i].Substring(TempFolder.Length + 1);
                                MaterialArray[i] = Path.ChangeExtension(MaterialArray[i], null);
                                MaterialArray[i] = MaterialArray[i].Replace("\\", "/");
                            }
                        }
                        foreach (string file in MaterialArray)
                        {
                            Console.WriteLine(file);
                        }

                        var TextureFolders = Directory.EnumerateDirectories(OutputFolder, "*", enumOptions)
                            .Where(dir => {
                                string folderName = Path.GetFileName(dir);
                                return folderName.Contains("Texture", StringComparison.OrdinalIgnoreCase) ||
                                       folderName.Equals("Tex", StringComparison.OrdinalIgnoreCase);
                            }).SelectMany(dir => Directory.EnumerateFiles(dir, "*"));

                        var Textures = Directory.EnumerateFiles(OutputFolder, "T_*", enumOptions);

                        string[] TextureArray = TextureFolders
                            .Union(Textures, StringComparer.OrdinalIgnoreCase)
                            .Where(file => Path.GetExtension(file).Equals(".uasset", StringComparison.OrdinalIgnoreCase))
                            .ToArray();

                        for (int i = 0; i < TextureArray.Length; i++)
                        {
                            if (TextureArray[i].StartsWith(TempFolder))
                            {
                                TextureArray[i] = TextureArray[i].Substring(TempFolder.Length + 1);
                                TextureArray[i] = Path.ChangeExtension(TextureArray[i], null);
                                TextureArray[i] = TextureArray[i].Replace("\\", "/");
                            }
                        }
                        foreach (string file in TextureArray)
                        {
                            Console.WriteLine(file);
                        }

                        Console.WriteLine();
                        Console.WriteLine("============= Updating Unpacked Mods with Unique Pathways =============");
                        Console.WriteLine();

                        //Use UEPath Updater to automatically update their Name Maps, then File Paths and lastly create a .json for the slot if one doesn't already exist
                        foreach (string ModPath in ModFileArray)
                        {
                            string CurrentModName = Path.GetFileNameWithoutExtension(ModPath);
                            Console.WriteLine($"========================== Current Mod: {CurrentModName} ==========================");
                            Console.WriteLine();

                            string CurrentOutputFolder = Path.Combine(TempFolder, CurrentModName);
                            string[] AllFiles = Directory.EnumerateFiles(CurrentOutputFolder, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".uasset")).ToArray();
                            string M_MeshPath = "";
                            string W_MeshPath = "";
                            string AppendPackageName = "";

                            if (AllFiles.Length > 0)
                            {
                                foreach (string UAssetPath in AllFiles)
                                {
                                    Console.WriteLine($"============= Modifying Asset: {Path.GetFileNameWithoutExtension(UAssetPath)} =============");
                                    Console.WriteLine();

                                    UAsset CurrentAsset = new UAsset(UAssetPath, EngineVersion.VER_UE4_26);
                                    string StringFind = "/Game/Characters/MainChar/";
                                    string StringReplace = "/Game/" + CurrentModName + "/";

                                    if (Directory.Exists(Path.Combine(TempFolder, CurrentModName, "_Shared")) && UAssetPath.Contains("_Shared"))
                                    {
                                        StringFind = "/Game/Characters/_Shared/";
                                        StringReplace = "/Game/" + CurrentModName + "/_Shared/";
                                    }

                                    string NewFileName = "";
                                    string NewFilePath = "";
                                    string UAssetPathExportName = "";
                                    string AnotherNameMapWithExportName = "";
                                    string Class = "";
                                    string OriginalMat = "";

                                    if (!CurrentAsset.VerifyBinaryEquality())
                                    {
                                        Console.WriteLine("Invalid Binary Equality... Something is wrong with this asset...");
                                        Proceed();
                                        return;
                                    }

                                    //Read Exports
                                    try
                                    {
                                        //Get Filename of Asset
                                        for (int i = 0; i < CurrentAsset.Exports.Count; i++)
                                        {
                                            if (CurrentAsset.Exports[i].OuterIndex.ToString() == "0")
                                            {
                                                //Filters out any "Default__" Exports
                                                if (CurrentAsset.Exports[i].ClassIndex.Index < 0)
                                                {
                                                    Class = CurrentAsset.Imports[Math.Abs(CurrentAsset.Exports[i].ClassIndex.Index) - 1].ObjectName.Value.ToString();
                                                    //There should never be a WBP, but needs to be checked due to _C being used for _Color
                                                    if (Class == "WidgetBlueprintGeneratedClass" && CurrentAsset.Exports[i].ObjectName.ToString().EndsWith("_C"))
                                                    {
                                                        UAssetPathExportName = CurrentAsset.Exports[i].ObjectName.ToString()[..^2];
                                                    }
                                                    else
                                                    {
                                                        UAssetPathExportName = CurrentAsset.Exports[i].ObjectName.ToString();
                                                    }
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        Console.WriteLine("Failed to Look Through Exports... Something is wrong with this asset...");
                                        Proceed();
                                        return;
                                    }

                                    //Read Imports
                                    try
                                    {
                                        //Check for if linked to another Asset that uses the same name...
                                        foreach (var import in CurrentAsset.Imports)
                                        {
                                            try
                                            {
                                                if (import?.ClassName?.Value != null && import?.ObjectName?.Value != null && UAssetPathExportName != null)
                                                {
                                                    string className = import.ClassName.Value.ToString();
                                                    string objectName = import.ObjectName.Value.ToString();

                                                    if (className == "Package" && objectName.StartsWith("/Game/") && objectName.EndsWith(UAssetPathExportName))
                                                    {
                                                        AnotherNameMapWithExportName = objectName;
                                                    }
                                                    if (Class == "MaterialInstanceConstant")
                                                    {
                                                        NormalExport MainExport = (NormalExport)CurrentAsset[Path.GetFileNameWithoutExtension(CurrentAsset.FilePath)];

                                                        if (MainExport != null)
                                                        {
                                                            if (MainExport.Data.FirstOrDefault(x => x.Name.ToString() == "Parent") is ObjectPropertyData OriginalMatObj)
                                                            {
                                                                OriginalMat = CurrentAsset.Imports[Math.Abs(OriginalMatObj.Value.Index - 1)].ObjectName.Value.ToString();
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    Console.WriteLine("Failed to Look Through Imports... Something is wrong with this asset...");
                                                    Proceed();
                                                    return;
                                                }
                                            }
                                            catch
                                            {
                                                Console.WriteLine("Failed to Look Through Imports... Something is wrong with this asset...");
                                                Proceed();
                                                return;
                                            }
                                        }

                                    }
                                    catch
                                    {
                                        Console.WriteLine("Failed to Look Through Imports... Something is wrong with this asset...");
                                        Proceed();
                                        return;
                                    }

                                    Match EndsWithNumber = Regex.Match(UAssetPathExportName, @"\d+$");
                                    if (EndsWithNumber.Success)
                                    {
                                        AppendPackageName = "_" + EndsWithNumber.Value;
                                    }

                                    //Update NameMap
                                    try
                                    {
                                        //Update Name Map Entries
                                        for (int i = 0; i < CurrentAsset.GetNameMapIndexList().Count; i++)
                                        {
                                            string CurrentString = CurrentAsset.GetNameReference(i).ToString();
                                            string NameOfPath = Path.GetFileNameWithoutExtension(CurrentString);
                                            string UpdatedString = CurrentString.Replace(StringFind, StringReplace);
                                            bool AllowModify = false;

                                            if (Class == "SkeletalMesh")
                                            {
                                                if (CurrentString.Contains("Material", StringComparison.OrdinalIgnoreCase) ||
                                                    CurrentString.Contains("Mat", StringComparison.OrdinalIgnoreCase) ||
                                                    NameOfPath.StartsWith("MI_") || NameOfPath.StartsWith("M_"))
                                                {
                                                    AllowModify = MaterialArray.Any(item => item.StartsWith(CurrentModName) && item.EndsWith(NameOfPath));
                                                    if (!AllowModify)
                                                    {
                                                        string OriginalPath = "";
                                                        if (CurrentString.Contains("/Game/Characters/MainChar"))
                                                        {
                                                            OriginalPath = CurrentString.Replace("/Game/Characters/MainChar", OrigAssetFolder);
                                                        }
                                                        else if (CurrentString.Contains("/Game/Characters/_Shared"))
                                                        {
                                                            OriginalPath = CurrentString.Replace("/Game/Characters/_Shared", OrigAssetFolder);
                                                        }

                                                        if (OriginalPath != "")
                                                        {
                                                            string OriginalFileName = Path.GetFileNameWithoutExtension(OriginalPath);
                                                            OriginalPath = OriginalPath.Replace("/", "\\");
                                                            OriginalPath = OriginalPath + ".uasset";

                                                            if (File.Exists(OriginalPath))
                                                            {
                                                                UAsset OriginalAsset = new UAsset(OriginalPath, EngineVersion.VER_UE4_26);
                                                                bool SaveAsset = false;
                                                                foreach (var import in OriginalAsset.Imports)
                                                                {
                                                                    if (import?.ClassName?.Value != null && import?.ObjectName?.Value != null && UAssetPathExportName != null)
                                                                    {
                                                                        string className = import.ClassName.Value.ToString();
                                                                        string objectName = import.ObjectName.Value.ToString();

                                                                        //Update Textures in Original Asset to match the mod
                                                                        if (className == "Package" && objectName.StartsWith("/Game/") &&
                                                                            (objectName.Contains("Textures") || Path.GetFileNameWithoutExtension(objectName).StartsWith("T_")))
                                                                        {
                                                                            if (TextureArray.Any(item => item.StartsWith(CurrentModName) && item.EndsWith(Path.GetFileNameWithoutExtension(objectName))))
                                                                            {
                                                                                string UpdatedOrigMapName = objectName.Replace(StringFind, StringReplace);
                                                                                int MapIndex = OriginalAsset.SearchNameReference((FString)objectName);

                                                                                OriginalAsset.SetNameReference(MapIndex, (FString)UpdatedOrigMapName);
                                                                                SaveAsset = true;
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                                if (SaveAsset)
                                                                {
                                                                    //Update Original Asset name and save in new location
                                                                    int MapIndex = OriginalAsset.SearchNameReference((FString)CurrentString);
                                                                    string NewPath = UpdatedString.Replace("/Materials/", "/SWM_Materials/");
                                                                    OriginalAsset.SetNameReference(MapIndex, (FString)NewPath);
                                                                    NewPath = NewPath.Replace("\\", "/");
                                                                    NewPath = Path.GetDirectoryName("/Sifu/Content/" + NewPath.Substring(6)) ?? "";
                                                                    NewPath = OutputFolder + NewPath;
                                                                    if (!Directory.Exists(NewPath))
                                                                    {
                                                                        Directory.CreateDirectory(NewPath);
                                                                    }
                                                                    NewPath = Path.Combine(NewPath, OriginalFileName + ".uasset");
                                                                    OriginalAsset.Write(NewPath);

                                                                    //Modify Current Asset
                                                                    MapIndex = CurrentAsset.SearchNameReference((FString)CurrentString);
                                                                    UpdatedString = UpdatedString.Replace("/Materials/", "/SWM_Materials/");
                                                                    CurrentAsset.SetNameReference(MapIndex, (FString)UpdatedString);
                                                                }
                                                            }


                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    AllowModify = true;
                                                }
                                            }
                                            else if (Class == "MaterialInstanceConstant")
                                            {
                                                if (CurrentString == OriginalMat)
                                                {
                                                    if (CurrentString.Contains("Material", StringComparison.OrdinalIgnoreCase) ||
                                                        CurrentString.Contains("Mat", StringComparison.OrdinalIgnoreCase) ||
                                                        NameOfPath.StartsWith("MI_") || NameOfPath.StartsWith("M_"))
                                                    {
                                                        AllowModify = MaterialArray.Any(item => item.StartsWith(CurrentModName) && item.EndsWith(NameOfPath));
                                                        if (!AllowModify)
                                                        {
                                                            string OriginalPath = "";
                                                            if (CurrentString.StartsWith("/Game/Characters/MainChar"))
                                                            {
                                                                OriginalPath = CurrentString.Replace("/Game/Characters/MainChar", OrigAssetFolder);
                                                            }
                                                            else if (CurrentString.StartsWith("/Game/Characters/_Shared"))
                                                            {
                                                                OriginalPath = CurrentString.Replace("/Game/Characters", OrigAssetFolder);
                                                            }

                                                            if (OriginalPath != "")
                                                            {
                                                                string OriginalFileName = Path.GetFileNameWithoutExtension(OriginalPath);
                                                                OriginalPath = OriginalPath.Replace("/", "\\");
                                                                OriginalPath = OriginalPath + ".uasset";

                                                                if (File.Exists(OriginalPath))
                                                                {
                                                                    UAsset OriginalAsset = new UAsset(OriginalPath, EngineVersion.VER_UE4_26);
                                                                    bool SaveAsset = false;
                                                                    foreach (var import in OriginalAsset.Imports)
                                                                    {
                                                                        if (import?.ClassName?.Value != null && import?.ObjectName?.Value != null && UAssetPathExportName != null)
                                                                        {
                                                                            string className = import.ClassName.Value.ToString();
                                                                            string objectName = import.ObjectName.Value.ToString();

                                                                            //Update Textures in Original Asset to match the mod
                                                                            if (className == "Package" && objectName.StartsWith("/Game/") &&
                                                                                (objectName.Contains("Textures") || Path.GetFileNameWithoutExtension(objectName).StartsWith("T_")))
                                                                            {
                                                                                if (TextureArray.Any(item => item.StartsWith(CurrentModName) && item.EndsWith(Path.GetFileNameWithoutExtension(objectName))))
                                                                                {
                                                                                    string UpdatedOrigMapName = "";
                                                                                    if (CurrentString.StartsWith("/Game/Characters/MainChar"))
                                                                                    {
                                                                                        UpdatedOrigMapName = objectName.Replace(StringFind, StringReplace);
                                                                                    }
                                                                                    else if (CurrentString.StartsWith("/Game/Characters/_Shared"))
                                                                                    {
                                                                                        UpdatedOrigMapName = objectName.Replace("/Game/Characters/_Shared/", $"/Game/{CurrentModName}/Characters/_Shared/");
                                                                                    }

                                                                                    int MapIndex = OriginalAsset.SearchNameReference((FString)objectName);

                                                                                    OriginalAsset.SetNameReference(MapIndex, (FString)UpdatedOrigMapName);
                                                                                    SaveAsset = true;
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                    if (SaveAsset)
                                                                    {
                                                                        //Update Original Asset name and save in new location
                                                                        int MapIndex = OriginalAsset.SearchNameReference((FString)CurrentString);

                                                                        string NewPath = UpdatedString.Replace("/Materials/", "/SWM_Materials/");
                                                                        NewPath = NewPath.Replace("/Game/Characters/_Shared/", $"/Game/{CurrentModName}/Characters/_Shared/");

                                                                        OriginalAsset.SetNameReference(MapIndex, (FString)NewPath);
                                                                        NewPath = NewPath.Replace("\\", "/");
                                                                        NewPath = Path.GetDirectoryName("/Sifu/Content/" + NewPath.Substring(6)) ?? "";
                                                                        NewPath = OutputFolder + NewPath;
                                                                        if (!Directory.Exists(NewPath))
                                                                        {
                                                                            Directory.CreateDirectory(NewPath);
                                                                        }
                                                                        NewPath = Path.Combine(NewPath, OriginalFileName + ".uasset");
                                                                        OriginalAsset.Write(NewPath);

                                                                        //Modify Current Asset
                                                                        MapIndex = CurrentAsset.SearchNameReference((FString)CurrentString);
                                                                        UpdatedString = UpdatedString.Replace("/Materials/", "/SWM_Materials/");
                                                                        UpdatedString = UpdatedString.Replace("/Game/Characters/_Shared/", $"/Game/{CurrentModName}/Characters/_Shared/");
                                                                        CurrentAsset.SetNameReference(MapIndex, (FString)UpdatedString);
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    AllowModify = true;
                                                }
                                            }
                                            else
                                            {
                                                AllowModify = true;
                                            }

                                            if (AllowModify && (UpdatedString != CurrentString))
                                            {
                                                CurrentAsset.SetNameReference(i, (FString)UpdatedString);
                                                Console.WriteLine($"Updated NameMap Index {i}: {UpdatedString}");

                                                //Update File Path Based on Name Map Entry
                                                string NameMapFile = Path.GetFileNameWithoutExtension(CurrentString);

                                                if (UAssetPathExportName != "" && UAssetPathExportName != null && (NameMapFile.EndsWith(UAssetPathExportName) || NameMapFile + AppendPackageName == UAssetPathExportName) &&
                                                    CurrentString.StartsWith("/Game/") &&
                                                    CurrentString != AnotherNameMapWithExportName)
                                                {

                                                    if (NameMapFile + AppendPackageName == UAssetPathExportName && AppendPackageName != "")
                                                    {
                                                        NewFileName = Path.GetFileNameWithoutExtension(UpdatedString) + AppendPackageName;
                                                    }
                                                    else
                                                    {
                                                        NewFileName = Path.GetFileNameWithoutExtension(UpdatedString);
                                                    }

                                                    NewFilePath = Path.GetDirectoryName("/Sifu/Content/" + UpdatedString.Substring(6)) ?? "";
                                                    NewFilePath = NewFilePath.Replace("\\", "/");

                                                    if (!NewFilePath.EndsWith("/"))
                                                    {
                                                        NewFilePath += "/";
                                                    }
                                                    if (NewFilePath.StartsWith("//"))
                                                    {
                                                        NewFilePath = NewFilePath.Substring(1);
                                                    }
                                                    NewFilePath = NewFilePath + NewFileName;

                                                    if (CurrentString.StartsWith("/Game/Characters/MainChar/M/") && Class == "SkeletalMesh" && NewFilePath.StartsWith("/Sifu/Content/"))
                                                    {
                                                        M_MeshPath = NewFilePath.Substring(1);
                                                    }
                                                    if (CurrentString.StartsWith("/Game/Characters/MainChar/W/") && Class == "SkeletalMesh" && NewFilePath.StartsWith("/Sifu/Content/"))
                                                    {
                                                        W_MeshPath = NewFilePath.Substring(1);
                                                    }
                                                }
                                            }

                                        }
                                    }
                                    catch
                                    {
                                        Console.WriteLine("Failed to Look Through Name Maps... Something is wrong with this asset...");
                                        Proceed();
                                        return;
                                    }

                                    //Save Asset
                                    try
                                    {
                                        string NewFile = "";
                                        string NewPath = "";
                                        string ubulkPath = UAssetPath.Replace(".uasset", ".ubulk");

                                        NewFile = NewFilePath.Replace('/', Path.DirectorySeparatorChar);
                                        NewFile = OutputFolder + NewFile + ".uasset";
                                        NewPath = Path.GetDirectoryName(NewFile) ?? "";

                                        if (File.Exists(ubulkPath))
                                        {
                                            if (!Path.Exists(NewPath))
                                            {
                                                Directory.CreateDirectory(NewPath);
                                            }

                                            if (ubulkPath != NewFile.Replace(".uasset", ".ubulk"))
                                            {
                                                File.Copy(ubulkPath, NewFile.Replace(".uasset", ".ubulk"), true);
                                            }
                                        }

                                        Console.WriteLine();
                                        Console.WriteLine($"Saving Asset as: " + Path.GetFileNameWithoutExtension(NewFile));
                                        Console.WriteLine();

                                        if (!string.IsNullOrEmpty(NewPath))
                                        {
                                            try
                                            {
                                                Directory.CreateDirectory(NewPath);
                                                CurrentAsset.Write(NewFile);
                                            }
                                            catch
                                            {
                                                Console.WriteLine("Failed to Save Asset... Possibly a lack of storage?");
                                                Proceed();
                                                return;
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        Console.WriteLine("Failed to Save Asset... Something is wrong with this asset...");
                                        Proceed();
                                        return;
                                    }
                                }

                                //Create a new JSON if one does not already exist
                                if (!JSONFileArray.Contains(CurrentModName))
                                {
                                    Console.WriteLine($"============= Generating JSON: {CurrentModName} =============");
                                    string CurrentJson = Path.Combine(JSONFolder, CurrentModName + ".json");
                                    if (!File.Exists(CurrentJson) && (!string.IsNullOrEmpty(M_MeshPath) || !string.IsNullOrEmpty(W_MeshPath)))
                                    {
                                        if (string.IsNullOrEmpty(M_MeshPath))
                                        {
                                            M_MeshPath = W_MeshPath;
                                        }
                                        if (string.IsNullOrEmpty(W_MeshPath))
                                        {
                                            W_MeshPath = M_MeshPath;
                                        }

                                        JObject Json = new JObject();

                                        Console.WriteLine();
                                        Console.WriteLine("Sort Order: (Default Value: 0)\n" +
                                            "Anything before 900 will be loaded immediately after the vanilla outfits.\n" +
                                            "\t\t900-914 are the Main Characters\n" +
                                            "\t\t1000-1014 are Prologue Characters\n" +
                                            "\t\t2000-2048 are Squats Characters\n" +
                                            "\t\t3000-3061 are Club Characters\n" +
                                            "\t\t4000-4054 are Museum Characters\n" +
                                            "\t\t5000-5033 are Bank Characters\n" +
                                            "\t\t6000-6023 are Sanctuary Characters");
                                        string UserInputSort = Console.ReadLine() ?? "0";
                                        Console.WriteLine();
                                        int SortId = 0;
                                        try
                                        {
                                            SortId = int.Parse(UserInputSort);
                                        }
                                        catch
                                        {
                                            UserInputSort = "0";
                                            Console.WriteLine("Invalid Sort ID, Defaulting to 0");
                                        }
                                        CurrentJson = Path.Combine(JSONFolder, UserInputSort + "_" + CurrentModName + ".json");

                                        Console.WriteLine($"Name: (Default Value: {CurrentModName})");
                                        string UserInputName = Console.ReadLine() ?? CurrentModName;
                                        Console.WriteLine();
                                        if (UserInputName == "" || string.IsNullOrEmpty(UserInputName))
                                        {
                                            Console.WriteLine($"Name will be defaulted to: {CurrentModName}");
                                            UserInputName = CurrentModName;
                                        }
                                        Json.Add("Name", UserInputName);

                                        Console.WriteLine($"Description: (Please credit the mod creator here)");
                                        string UserInputDescription = Console.ReadLine() ?? "";
                                        Console.WriteLine();
                                        Json.Add("Description", UserInputDescription);

                                        JObject NestedModel = new JObject();
                                        NestedModel.Add("Male", M_MeshPath);
                                        NestedModel.Add("Female", W_MeshPath);
                                        Json.Add("Model", NestedModel);
                                        Json.Add("Pak", CurrentModName);
                                        try
                                        {
                                            File.WriteAllText(CurrentJson, Json.ToString(Formatting.Indented));
                                        }
                                        catch
                                        {
                                            Console.WriteLine("Failed to Create JSON...  Possibly a lack of storage?");
                                            Proceed();
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        if (File.Exists(CurrentJson))
                                        {
                                            Console.WriteLine("Json already exists with the name: " + Path.GetFileNameWithoutExtension(CurrentJson));
                                        }
                                        else
                                        {
                                            if (string.IsNullOrEmpty(M_MeshPath) && string.IsNullOrEmpty(W_MeshPath))
                                            {
                                                Console.WriteLine("Failed to generate a Json for this mod...\n" +
                                                    "Couldn't automatically find a Mesh to use for this Pak mod...\n" +
                                                    "You should remove this pak from the Mod folder and port it manually.");
                                                Proceed();
                                                return;
                                            }
                                        }
                                    }
                                }
                                Console.WriteLine();
                            }
                            else
                            {
                                Console.WriteLine($"{Path.GetFileNameWithoutExtension(ModPath)} does not contain any .uassets to update...?");
                                Proceed();
                                return;
                            }
                        }
                    }
                }

                //Clean up TempFolder
                if (Path.Exists(TempFolder))
                {
                    Directory.Delete(TempFolder, true);
                }

                //Make this simple no path array for Paks so when adding buttons, if any Pak mods are missing, we can exclude those jsons
                string[] ModFilesNoPath = new string[ModFileArray.Length];
                for (int i = 0; i < ModFilesNoPath.Length; i++)
                {
                    ModFilesNoPath[i] = Path.GetFileNameWithoutExtension(ModFileArray[i]);
                }

                //Resetup this array with it's path + sort id included
                JSONFileArray = GatherFiles(JSONFolder, "*.json");

                //Add new slots
                if (JSONFileArray.Length > 0)
                {
                    Console.WriteLine($"========================== Creating new Costume Slots ==========================");
                    Console.WriteLine();

                    Array.Sort(JSONFileArray, new NaturalStringComparer());

                    NormalExport BP_Menu_Outfits = (NormalExport)MenuOutfitAsset["BP_Menu_Outfits_C"];
                    StructExport BP_StructMenu_Outfits = (StructExport)MenuOutfitAsset["BP_Menu_Outfits_C"];

                    //BtnBlood and its Slot will be cloned for the new buttons
                    NormalExport BtnBloodExport = (NormalExport)MenuOutfitAsset["Btn_Blood"];
                    FObjectProperty? BtnBloodScript = BP_StructMenu_Outfits.LoadedProperties.FirstOrDefault(x => x.Name.ToString() == "Btn_Blood") as FObjectProperty;

                    if (BtnBloodExport == null || BP_Menu_Outfits == null || BP_StructMenu_Outfits == null || BtnBloodScript == null)
                    {
                        Console.WriteLine("One or more necessary exports are missing in the 'BP_Menu_Outfits.uasset'. Please ensure the asset is correct.");
                        Proceed();
                        return;
                    }

                    if (OutfitsScrollExport["Slots"] is ArrayPropertyData arrProp)
                    {
                        slotCount = arrProp.Value.Length - 1;
                        foreach (string filepath in JSONFileArray)
                        {
                            bool LastEntry = false;
                            if (filepath == JSONFileArray[^1])
                            {
                                LastEntry = true;
                            }

                            ParseJSON(filepath);

                            if (name != "" && male != "" && female != "" && (pakfile == "pakchunk0-WindowsNoEditor" || ModFilesNoPath.Contains(pakfile)))
                            {
                                Console.WriteLine($"======== Adding: {name} ========");
                                Console.WriteLine("Expanding BP_Menu_Outfits...");
                                slotCount++;

                                for (int i = 0; i < BtnBloodExport.Data.Count; i++)
                                {
                                    if (BtnBloodExport.Data[i].Name.ToString() == "Slot")
                                    {
                                        bloodSlotIndex = int.Parse(BtnBloodExport.Data[i]?.RawValue?.ToString() ?? "0");
                                        break;
                                    }
                                }

                                NormalExport newBtn = (NormalExport)BtnBloodExport.Clone();
                                NormalExport SlotBloodExport = (NormalExport)MenuOutfitAsset.Exports[bloodSlotIndex];
                                NormalExport newSlot = (NormalExport)SlotBloodExport.Clone();

                                var clonedDataBtn = new List<PropertyData>();
                                foreach (var property in BtnBloodExport.Data)
                                {
                                    clonedDataBtn.Add((PropertyData)property.Clone());
                                }
                                newBtn.Data = clonedDataBtn;

                                var clonedDataSlot = new List<PropertyData>();
                                foreach (var property in SlotBloodExport.Data)
                                {
                                    clonedDataSlot.Add((PropertyData)property.Clone());
                                }
                                newSlot.Data = clonedDataSlot;

                                MenuOutfitAsset.Exports.Add(newBtn);
                                MenuOutfitAsset.Exports.Add(newSlot);

                                //Update the Serialization Dependencies to match the logic of the original button/slot.
                                var deps = newBtn.CreateBeforeSerializationDependencies?.ToList() ?? new List<FPackageIndex>();
                                deps[1] = FPackageIndex.FromRawIndex(MenuOutfitAsset.Exports.IndexOf(newSlot) + 1);
                                newBtn.CreateBeforeSerializationDependencies = deps;

                                deps = newSlot.CreateBeforeSerializationDependencies?.ToList() ?? new List<FPackageIndex>();
                                deps[0] = FPackageIndex.FromRawIndex(MenuOutfitAsset.Exports.IndexOf(newBtn) + 1);
                                newSlot.CreateBeforeSerializationDependencies = deps;

                                var btnname = name?.Replace(" ", "");
                                newBtn.ObjectName = FName.FromString(MenuOutfitAsset, "Btn" + btnname + "_" + slotCount.ToString());
                                newSlot.ObjectName = FName.FromString(MenuOutfitAsset, "Slot" + btnname + "_" + slotCount.ToString());

                                //Edit new button
                                FName TableId = FName.FromString(MenuOutfitAsset, "0");

                                for (int i = 0; i < newBtn.Data.Count; i++)
                                {
                                    if (newBtn.Data[i].Name.ToString() == "Title")
                                    {
                                        if (newBtn.Data[i] is TextPropertyData textProp)
                                        {
                                            textProp.HistoryType = TextHistoryType.Base;
                                            textProp.Value = (FString)"0";
                                            textProp.CultureInvariantString = (FString)name;
                                        }
                                    }
                                    if (newBtn.Data[i].Name.ToString() == "Description")
                                    {
                                        if (newBtn.Data[i] is TextPropertyData textProp)
                                        {
                                            textProp.HistoryType = TextHistoryType.Base;
                                            textProp.Value = (FString)"0";
                                            textProp.CultureInvariantString = (FString)desc;
                                        }
                                    }
                                    if (newBtn.Data[i].Name.ToString() == "iOutfitIndex")
                                    {
                                        newBtn.Data[i].RawValue = slotCount;
                                    }
                                    if (newBtn.Data[i].Name.ToString() == "Slot")
                                    {
                                        var slotIndex = new FPackageIndex(MenuOutfitAsset.Exports.Count);
                                        newBtn.Data[i].RawValue = slotIndex;
                                    }
                                }
                                //Edit new slot
                                for (int i = 0; i < newSlot.Data.Count; i++)
                                {
                                    if (newSlot.Data[i].Name.ToString() == "Content")
                                    {
                                        var slotIndex = new FPackageIndex(MenuOutfitAsset.Exports.Count - 1);
                                        newSlot.Data[i].RawValue = slotIndex;
                                    }
                                }
                                //Edit the OutfitsScroll array to add the new slot
                                var newSlotProperty = new ObjectPropertyData
                                {
                                    Name = FName.FromString(MenuOutfitAsset, "Slots"),
                                    Value = FPackageIndex.FromRawIndex(MenuOutfitAsset.Exports.IndexOf(newSlot) + 1)
                                };

                                var oldValues = arrProp.Value;
                                var newValues = new PropertyData[oldValues.Length + 1];
                                Array.Copy(oldValues, newValues, oldValues.Length);
                                newValues[^1] = newSlotProperty;
                                arrProp.Value = newValues;


                                if (LastEntry)
                                {
                                    var widgetNavis = MenuOutfitAsset.Exports.OfType<NormalExport>().Where(x => x.ObjectName.ToString() == "WidgetNavigation_0").ToList();
                                    NormalExport FirstWidgetNavi = widgetNavis[0];
                                    NormalExport SecondWidgetNavi = widgetNavis[1];
                                    FPackageIndex SecondWidgetNaviIdx = FPackageIndex.FromRawIndex(MenuOutfitAsset.Exports.IndexOf(SecondWidgetNavi) + 1);

                                    NormalExport PreviousLastBtn = (NormalExport)MenuOutfitAsset.Exports[SecondWidgetNavi.OuterIndex.Index - 1];
                                    FPackageIndex PreviousLastBtnIdx = FPackageIndex.FromRawIndex(MenuOutfitAsset.Exports.IndexOf(PreviousLastBtn) + 1);

                                    string FirstCostume = "Btn_DefaultOutfit";

                                    if (OutfitsScrollExport?.Data.FirstOrDefault(x => x.Name.ToString() == "Slots") is ArrayPropertyData OutfitScrollSlots)
                                    {
                                        if (OutfitScrollSlots.Value.Length > 0)
                                        {
                                            if (OutfitScrollSlots.Value[0] is ObjectPropertyData SlotObjProp)
                                            {
                                                NormalExport FirstBtn = (NormalExport)MenuOutfitAsset.Exports[SlotObjProp.Value.Index - 1];

                                                if (FirstBtn?.Data.FirstOrDefault(x => x.Name.ToString() == "Content") is ObjectPropertyData FirstSlotBtnObj)
                                                {
                                                    FirstBtn = (NormalExport)MenuOutfitAsset.Exports[FirstSlotBtnObj.Value.Index - 1];
                                                    FirstCostume = FirstBtn.ObjectName.ToString();
                                                }
                                            }
                                        }
                                    }


                                    if (widgetNavis.Count > 0)
                                    {
                                        if (PreviousLastBtn != null && FirstWidgetNavi != null && SecondWidgetNavi != null)
                                        {
                                            //Fix the "LoopList" when the Widget is Expanded, without having to go through a ton of Bytecode...
                                            ModifyNaviWidgets(MenuOutfitAsset, FirstWidgetNavi, "Up", newBtn.ObjectName.ToString());
                                            ModifyNaviWidgets(MenuOutfitAsset, FirstWidgetNavi, "Previous", newBtn.ObjectName.ToString());

                                            ModifyNaviWidgets(MenuOutfitAsset, SecondWidgetNavi, "Up", FirstCostume, "Next");
                                            ModifyNaviWidgets(MenuOutfitAsset, SecondWidgetNavi, "Down", FirstCostume);

                                            //Remove Navigation Link and Serialization from Previous Last Button (OutfitH5)
                                            int IdxToRemove = -1;
                                            for (int i = PreviousLastBtn.Data.Count - 1; i >= 0; i--)
                                            {
                                                if (PreviousLastBtn.Data[i] is ObjectPropertyData NaviObjProp && NaviObjProp.Name.ToString() == "Navigation")
                                                {
                                                    IdxToRemove = i;
                                                    break;
                                                }
                                            }
                                            if (IdxToRemove > -1)
                                            {
                                                PreviousLastBtn.Data.RemoveAt(IdxToRemove);
                                            }
                                            var PreviousLastBtnDep = PreviousLastBtn.CreateBeforeSerializationDependencies?.ToList() ?? new List<FPackageIndex>();
                                            PreviousLastBtnDep.Remove(SecondWidgetNaviIdx);
                                            PreviousLastBtn.CreateBeforeSerializationDependencies = PreviousLastBtnDep;

                                            //Update the Last Widget Navigation to be linked to the last button
                                            SecondWidgetNavi.OuterIndex = FPackageIndex.FromRawIndex(MenuOutfitAsset.Exports.IndexOf(newBtn) + 1);
                                            var SecondWidgetNaviCreateDeps = new List<FPackageIndex>();
                                            SecondWidgetNaviCreateDeps.Add(FPackageIndex.FromRawIndex(MenuOutfitAsset.Exports.IndexOf(newBtn) + 1));
                                            SecondWidgetNavi.CreateBeforeCreateDependencies = SecondWidgetNaviCreateDeps;

                                            //Add Widget Navigation to New Final Button
                                            ObjectPropertyData NewObj = new();
                                            NewObj.Name = FName.FromString(MenuOutfitAsset, "Navigation");
                                            NewObj.Value = SecondWidgetNaviIdx;
                                            newBtn.Data.Add(NewObj);

                                            deps = newBtn.CreateBeforeSerializationDependencies?.ToList() ?? new List<FPackageIndex>();
                                            deps.Add(SecondWidgetNaviIdx);
                                            newBtn.CreateBeforeSerializationDependencies = deps;



                                        }
                                    }
                                }

                                //Add Bindings
                                for (int i = 0; i < BP_Menu_Outfits.Data.Count; i++)
                                {
                                    if (BP_Menu_Outfits.Data[i].Name.ToString() == "Bindings")
                                    {
                                        oldValues = (PropertyData[])BP_Menu_Outfits.Data[i].RawValue;

                                        var tempOnFocus_Bindable = (StructPropertyData)oldValues[42];
                                        var tempOnClick_Bindable = (StructPropertyData)oldValues[43];
                                        var newBinding1 = (StructPropertyData)tempOnFocus_Bindable.Clone();
                                        var newBinding2 = (StructPropertyData)tempOnClick_Bindable.Clone();

                                        if (newBinding1.Value is List<PropertyData> fields1)
                                        {
                                            var objectName = fields1.FirstOrDefault(x => x.Name.ToString() == "ObjectName");

                                            if (objectName is StrPropertyData str1)
                                            {
                                                str1.Value = FString.FromString("Btn" + btnname + "_" + slotCount);
                                            }
                                        }
                                        if (newBinding2.Value is List<PropertyData> fields2)
                                        {
                                            var objectName = fields2.FirstOrDefault(x => x.Name.ToString() == "ObjectName");

                                            if (objectName is StrPropertyData str2)
                                            {
                                                str2.Value = FString.FromString("Btn" + btnname + "_" + slotCount);
                                            }
                                        }

                                        newValues = new PropertyData[oldValues.Length + 2];
                                        Array.Copy(oldValues, newValues, oldValues.Length);
                                        newValues[^2] = newBinding1;
                                        newValues[^1] = newBinding2;

                                        BP_Menu_Outfits.Data[i].RawValue = newValues;
                                    }
                                }

                                var oldProperties = BP_StructMenu_Outfits.LoadedProperties;
                                var newProperties = new FProperty[oldProperties.Length + 1];

                                //Add UStructData Script
                                var newBtnScript = new FObjectProperty();
                                newBtnScript.Name = FName.FromString(MenuOutfitAsset, "Btn" + btnname + "_" + slotCount);
                                newBtnScript.PropertyClass = BtnBloodScript.PropertyClass;
                                newBtnScript.Flags = BtnBloodScript.Flags;
                                newBtnScript.ElementSize = BtnBloodScript.ElementSize;
                                newBtnScript.PropertyFlags = BtnBloodScript.PropertyFlags;
                                newBtnScript.RawValue = BtnBloodScript.RawValue;
                                newBtnScript.ArrayDim = BtnBloodScript.ArrayDim;
                                newBtnScript.SerializedType = BtnBloodScript.SerializedType;

                                newProperties[^1] = newBtnScript;
                                Array.Copy(oldProperties, newProperties, oldProperties.Length);
                                BP_StructMenu_Outfits.LoadedProperties = newProperties;

                                Console.WriteLine($"Successfully added {name} to the BP_Menu_Outfits as a new button toggle.");
                                Console.WriteLine("Expanding BP_FightingPlayer...");

                                //Apply the Character Model to the New Slot
                                NormalExport PlayerFightingExport = (NormalExport)PlayerAsset["PlayerFighting_GEN_VARIABLE"];
                                List<StructPropertyData> GenderData = new List<StructPropertyData>();
                                for (int i = 0; i < PlayerFightingExport.Data.Count; i++)
                                {
                                    if (PlayerFightingExport.Data[i].Name.ToString() == "m_ByGenderData")
                                    {
                                        GenderData.Add((StructPropertyData)PlayerFightingExport.Data[i]);
                                    }
                                }

                                //Clone the Males First Outfit slot to clone it before applying our changes.
                                var maleoutfits = (ArrayPropertyData)GenderData[0].Value[0];
                                var femaleoutfits = (ArrayPropertyData)GenderData[1].Value[0];

                                var template = (StructPropertyData)maleoutfits.Value[0];
                                var newOutfit = (StructPropertyData)template.Clone();
                                var softPath = (FSoftObjectPath)newOutfit.Value[0].RawValue;

                                //Correct the Mesh Pathway
                                var newMesh = male?.Replace("Sifu/Content/", "/Game/") ?? string.Empty;
                                int lastIndex = newMesh.LastIndexOf('/');
                                string FileName = lastIndex != -1 ? newMesh.Substring(lastIndex + 1) : newMesh;
                                newMesh += "." + FileName;

                                //Apply New Model Pathway
                                softPath.AssetPath.AssetName = FName.FromString(PlayerAsset, newMesh);
                                newOutfit.Value[0].RawValue = softPath;

                                //Apply New Array with the additional slot
                                var Gender_oldValues = maleoutfits.Value;
                                var Gender_newValues = new PropertyData[Gender_oldValues.Length + 1];
                                Array.Copy(Gender_oldValues, Gender_newValues, Gender_oldValues.Length);
                                Gender_newValues[^1] = newOutfit;
                                maleoutfits.Value = Gender_newValues;

                                Console.WriteLine($"Successfully added {name} to the BP_FightingPlayer as a new Male model with path:\n{newMesh}");

                                //Clone the Males First Outfit slot to clone it before applying our changes.
                                newOutfit = (StructPropertyData)template.Clone();
                                softPath = (FSoftObjectPath)newOutfit.Value[0].RawValue;

                                //Correct the Mesh Pathway
                                newMesh = female?.Replace("Sifu/Content/", "/Game/") ?? string.Empty;
                                lastIndex = newMesh.LastIndexOf('/');
                                FileName = lastIndex != -1 ? newMesh.Substring(lastIndex + 1) : newMesh;
                                newMesh += "." + FileName;

                                //Apply New Model Pathway
                                softPath.AssetPath.AssetName = FName.FromString(PlayerAsset, newMesh);
                                newOutfit.Value[0].RawValue = softPath;

                                //Apply New Array with the additional slot
                                Gender_oldValues = femaleoutfits.Value;
                                Gender_newValues = new PropertyData[Gender_oldValues.Length + 1];
                                Array.Copy(Gender_oldValues, Gender_newValues, Gender_oldValues.Length);
                                Gender_newValues[^1] = newOutfit;
                                femaleoutfits.Value = Gender_newValues;

                                Console.WriteLine($"Successfully added {name} to the BP_FightingPlayer as a new Female model with path:\n{newMesh}");
                                Console.WriteLine();

                                ModifiedAssets = true;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("The 'Slots' property is not an array in the 'OutfitsScroll' export somehow...");
                        Proceed();
                        return;
                    }

                    Console.WriteLine("Finished Searching Through JSONs");
                }
                else
                {
                    Console.WriteLine("The 'SlotsToAdd' directory is empty so there's nothing to add.");
                    if (ModifiedAssets)
                    {
                        Console.WriteLine();
                    }
                    else
                    {
                        Proceed();
                        return;
                    }
                }

                //Save Assets and Compile Pak
                if (ModifiedAssets)
                {
                    Console.WriteLine("Saving Assets...");
                    Console.WriteLine();

                    if (!Directory.Exists(ModOutfitButtonPath))
                    {
                        Directory.CreateDirectory(ModOutfitButtonPath);
                    }
                    if (!Directory.Exists(ModFightingPlayerPath))
                    {
                        Directory.CreateDirectory(ModFightingPlayerPath);
                    }

                    try
                    {
                        //Finished so lets save.
                        MenuOutfitAsset.Write(Path.Combine(ModOutfitButtonPath, "BP_Menu_Outfits.uasset"));
                        PlayerAsset.Write(Path.Combine(ModFightingPlayerPath, "BP_FightingPlayer.uasset"));

                        //Generate Pak with UnrealPak
                        if (File.Exists(UnrealPakPath))
                        {
                            Console.WriteLine($"Detected UnrealPak, Generating Pak...");
                            Console.WriteLine();

                            string FileListPath = Path.Combine(Directory.GetCurrentDirectory(), "filelist.txt");

                            //Create FilePath.txt so UnrealPak can generate Pak
                            if (!File.Exists(FileListPath))
                            {
                                string FileListContent = "\"" + Directory.GetCurrentDirectory() + "\\" + FinalizedModFolder + "\\*.*\" \"..\\..\\..\\*.*\"";

                                try
                                {
                                    File.WriteAllText(FileListPath, FileListContent);
                                }
                                catch
                                {
                                    Console.WriteLine("Failed to Create Filelist.txt?");
                                    Proceed();
                                    return;
                                }
                            }

                            //Generate Pak
                            ProcessStartInfo startInfo = new ProcessStartInfo();
                            startInfo.FileName = "UnrealPak.exe";
                            startInfo.Arguments = $"\"{PakPath}\" -create=\"{FileListPath}\" -compress";
                            using (var process = Process.Start(startInfo))
                            {
                                if (process != null)
                                {
                                    process.WaitForExit();
                                }
                                else
                                {
                                    Console.WriteLine("UnrealPak failed to launch...?");
                                    Proceed();
                                }
                            }

                            //Copy over .sig
                            string SigPath = Path.Combine(OrigAssetFolder, "pakchunk0-WindowsNoEditor.sig");

                            if (File.Exists(SigPath))
                            {
                                if (!File.Exists(NewSig))
                                {
                                    try
                                    {
                                        File.Copy(SigPath, NewSig, false);
                                    }
                                    catch
                                    {
                                        Console.WriteLine("Failed to Copy Detected .sig... Possibly a lack of storage?");
                                        Proceed();
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        Console.WriteLine("Failed to Save Assets... Possibly a lack of storage?");
                        Proceed();
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Nothing was modified. I don't know how you got to the end of this function without the return being triggered...");
                    Proceed();
                    return;
                }

                if (File.Exists(PakPath))
                {
                    if (Directory.Exists(OutputFolder))
                    {
                        Directory.Delete(OutputFolder, true);
                    }

                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine();
                    if (File.Exists(NewSig))
                    {
                        Console.WriteLine("SifuCustomWardrobe was Successfully Compiled!\n\n" +
                                          "Install both the SifuCustomWardrobe_9_P.pak and .sig into your \"Sifu\\Sifu\\Content\\Paks\\~mods\" folder.\n" +
                                          "If you don't already have a \"~mods\" folder, create it.");
                    }
                    else
                    {
                        Console.WriteLine("SifuCustomWardrobe was Successfully Compiled!\n\n" +
                                          "Install the SifuCustomWardrobe_9_P.pak into your \"Sifu\\Sifu\\Content\\Paks\\~mods\" folder.\n" +
                                          "If you don't already have a \"~mods\" folder, create it.\n\n" +
                                          "Since you for some reason, deleted the pakchunk0-WindowsNoEditor.sig in \"ORIGINALASSETS\"\n" +
                                          "You'll have to clone your own game .sig, and rename it to SifuCustomWardrobe_9_P.sig\n" +
                                          "Ensure that the .sig is in the same spot as the mod .Pak");
                    }
                    Console.WriteLine();
                }
                else
                {
                    if (Directory.Exists(OutputFolder) &&
                        File.Exists(Path.Combine(ModOutfitButtonPath, "BP_Menu_Outfits.uasset")) &&
                        File.Exists(Path.Combine(ModFightingPlayerPath, "BP_FightingPlayer.uasset")))
                    {
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine("SifuCustomWardrobe Assets Successfully Created!\n\n" +
                                          "Download UnrealPak and convert the folder into a .Pak\n" +
                                          "Insert the Compiled .Pak into your \"Sifu\\Sifu\\Content\\Paks\\~mods\" folder.\n" +
                                          "If you don't already have a \"~mods\" folder, create it.\n" +
                                          "After that, you'll have to clone your own game .sig, and rename it to SifuCustomWardrobe_9_P.sig\n\n" +
                                          "If you include the UnrealPak.exe in this folder, the .Pak and .Sig will automatically be handled for you if possible.");
                        Console.WriteLine();
                    }
                }

                Console.WriteLine("Sifu Wardrobe Manager was created by Kuroha Saenoki");
                Proceed();
            }
            else
            {
                Console.WriteLine("One or more necessary files are missing. Please ensure the following files exist:");
                foreach (var file in NecessaryFiles)
                {
                    if (!File.Exists(file))
                    {
                        Console.WriteLine(file);
                    }
                }
                Proceed();
                return;
            }
        }

        private static void ClearFolder(string StrPath)
        {
            if (Directory.Exists(StrPath))
            {
                foreach (string file in Directory.GetFiles(StrPath))
                {
                    File.Delete(file);
                }
                foreach (string dir in Directory.GetDirectories(StrPath))
                {
                    Directory.Delete(dir, true);
                }
            }
        }
        private static string[] GatherFiles(string StrPath, string Extension)
        {
            if (!Directory.Exists(StrPath))
            {
                Directory.CreateDirectory(StrPath);
            }
            return Directory.GetFiles(StrPath, Extension);
        }
        static void Proceed()
        {
            Console.WriteLine("Press any key to continue . . .");
            Console.ReadLine();
        }
        static void AllowUserToUnlockOutfits(UAsset AssetToModify, NormalExport ExportToLoad)
        {
            //Modify Current Slots to make them unlocked by default
            Console.WriteLine("Unlock All Vanilla Costumes? [Y/N] (Default Value: N)");
            string? UnlockOutfitsStr = Console.ReadLine();
            Console.WriteLine();
            if (UnlockOutfitsStr == "Y" || UnlockOutfitsStr == "y")
            {
                if (ExportToLoad["Slots"] is ArrayPropertyData arrProp)
                {
                    foreach (ObjectPropertyData prop in arrProp.Value)
                    {
                        NormalExport CurrentObjExport = (NormalExport)AssetToModify.Exports[prop.Value.Index - 1];

                        if (CurrentObjExport?.Data.FirstOrDefault(x => x.Name.ToString() == "Content") is ObjectPropertyData ContentObjProp)
                        {
                            CurrentObjExport = (NormalExport)AssetToModify.Exports[ContentObjProp.Value.Index - 1];
                            int UnlockBoolNum = -1;
                            int UnlockTagNum = -1;

                            //Loop backwards through the Exports to safely remove data
                            for (int i = CurrentObjExport.Data.Count - 1; i >= 0; i--)
                            {

                                if (CurrentObjExport.Data[i] is BoolPropertyData boolProp && boolProp.Name.ToString() == "Locked")
                                {
                                    UnlockBoolNum = i;
                                }
                                if (CurrentObjExport.Data[i] is StructPropertyData StructProp && StructProp.Name.ToString() == "UnlockTag")
                                {
                                    UnlockTagNum = i;
                                }
                            }
                            if (UnlockBoolNum > -1 || UnlockTagNum > -1)
                            {
                                Console.WriteLine($"======== Modifying Costume Slot: {CurrentObjExport.ObjectName} ========");

                                //If both of them are going to be edited, which should always happen, +1 to the TagNum since after Removing one, the offsets would change
                                if (UnlockBoolNum != -1 && UnlockTagNum != -1)
                                {
                                    UnlockTagNum--;
                                }

                                if (UnlockBoolNum > -1)
                                {
                                    CurrentObjExport.Data.RemoveAt(UnlockBoolNum);
                                }
                                if (UnlockTagNum > -1)
                                {
                                    CurrentObjExport.Data.RemoveAt(UnlockTagNum);
                                }

                                ModifiedAssets = true;
                                UnlockBoolNum = -1;
                                UnlockTagNum = -1;
                                Console.WriteLine($"Removed Locked States from {CurrentObjExport.ObjectName}");
                                Console.WriteLine();
                            }
                        }
                    }
                }
            }
        }
        
        public class NaturalStringComparer : IComparer<string?>
        {
            private static readonly Regex ChunkRegex = new Regex(@"(\d+)|(\D+)", RegexOptions.Compiled);

            public int Compare(string? x, string? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                if (x == y) return 0;

                var chunksX = ChunkRegex.Matches(x);
                var chunksY = ChunkRegex.Matches(y);
                int minLength = Math.Min(chunksX.Count, chunksY.Count);

                for (int i = 0; i < minLength; i++)
                {
                    string strX = chunksX[i].Value;
                    string strY = chunksY[i].Value;

                    if (char.IsDigit(strX[0]) && char.IsDigit(strY[0]))
                    {
                        if (long.TryParse(strX, out long numX) && long.TryParse(strY, out long numY))
                        {
                            int numCompare = numX.CompareTo(numY);
                            if (numCompare != 0) return numCompare;
                        }
                        else
                        {
                            int lenCompare = strX.Length.CompareTo(strY.Length);
                            if (lenCompare != 0) return lenCompare;

                            int valCompare = string.Compare(strX, strY, StringComparison.Ordinal);
                            if (valCompare != 0) return valCompare;
                        }
                    }
                    else
                    {
                        int textCompare = string.Compare(strX, strY, StringComparison.CurrentCultureIgnoreCase);
                        if (textCompare != 0) return textCompare;
                    }
                }
                return chunksX.Count.CompareTo(chunksY.Count);
            }
        }
        private static string ReturnFileNameWithoutSort(string input = "")
        {
            // Matches one or more digits at the start of the string
            Match match = Regex.Match(input, @"^\d+");

            if (match.Success)
            {
                return match.Value + "_";
            }

            return "";
        }
        static void ParseJSON(string filepath)
        {
            name = "";
            desc = "";
            male = "";
            female = "";
            pakfile = "";

            JObject Json = JObject.Parse(File.ReadAllText(filepath));

            bool[] NecessaryKeys = {
                !string.IsNullOrEmpty((string?)Json.SelectToken("Name")),
                !string.IsNullOrEmpty((string?)Json.SelectToken("Model.Male")),
                !string.IsNullOrEmpty((string?)Json.SelectToken("Model.Female"))
            };

            bool allKeysExist = NecessaryKeys.All(keyExists => keyExists);
            if (allKeysExist)
            {
                name = (string?)Json.SelectToken("Name");
                if (Json.SelectToken("Description") != null)
                {
                    desc = (string?)Json.SelectToken("Description");
                }
                male = (string?)Json.SelectToken("Model.Male");
                female = (string?)Json.SelectToken("Model.Female");
                if (Json.SelectToken("Pak") != null)
                {
                    pakfile = (string?)Json.SelectToken("Pak");
                }
            }
            else
            {
                if (string.IsNullOrEmpty((string?)Json.SelectToken("Name")))
                {
                    Console.WriteLine("Key 'Name' is missing or empty.");
                }
                if (string.IsNullOrEmpty((string?)Json.SelectToken("Model.Male")))
                {
                    Console.WriteLine("Key 'Model.Male' is missing or empty.");
                }
                if (string.IsNullOrEmpty((string?)Json.SelectToken("Model.Female")))
                {
                    Console.WriteLine("Key 'Model.Female' is missing or empty.");
                }
                Console.WriteLine($"This file will be skipped.");
                return;
            }
        }
        static void ModifyNaviWidgets(UAsset CurrentAsset, NormalExport WidgetNavi, string Direction, string WidgetToFocus, string NewDirection = "")
        {
            if (CurrentAsset != null && WidgetNavi != null)
            {
                if (!WidgetNavi.Data.Any(x => x.Name.ToString() == Direction))
                {
                    Direction = NewDirection;
                }

                if (WidgetNavi.Data.FirstOrDefault(x => x.Name.ToString() == Direction) is StructPropertyData DirectionStruct)
                {
                    if (NewDirection != "" && NewDirection != Direction)
                    {
                        DirectionStruct.Name = FName.FromString(CurrentAsset, NewDirection);
                    }
                    if (DirectionStruct.Value.FirstOrDefault(x => x.Name.ToString() == "Rule") is EnumPropertyData RuleEnum)
                    {
                        RuleEnum.Value = FName.FromString(CurrentAsset, "EUINavigationRule::Explicit");
                    }
                    if (DirectionStruct.Value.FirstOrDefault(x => x.Name.ToString() == "WidgetToFocus") is NamePropertyData WidgetToFocusName)
                    {
                        WidgetToFocusName.Value = FName.FromString(CurrentAsset, WidgetToFocus);
                    }
                }
            }
        }
    }

}
