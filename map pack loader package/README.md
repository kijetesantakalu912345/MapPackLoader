# Map Pack Loader
Map Pack Loader is a mod for automatically installing a zip of map files (a map pack) without needing to get the user to manually set them up. Any maps in the maps folder that are not part of the map pack are automatically moved.
Map Pack loader will also handle updating the maps folder when the `mappack.zip` file is updated, moving any removed/outdated maps to another folder, updating outdated maps, and adding new maps that weren't already there.  
  
This mod was originally made for the bopl battle 2 tournament server, but anybody is free to use it for their own mod packs.

## Usage
1. Install map maker and map pack loader.
2. Put the map files you want to use in a zip file and name it `mappack.zip`.
3. Put `mappack.zip` in `/Bopl Battle/BepInEx/Plugins/`.

To change the list of maps, update the mod pack with an updated `mappack.zip` and Map Pack Loader will automatically update the maps folder the next time the game is run.

https://github.com/kijetesantakalu912345/MapPackLoader
