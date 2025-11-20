namespace TerrainGenerators.Helpers
{
    public class RNG
    {
        public int Seed;
        public int Index;
        private System.Random rng;

        public RNG(int seed)
        {
            Seed = seed;
            rng = new System.Random(seed);
        }

        public int Next(int bound1, int bound2)
        {
            if (bound1 < bound2)
                return rng.Next(bound1, bound2);
            else
                return rng.Next(bound2, bound1);
        }
        public float Next(float bound1, float bound2)
        {
            return (float)Next((double)bound1, bound2);
        }

        public double Next(double bound1, double bound2)
        {
            if (bound1 < bound2)
                return rng.NextDouble() * (bound2 - bound1) + bound1;
            else
                return rng.NextDouble() * (bound1 - bound2) + bound2;
        }
    }
}