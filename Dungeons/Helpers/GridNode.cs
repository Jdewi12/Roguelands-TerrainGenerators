using System.Collections.Generic;

namespace TerrainGenerators.Helpers
{
    public class GridNode
    {
        public Vector2Int Position;
        public int Radius;
        public List<GridNode> Connections;

        public GridNode(Vector2Int position, int radius = 0, List<GridNode> connections = null)
        {
            this.Position = position;
            this.Radius = radius;
            if (connections != null)
                this.Connections = connections;
            else
                this.Connections = new List<GridNode>();
        }

        public override string ToString() => $"({Position.x}, {Position.y})";
    }
}
