using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using System.Runtime.InteropServices;

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

        static void Main(string[] args)
        {
            string[] NecessaryFiles = {
                Path.Combine(Directory.GetCurrentDirectory(), "ORIGINALASSETS", "BP_Menu_Outfits.uasset"),
                Path.Combine(Directory.GetCurrentDirectory(), "ORIGINALASSETS", "BP_Menu_Outfits.uexp"),
                Path.Combine(Directory.GetCurrentDirectory(), "ORIGINALASSETS", "BP_FightingPlayer.uasset"),
                Path.Combine(Directory.GetCurrentDirectory(), "ORIGINALASSETS", "BP_FightingPlayer.uexp"),
            };

            bool DoFilesExist = NecessaryFiles.All(File.Exists);
            if (DoFilesExist)
            {
                UAsset MenuOutfitAsset = new UAsset(NecessaryFiles[0], EngineVersion.VER_UE4_26);
                UAsset PlayerAsset = new UAsset(NecessaryFiles[2], EngineVersion.VER_UE4_26);

                string[] JSONFileArray = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "SlotsToAdd"));

                if (JSONFileArray.Length > 0)
                {
                    Array.Sort(JSONFileArray, new NaturalStringComparer());

                    //BtnBlood and its Slot will be cloned for the new buttons
                    NormalExport BtnBloodExport = (NormalExport)MenuOutfitAsset["Btn_Blood"];
                    NormalExport OutfitsScrollExport = (NormalExport)MenuOutfitAsset["OutfitsScroll"];

                    NormalExport BP_Menu_Outfits = (NormalExport)MenuOutfitAsset["BP_Menu_Outfits_C"];
                    StructExport BP_StructMenu_Outfits = (StructExport)MenuOutfitAsset["BP_Menu_Outfits_C"];
                    FObjectProperty? BtnBloodScript = BP_StructMenu_Outfits.LoadedProperties.FirstOrDefault(x => x.Name.ToString() == "Btn_Blood") as FObjectProperty;

                    if (BtnBloodExport == null || OutfitsScrollExport == null || BP_Menu_Outfits == null || BP_StructMenu_Outfits == null || BtnBloodScript == null)
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
                            ParseJSON(filepath);

                            if (name != "" && male != "" && female != "")
                            {
                                Console.WriteLine($"======== Adding: {name} ========");
                                Console.WriteLine("Expanding BP_Menu_Outfits...");
                                slotCount++;

                                for (int i = 0; i < BtnBloodExport.Data.Count; i++)
                                {
                                    if (BtnBloodExport.Data[i].Name.ToString() == "Slot")
                                    {
                                        bloodSlotIndex = int.Parse(BtnBloodExport.Data[i].RawValue.ToString());
                                        break;
                                    }
                                }

                                NormalExport SlotBloodExport = (NormalExport)MenuOutfitAsset.Exports[bloodSlotIndex];

                                //They need two clones??? or else you get a reference to the same object and it will overwrite the previous one.
                                NormalExport newBtn = (NormalExport)BtnBloodExport.Clone();
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

                                //Edit both WidgetNavigations to use our Btn as the focus point
                                var widgetNavis = MenuOutfitAsset.Exports.OfType<NormalExport>().Where(x => x.ObjectName.ToString() == "WidgetNavigation_0").ToList();
                                NormalExport WidgetNavi1 = widgetNavis[0];
                                NormalExport WidgetNavi2 = widgetNavis[1];

                                for (int i = 0; i < WidgetNavi1.Data.Count; i++)
                                {
                                    if (WidgetNavi1.Data[i].RawValue is List<PropertyData> list)
                                    {
                                        for (int j = 0; j < list.Count; j++)
                                        {
                                            if (list[j].Name.ToString() == "WidgetToFocus")
                                            {
                                                list[j].RawValue = FName.FromString(MenuOutfitAsset, "Btn" + btnname + "_" + slotCount);
                                            }
                                        }
                                    }
                                }
                                for (int i = 0; i < WidgetNavi2.Data.Count; i++)
                                {
                                    if (WidgetNavi2.Data[i].RawValue is List<PropertyData> list)
                                    {
                                        for (int j = 0; j < list.Count; j++)
                                        {
                                            if (list[j].Name.ToString() == "WidgetToFocus")
                                            {
                                                list[j].RawValue = FName.FromString(MenuOutfitAsset, "Btn" + btnname + "_" + slotCount);
                                            }
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
                                var newMesh = male.Replace("Sifu/Content/", "/Game/");
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
                                newMesh = female.Replace("Sifu/Content/", "/Game/");
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
                            }
                        }
                        
                    }
                    else
                    {
                        Console.WriteLine("The 'Slots' property is not an array in the 'OutfitsScroll' export somehow...");
                        Proceed();
                        return;
                    }

                    Console.WriteLine($"Finished Searching Through JSONs, Saving uassets.");

                    //Save UAssets
                    string ModFolder = "SifuCustomWardrobe";
                    string ModBasePath = ModFolder + "\\Sifu\\Content\\";
                    string ModOutfitButtonPath = "UI\\Blueprints\\Menus\\Outfits\\";
                    string ModFightingPlayerPath = "Blueprints\\";
                    ModOutfitButtonPath = Directory.GetCurrentDirectory() + "\\" + ModBasePath + ModOutfitButtonPath;
                    ModFightingPlayerPath = Directory.GetCurrentDirectory() + "\\" + ModBasePath + ModFightingPlayerPath;

                    if (!Directory.Exists(ModOutfitButtonPath))
                    {
                        Directory.CreateDirectory(ModOutfitButtonPath);
                    }
                    if (!Directory.Exists(ModFightingPlayerPath))
                    {
                        Directory.CreateDirectory(ModFightingPlayerPath);
                    }

                    //Finished so lets save.
                    MenuOutfitAsset.Write(Path.Combine(ModOutfitButtonPath, "BP_Menu_Outfits.uasset"));
                    PlayerAsset.Write(Path.Combine(ModFightingPlayerPath, "BP_FightingPlayer.uasset"));

                    Thread.Sleep(1000);

                    //Generate Pak with UnrealPak
                    string UnrealPak = Directory.GetCurrentDirectory() + "\\UnrealPak.exe";
                    if (File.Exists(UnrealPak))
                    {
                        Console.WriteLine($"Detected UnrealPak, Generating Pak...");
                        Console.WriteLine();

                        string PakPath = Directory.GetCurrentDirectory() + "\\" + ModFolder + "_9_P.pak";
                        string FileListPath = Directory.GetCurrentDirectory() + "\\filelist.txt";
                        
                        //Create FilePath.txt so UnrealPak can generate Pak
                        if (!File.Exists(FileListPath))
                        {
                            string FileListContent = "\"" + Directory.GetCurrentDirectory() + "\\" + ModFolder + "\\*.*\" \"..\\..\\..\\*.*\"";
                            File.WriteAllText(FileListPath, FileListContent);
                        }

                        //Generate Pak
                        ProcessStartInfo startInfo = new ProcessStartInfo();
                        startInfo.FileName = "UnrealPak.exe";
                        startInfo.Arguments = $"\"{PakPath}\" -create=\"{FileListPath}\" -compress";
                        Process.Start(startInfo);

                        //Copy over .sig
                        string SigPath = Directory.GetCurrentDirectory() + "\\ORIGINALASSETS\\pakchunk0-WindowsNoEditor.sig";
                        string NewSig = Directory.GetCurrentDirectory() + "\\" + ModFolder + "_9_P.sig";

                        if (File.Exists(SigPath))
                        {
                            if (!File.Exists(NewSig))
                            {
                                File.Copy(SigPath, NewSig, false);
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("The 'SlotsToAdd' directory is empty so there's nothing to add.");
                    Proceed();
                    return;
                }
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
            }
            Thread.Sleep(1000);
            Console.WriteLine();
            Console.WriteLine("Sifu Wardrobe Manager was created by Kuroha Saenoki");
            Proceed();
        }

        static void ParseJSON(string filepath)
        {
            name = "";
            desc = "";
            male = "";
            female = "";

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

        static void Proceed()
        {
            Console.WriteLine("Press any key to continue . . .");
            Console.ReadLine();
        }

        public class NaturalStringComparer : IComparer<string>
        {
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            private static extern int StrCmpLogicalW(string psz1, string psz2);

            public int Compare(string x, string y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                return StrCmpLogicalW(x, y);
            }
        }
    }
}
