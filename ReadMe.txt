Replaces the level generator for planets with custom procedural terrain generators depending on the planet type.


To build this project without modifying the .csproj file you need to add a file called "GamePaths.xml" to the folder ABOVE this one, with the following content:

<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <!-- Set this full path to your game folder. Must contain a slash at the end. -->
    <GamePath>C:\Program Files (x86)\Steam\steamapps\common\Roguelands\</GamePath>

    <!-- Set this partial path to the game's Managed folder. Must contain a slash at the end. -->
    <ManagedFolder>Roguelands_Data\Managed\</ManagedFolder>
  </PropertyGroup>
</Project>

Note that `GamePath` may need to be changed, depending on the nature of your installation.
