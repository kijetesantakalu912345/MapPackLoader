using System.IO.Compression;
using BepInEx;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using MiniJSON;
using BepInEx.Logging;


namespace MapPackLoader
{
    [BepInPlugin("com.kijetesantakalu912345.MapPackLoader", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        //private readonly Harmony harmony = new Harmony("com.kijetesantakalu.MapPackLoader");
        public void Awake()
        {
            string pluginsFolder = Path.Combine(Directory.GetCurrentDirectory(), "BepInEx/Plugins");
            Logger.LogInfo(pluginsFolder);
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            using ZipArchive mapPackZip = ZipFile.OpenRead(Path.Combine(Paths.PluginPath, "mappack.zip"));
            string mapsFolderPath = "";
            if (Directory.Exists("Maps") && File.Exists(Path.Combine(pluginsFolder, "MapMaker.dll")))
            {
                mapsFolderPath = Path.Combine(pluginsFolder, "Maps");
            }
            else
            {
                List<string> mapLoaderDLLPath = RecursivelySearchDirectoryForFile(pluginsFolder, "MapMaker.dll");
                //Directory.GetFiles(pluginsFolder, "MapMaker.dll", SearchOption.AllDirectories);
                if (mapLoaderDLLPath.Count != 1) // if we didn't find anything or found multiple copies of the DLL
                {
                    throw new Exception("Found " + mapLoaderDLLPath.Count.ToString() + " copies of mapmaker in the plugins folder. Make sure you have exactly 1 installed."
                        + " Map pack not extracted.");
                }
                mapsFolderPath = Path.Combine(mapLoaderDLLPath[0], "Maps");
            }
            Logger.LogInfo("maps folder: " + mapsFolderPath);

            if (!Directory.Exists(mapsFolderPath)) { Directory.CreateDirectory(mapsFolderPath); }

            if (Directory.GetFiles(mapsFolderPath).Length == 0) // empty maps folder, we can just freely extract the mappack zip into it
            {
                mapPackZip.ExtractToDirectory(mapsFolderPath);
                return;
            }
            // maybe there should be a metadata file like a json or something for this but im trying to keep this as simple as possible for the user and im not making a UI for
            // creating map packs. i just want map packs to be a zip of maps that are pasted into the maps folder if needed.
            //CheckAndUpdateOldMaps()

        }

        public void CheckAndUpdateOldMaps(ZipArchive mapPackZip, string mapsFolderPath)
        {
            /*List<int> packMapIDs = new List<int>(); 
            List<uint> packMapVersions = new List<uint>();
            List<int> folderMapIDs = new List<int>();
            List<uint> folderMapVersions = new List<uint>();*/

            Dictionary<int, ZippedMap> packMaps = new Dictionary<int, ZippedMap>(); // Dictionary<mapID, ZippedMap>
            Dictionary<int, ZippedMap> validFolderMaps = new Dictionary<int, ZippedMap>(); // Dictionary<mapID, ZippedMap>
            List<string> pathsOfFolderMapsToMove = new List<string>();

            // all of the maps in the mappack zip.
            for (int i = 0; i < mapPackZip.Entries.Count; i++) 
            {
                // remember that maps are themselves zip files
                using ZipArchive mapZip = new ZipArchive(mapPackZip.Entries[i].Open());
                ZipArchiveEntry zippedMetaData = null;
                zippedMetaData = mapZip.GetEntry("MetaData.json");
                if (zippedMetaData == null)
                {
                    throw new Exception("Invalid map \"" + mapPackZip.Entries[i].FullName + "\" in the map pack has no MetaData.json file. Map pack not extracted.");
                }
                string metadataJsonText = new StreamReader(zippedMetaData.Open()).ReadToEnd();
                Dictionary<string, object> metadataJsonDict = MiniJSON.Json.Deserialize(metadataJsonText) as Dictionary<string, object>;

                // i don't think UUIDs work like this (aren't they 128 bit?) but it's what mapmaker calls them
                int mapID = Convert.ToInt32(metadataJsonDict["MapUUID"]);
                uint mapVersion = Convert.ToUInt32(metadataJsonDict["MapVersion"]);
                packMaps.Add(mapID, new ZippedMap(mapID, mapVersion, mapZip));
            }

            // now we read all of the maps in the folder.
            // this feels kinda spaghetti.
            for (int i = 0; i < Directory.GetFiles(mapsFolderPath).Length; i++)
            {
                try
                {
                    string currentFilePath = Directory.GetFiles(mapsFolderPath)[i];
                    if (Path.GetExtension(currentFilePath) != ".zip")
                    {
                        continue;
                    }
                    using ZipArchive mapZip = new ZipArchive(File.OpenRead(currentFilePath));
                    ZipArchiveEntry zippedMetaData = null;
                    zippedMetaData = mapZip.GetEntry("MetaData.json");
                    if (zippedMetaData == null)
                    {
                        // we'll just move invalid maps that are inside the maps folder.
                        Logger.LogWarning("Invalid map \"" + mapPackZip.Entries[i].FullName + "\" in your maps folder has no MetaData.json");
                        pathsOfFolderMapsToMove.Add(currentFilePath);
                        continue;
                    }
                    string metadataJsonText = new StreamReader(zippedMetaData.Open()).ReadToEnd();
                    Dictionary<string, object> metadataJsonDict = MiniJSON.Json.Deserialize(metadataJsonText) as Dictionary<string, object>;
                    // again i don't think UUIDs work like that but whatever fine
                    int mapID = Convert.ToInt32(metadataJsonDict["MapUUID"]);
                    uint mapVersion = Convert.ToUInt32(metadataJsonDict["MapVersion"]);
                    validFolderMaps.Add(mapID, new ZippedMap(mapID, mapVersion, currentFilePath));
                }
                catch (Exception ex)
                {
                    Logger.LogError("Encountered an error while trying to read maps in the maps folder. This map will be moved. Error message: \n" + ex.Message);
                    pathsOfFolderMapsToMove.Add(Directory.GetFiles(mapsFolderPath)[i]);
                }
            }

            // ok we *finally* have the map data.



            // get all map "UU"IDs
            // compare all map "UU"IDs
            // move maps that don't exist in the zip to another folder, probably just in a separate folder that's one level up from the maps folder?
            // - I could also delete them but deleting them feels sketchy, espeically because there'll be no warning. "oops! I hope that wasn't the only copy of the map file on
            // your computer! because the map pack mod just deleted everything in the maps folder without warning before pasting in the maps from the map pack zip."
            // for maps with corosponding "UU"IDs, check if the map versions match.
            // if there's a mismatch, move the old map into a separate folder and paste the zip's version of the map into the folder.
            // - check for a *mismatch*, NOT for the version specifically being higher.
            // otherwise, if they do match, then leave the file as is.
            // MAYBE JUST BACKUP THE WHOLE MAPS FOLDER WHEN THERE ARE NEW UPDATES? ehh, idk though. I'd prefer to avoid that if possible.

        }

        public static List<string> RecursivelySearchDirectoryForFile(string searchPath, string containedFileName)
        {
            List<string> foundFiles = new List<string>();
            string[] filesInDir = Directory.GetFiles(searchPath);
            for (int i = 0; i < filesInDir.Count(); i++)
            {
                if (Path.GetFileName(filesInDir[i]).Contains(containedFileName))
                {
                    foundFiles.Add(filesInDir[i]);
                }
            }
            string[] directories = Directory.GetDirectories(searchPath);
            for (int i = 0; i < directories.Count(); i++)
            {
                foundFiles.AddRange(RecursivelySearchDirectoryForFile(directories[i], containedFileName));
            }
            return foundFiles;
        }
    }

    public struct ZippedMap
    {
        public int ID;
        public uint version;
        public ZipArchive zip; // this or path will be null.
        public string path;

        public ZippedMap(int ID, uint version, ZipArchive zip)
        {
            this.ID = ID;
            this.version = version;
            this.zip = zip;
        }
        public ZippedMap(int ID, uint version, /*ZipArchive zip, */string path)
        {
            this.ID = ID;
            this.version = version;
            //this.zip = zip;
            this.path = path;
        }
    }
}
