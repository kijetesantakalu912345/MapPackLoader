using System.IO.Compression;
using BepInEx;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using MiniJSON;
using BepInEx.Logging;
using System.Text;


namespace MapPackLoader
{
    [BepInPlugin("com.kijetesantakalu912345.MapPackLoader", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static string startTimeString = DateTime.Now.ToString("yyyy-MMM-dd hhtt ss.fff");
        public void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            // im just gonna recursively search for mappack.zip from bepinex/plugins. not ideal but it looks like i need to place it there to upload this mod to thunderstore and
            // mod managers will put every mod in their own folder for bepinex to load. so when custom a mappack.zip file is included with mods packs it be at
            // `bepinex/plugins/modpack-name/mappack.zip`.
            List<string> mapPackZipPaths = RecursivelySearchDirectoryForFile(Paths.PluginPath, "mappack.zip");

            if (mapPackZipPaths.Count == 0)
            {
                Logger.LogError("No mappack.zip found! map pack loader will not load any map pack.");
                return;
            }
            if (mapPackZipPaths.Count > 1)
            {
                Logger.LogWarning("multiple `mappack.zip`s found! The mappack.zip at `" + mapPackZipPaths[0] + "` will be used. paths of all map packs:");
                string paths = "";
                foreach (string zipPath in mapPackZipPaths)
                {
                    paths += zipPath + "\n";
                }
                Logger.LogWarning(paths);
            }

            string mapPackZipPath = mapPackZipPaths[0];

            using ZipArchive mapPackZip = ZipFile.OpenRead(mapPackZipPath);
            string mapsFolderPath = Path.Combine(Paths.PluginPath, "Maps");
            Logger.LogInfo("maps folder: " + mapsFolderPath);

            if (!Directory.Exists(mapsFolderPath)) { Directory.CreateDirectory(mapsFolderPath); }

            // for security reasons we're not doing this! it's less efficient but this would allow somebody to sneak custom DLLs from mappack.zip if the maps folder path was empty.
            /*if (Directory.GetFiles(mapsFolderPath).Length == 0) // empty maps folder, we can just freely extract the mappack zip into it
            {
                mapPackZip.ExtractToDirectory(mapsFolderPath);
                return;
            }*/
            // maybe there could be a metadata file like a json or something for this but im trying to keep this as simple as possible for the user and im not making a UI for
            // creating map packs. i just want map packs to be a zip of maps that are pasted into the maps folder as needed.
            CheckAndUpdateOldMaps(mapPackZip, mapsFolderPath);
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


        public void CheckAndUpdateOldMaps(ZipArchive mapPackZip, string mapsFolderPath)
        {
            Dictionary<string, ZippedMap> packMaps = new Dictionary<string, ZippedMap>(); // Dictionary<mapID, ZippedMap>

            for (int i = 0; i < mapPackZip.Entries.Count; i++)
            {
                // remember that maps are themselves zip files
                /*using*/ ZipArchive mapZip = new ZipArchive(mapPackZip.Entries[i].Open());
                ZipArchiveEntry zippedMetaData = null;
                zippedMetaData = mapZip.GetEntry("MetaData.json");
                if (zippedMetaData == null)
                {
                    throw new Exception("Invalid map \"" + mapPackZip.Entries[i].FullName + "\" in the map pack has no MetaData.json file. Map pack not extracted.");
                }
                string metadataJsonText = new StreamReader(zippedMetaData.Open()).ReadToEnd();
                Dictionary<string, object> metadataJsonDict = MiniJSON.Json.Deserialize(metadataJsonText) as Dictionary<string, object>;
                string mapName = metadataJsonDict["MapName"].ToString();
                uint mapVersion = Convert.ToUInt32(metadataJsonDict["MapVersion"]);
                packMaps.Add(mapName, new ZippedMap(mapName, mapVersion, mapZip, mapPackZip.Entries[i].Name));
            }


            for (int i = 0; i < Directory.GetFiles(mapsFolderPath).Length; i++)
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
                    Logger.LogWarning("Invalid map \"" + mapPackZip.Entries[i].FullName + "\" in your maps folder has no MetaData.json, moving...");
                    MoveMapToNewFolder(mapsFolderPath, currentFilePath);
                    continue;
                }
                string metadataJsonText = new StreamReader(zippedMetaData.Open()).ReadToEnd();
                Dictionary<string, object> metadataJsonDict = MiniJSON.Json.Deserialize(metadataJsonText) as Dictionary<string, object>;
                string mapName = metadataJsonDict["MapName"].ToString();
                uint mapVersion = Convert.ToUInt32(metadataJsonDict["MapVersion"]);

                // ALRIGHT, now we check if this map exists in the mappack and if so, if it's a different version.
                if (packMaps.ContainsKey(mapName))
                {
                    if (packMaps[mapName].version != mapVersion)
                    {
                        mapZip.Dispose();
                        MoveMapToNewFolder(mapsFolderPath, currentFilePath);
                        ExtractMapZipFromMainZip(mapsFolderPath, mapPackZip, packMaps[mapName].mapFileName);
                    }
                    packMaps[mapName].zip.Dispose();
                    packMaps.Remove(mapName);
                }
                else
                {
                    mapZip.Dispose();
                    MoveMapToNewFolder(mapsFolderPath, currentFilePath);
                }
            }

            // ok so now all outdated maps should be extracted and any maps not in the pack should have been moved. now we just need to extract any remaining maps in the pack.
            foreach (ZippedMap packMap in packMaps.Values)
            {
                ExtractMapZipFromMainZip(mapsFolderPath, mapPackZip, packMap.mapFileName);
                packMap.zip.Dispose();
            }
        }

        public void ExtractMapZipFromMainZip(string mapsFolderPath, ZipArchive mapPackZip, string mapFileName)
        {
            string mapPath = Path.Combine(mapsFolderPath, mapFileName);
            mapPackZip.GetEntry(mapFileName).ExtractToFile(mapPath);
        }

        public void MoveMapToNewFolder(string mapsFolder, string mapPath)
        {
            string dirToMoveTo = Path.Combine(Directory.GetParent(mapsFolder).FullName, "maps moved when setting up map pack " + startTimeString);
            if (!Directory.Exists(dirToMoveTo))
            {
                Directory.CreateDirectory(dirToMoveTo);
            }
            Logger.LogInfo(Path.Combine(dirToMoveTo + "/" + Path.GetFileName(mapPath)));
            File.Move(mapPath, Path.Combine(dirToMoveTo + "/" + Path.GetFileName(mapPath)));
        }
    }

    public struct ZippedMap
    {
        public string mapName;
        public string mapFileName;
        public uint version;
        public ZipArchive zip;

        public ZippedMap(string mapName, uint version, ZipArchive zip, string mapFileName)
        {
            this.mapName = mapName;
            this.version = version;
            this.zip = zip;
            this.mapFileName = mapFileName;
        }
    }
}
