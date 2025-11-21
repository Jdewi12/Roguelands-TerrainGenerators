namespace TerrainGenerators.Helpers
{
    public class OneWayGridNode
    {
        public Vector2Int Position;
        public int Radius;
        public OneWayGridNode Parent;

        public OneWayGridNode(Vector2Int position, int radius, OneWayGridNode connection = null)
        {
            this.Position = position;
            this.Radius = radius;
            this.Parent = connection;
        }
    }
}
