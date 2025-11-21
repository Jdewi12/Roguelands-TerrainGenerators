using System.Collections.Generic;

namespace TerrainGenerators.Helpers
{
    public class GridNode
    {
        public Vector2Int Position;
        public int Radius;
        public List<GridNode> Connections;

        public GridNode(Vector2Int position, int radius, List<GridNode> connections = null)
        {
            this.Position = position;
            this.Radius = radius;
            if (connections != null)
                this.Connections = connections;
            else
                this.Connections = new List<GridNode>();
        }
    }
}
