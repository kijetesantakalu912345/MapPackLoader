using System.IO.Compression;
using BepInEx;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapPackLoader
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        //private readonly Harmony harmony = new Harmony("com.kijetesantakalu.MapPackLoader");
        public void Awake()
        {
            string pluginsFolder = Path.Combine(Directory.GetCurrentDirectory(), "BepInEx/Plugins");
            Logger.LogInfo(pluginsFolder);
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            using var mapPackZip = ZipFile.OpenRead(Path.Combine(Paths.PluginPath, "mappack.zip"));
            string mapsFolderPath = "";
            if (Directory.Exists("Maps"))
            {
                mapsFolderPath = Path.Combine(pluginsFolder, "Maps");
            }
            else
            {
                // WAIT, we need to back track to bepinex/plugins in case this mod was also put into its own folder.
                List<string> mapLoaderDLLPath = RecursivelySearchDirectoryForFile(pluginsFolder, "MapMaker.dll");
                //Directory.GetFiles(pluginsFolder, "MapMaker.dll", SearchOption.AllDirectories);
                if (mapLoaderDLLPath.Count != 1) // if we didn't find anything or found multiple copies of the DLL
                {
                    throw new Exception("Map pack not set up! Found " + mapLoaderDLLPath.Count.ToString() + " copies of mapmaker in the plugins folder. Make sure you have "
                        + "exactly one installed.");
                }
                mapsFolderPath = Path.Combine(mapLoaderDLLPath[0], "Maps");
            }
            Logger.LogInfo("maps folder: " + mapsFolderPath);
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
}
