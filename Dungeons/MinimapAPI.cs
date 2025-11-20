using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TerrainGenerators
{
    internal class MinimapAPI
    {
        public void OverrideMinimap(Texture2D map, float leftWorld, float rightWorld, float topWorld, float botWorld, int mapViewportWidth = 0, int mapViewPortHeight = 0)
        {
            Minimap.Minimap.minimap = map;
            Minimap.Minimap.overrideMinimap = true;
            Minimap.Minimap.leftWorld = leftWorld;
            Minimap.Minimap.rightWorld = rightWorld;
            Minimap.Minimap.topWorld = topWorld;
            Minimap.Minimap.botWorld = botWorld;
            if (mapViewportWidth != 0 && mapViewPortHeight != 0)
                Minimap.Minimap.overrideMinimapSize = new GadgetCore.Util.Tuple<int, int>(mapViewportWidth, mapViewPortHeight);
            else
                Minimap.Minimap.overrideMinimapSize = null;
        }

    }
}
