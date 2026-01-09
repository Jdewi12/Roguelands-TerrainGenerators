using GadgetCore.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TerrainGenerators.Helpers;
using TerrainGenerators.Patches;
using UnityEngine;
using Color = UnityEngine.Color;

namespace TerrainGenerators.Generators
{
    public abstract class GeneratorBase
    {
        public virtual List<Spawnable> AdditionalGroundSpawns { get; } // name, weight, (optional y-offset)
        public virtual List<Spawnable> AdditionalAirSpawns { get; } // name, weight, (optional y-offset)
        public virtual int BlockSize => Patch_SpawnerScript_World.BlockSize;
        public virtual int GridWidth { get; } = 32;
        public virtual int GridHeight { get; } = 24;
        public abstract bool[,] WallsGrid { get; }
        public virtual Color MinimapWallsColor { get => GetMinimapColor(); }
        public abstract Vector2Int PlayerSpawn { get; } // grid position
        public static Vector2 WorldOffset = new Vector2(200, 0); // todo: Move?

        public virtual int MinimapViewportWidth => GridWidth - 3;
        public virtual int MinimapViewportHeight => GridHeight - 3;
        //public virtual List<Vector2Int> SpawnablePositions { get; } = new List<Vector2Int>();
        public virtual List<GameObject> Spawned { get; } = new List<GameObject>();
        public virtual List<Vector2Int> TeleporterPositions { get; } = new List<Vector2Int>();
        public Chunk ChunkScript = null;

        public static readonly FieldInfo NetworkStuffField = typeof(Chunk).GetField("networkStuff", BindingFlags.Instance | BindingFlags.NonPublic);

        public static Color GetMinimapColor()
        {
            int id = SpawnerScript.curBiome;
            switch (id)
            {
                case 0: // Desolate Canyon
                    return new Color(0.3f, 1f, 0.2f);
                case 1: // Deep Jungle
                    return new Color(0.2f, 0.75f, 0f);
                case 2: // Hollow Caverns
                    return new Color(0.75f, 0.2f, 0.75f);
                case 3: // Shroomtown
                    return new Color(1f, 1f, 0.25f);
                case 4: // Ancient Ruins
                    return new Color(0.75f, 0.75f, 0.2f);
                case 5: // Plaguelands
                    return new Color(0.75f, 0.5f, 0.25f);
                case 6: // Byfrost
                    return new Color(0.75f, 0.9f, 0.9f);
                case 7: // Molten Crag
                    return new Color(1f, 1f, 0.25f);
                //case 8: // Mech City
                case 9: // Demon's Rift
                    return new Color(0.75f, 0.3f, 0.9f);
                case 10: // Whisperwood
                    return new Color(0.6f, 0.75f, 0.6f);
                // case 11: // Old Earth
                case 12: // Forbidden Arena
                    return new Color(1f, 1f, 0.25f);
                case 13: // Cathedral
                    return new Color(0.5f, 1f, 0.9f);
                default: // Probably a modded biome
                {
                    if (ModdedTextures.CustomPlanetIDColors.TryGetValue(id, out Color col))
                        return col;
                    // else
                    return new Color(1f, 1f, 1f);

                }
            }
        }

        //public abstract void Generate(RNG rng);

        public abstract void GenerateWalls(RNG rng); // called on server and client

        public abstract void PopulateLevel(RNG rng); // only called on server

        public virtual void CreateMinimapIfPresent(bool[,] wallsGrid, int padding = 3)
        {
            if (TerrainGenerators.MinimapAPI == null)
                return;

            int width = wallsGrid.GetLength(0);
            int height = wallsGrid.GetLength(1);
            int minimapWidth = width + padding * 2;
            int minimapHeight = height + padding * 2;
            Color[] mapGrid = new Color[minimapWidth * minimapHeight];
            for (int x = -padding; x < width + padding; x++)
            {
                for (int y = -padding; y < height + padding; y++)
                {
                    if (wallsGrid.IsWall(x, y))
                    {
                        int index = (x + padding) + (y + padding) * minimapWidth;
                        //TerrainGenerators.Log("wX: " + x + ", wY: " + y + "index: " + index);
                        mapGrid[index] = MinimapWallsColor;
                    }
                }
            }

            TerrainGenerators.Log("Interfacing with Minimap");
            Texture2D minimapTex = new Texture2D(minimapWidth, minimapHeight);
            minimapTex.SetPixels(mapGrid);
            TerrainGenerators.MinimapAPI.OverrideMinimap(minimapTex, 
                leftWorld: WorldOffset.x + (-padding - 0.5f) * BlockSize, 
                rightWorld: WorldOffset.x + (minimapWidth - padding - 0.5f) * BlockSize,
                topWorld: WorldOffset.y + (minimapHeight - padding - 1.5f) * BlockSize, 
                botWorld: WorldOffset.y + (-padding - 1.5f) * BlockSize, 
                mapViewportWidth: MinimapViewportWidth,
                mapViewPortHeight: MinimapViewportHeight);
        }
    

        /// <summary>
        /// x and y are chunk positions, not world positions. Remember to call the original method if you override it, so you can still spawn shared and vanilla resources.
        /// </summary>
        public virtual void SpawnThing(RNG rng, string name, float x, float y, float yOffset = 0.375f)
        {
            if (name == "")
                return;
            else if (name == "shop")
            {
                int stands = 3;
                float xScale = (BlockSize - 2) / stands;
                for (int i = 0; i < stands; i++)
                {
                    if (rng.Next(0, 2) == 0)
                        name = "obj/chipStand";
                    else
                        name = "obj/itemStand";
                    float xOffset = (i + 0.5f - stands / 2f) * xScale;

                    GameObject standToSpawn = (GameObject)Resources.Load(name);
                    Spawned.Add((GameObject)Network.Instantiate(standToSpawn, new Vector3(x * BlockSize + xOffset, (y + 0.5f) * BlockSize + yOffset, 0) + (Vector3)WorldOffset, Quaternion.identity, 0));
                }
                return;
            }
            else
            {
                if (name.StartsWith("haz/haz")) // don't spawn hazards too close to spawn
                {
                    float sqrDistance = Mathf.Pow(((y + 0.5f) * BlockSize + yOffset) - (PlayerSpawn.y * BlockSize + 0.125f), 2) + Mathf.Pow((x * BlockSize) - (PlayerSpawn.x * BlockSize), 2);
                    if (sqrDistance < Mathf.Pow(16 * 4, 2)) // if distance is less than 4 chunks at blockSize 16, or 64 world units
                        return;
                }
                TerrainGenerators.Log("Attempting to spawn vanilla resource: " + name);
                GameObject toSpawn = (GameObject)Resources.Load(name);
                float centreY = 0;
                var rend = toSpawn.GetComponent<Renderer>();
                if (rend != null)
                    centreY = rend.bounds.center.y;
                float z = 0;
                if (name == "npc/ringabolt") //TODO: Other NPCs too?
                {
                    if (SpawnerScript.ringabolt == 0)
                    {
                        SpawnerScript.ringabolt = 1;
                        z = 0.2f;
                        centreY = toSpawn.GetComponentInChildren<Renderer>().bounds.center.y;
                    }
                    else
                    {
                        return;
                    }
                }
                else if (name == "npc/gromwell")
                {
                    if (SpawnerScript.gromwell == 0)
                    {
                        SpawnerScript.gromwell = 1;
                        z = 0.2f;
                        centreY = toSpawn.GetComponentInChildren<Renderer>().bounds.center.y;
                    }
                    else
                    {
                        return;
                    }
                }

                Spawned.Add((GameObject)Network.Instantiate(toSpawn, (Vector3)WorldOffset + new Vector3(x * BlockSize + rng.Next(-(BlockSize / 2f - 2), (BlockSize / 2f - 2)), (y + 0.5f) * BlockSize + centreY + yOffset, z), Quaternion.identity, 0));
            }
        }

        public virtual void CreateWalls(bool[,] wallsGrid, int padding = 3)
        {
            int width = wallsGrid.GetLength(0);
            int height = wallsGrid.GetLength(1);
            for (int x = -padding; x < width + padding; x++)
            {
                for (int y = -padding; y < height + padding; y++)
                {
                    if (wallsGrid.IsWall(x, y))
                    {
                        bool[] tileCombination = GetTileCombination(wallsGrid, x, y);
                        SpawnWall(x, y, ConnectedTextures.GetTexture(tileCombination));
                    }
                }
            }
        }

        public virtual bool[] GetTileCombination(bool[,] wallsGrid, int x, int y)
        {
            //  Tile 0   Tile 1   Tile 2
            //  Tile 3     ME     Tile 4
            //  Tile 5   Tile 6   Tile 7

            return new bool[8]
            {
                wallsGrid.IsWall(x - 1, y + 1),
                wallsGrid.IsWall(x, y + 1),
                wallsGrid.IsWall(x + 1, y + 1),
                wallsGrid.IsWall(x - 1, y),
                wallsGrid.IsWall(x + 1, y),
                wallsGrid.IsWall(x - 1, y -1),
                wallsGrid.IsWall(x, y -1),
                wallsGrid.IsWall(x + 1, y -1),
            };
        }

        public virtual void SpawnWall(int x, int y, Texture2D tex)
        {
            GameObject tile = GameObject.Instantiate(TerrainGenerators.TilePrefab,
                (Vector3)WorldOffset + new Vector3(x * BlockSize, y * BlockSize, 2f/*0.15f*/), Quaternion.Euler(180, 0, 0));
            Spawned.Add(tile);
            tile.transform.localScale = new Vector3(1, 1, 1) * BlockSize / 2;
            tile.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", tex);
        }

        public virtual void SpawnAirSpawnables(RNG rng, bool[,] wallsGrid)
        {
            for (int x = 0; x < wallsGrid.GetLength(0); x++)
            {
                for (int y = 0; y < wallsGrid.GetLength(1); y++)
                {
                    if (x == PlayerSpawn.x) // Don't spawn above the player.
                        continue;
                    else if (TeleporterPositions.Contains(new Vector2Int(x, y))) // there is a teleporter here
                        continue;
                    if (y == 0)
                        continue;
                    if (!wallsGrid.IsWall(x, y)) // if this tile is air
                    {
                        float totalWeights = 0;
                        foreach (Spawnable spawn in AdditionalAirSpawns)
                            totalWeights += spawn.SpawnWeight;
                        float r = rng.Next(0, totalWeights);
                        totalWeights = 0;
                        foreach (Spawnable spawn in AdditionalAirSpawns)
                        {
                            totalWeights += spawn.SpawnWeight;
                            if (totalWeights >= r)
                            {
                                SpawnThing(rng, spawn.Name, x, y - 0.35f, spawn.YOffset);
                                break;
                            }
                        }
                    }
                }
            }
        }

        public virtual void SpawnObjective(int x, int y)
        {
            float worldX = WorldOffset.x + x * BlockSize;
            float worldY = WorldOffset.y + (y + 0.5f) * BlockSize + 1.75f; // some vertical offset needed
            Spawned.Add((GameObject)Network.Instantiate((GameObject)Resources.Load("objective/objective1"), new Vector3(worldX, worldY, 4.75f /*z may vary a bit*/), Quaternion.identity, 0));
        }

        /// <summary>
        /// No longer used, in favour of using vanilla teleporter spawning to be more compatible with other mods, such as Loop Portal
        /// </summary>
        public virtual void SpawnTeleporter(int x, int y, int id)
        {
            float worldX = WorldOffset.x + x * BlockSize;
            float worldY = WorldOffset.y + (y + 0.5f) * BlockSize;
            int nextBiome;
            /*
            if (id == 3) // if a 4th portal is spawned (e.g. loop portal mod) it goes to same biome
            {
                nextBiome = SpawnerScript.curBiome;
            }
            else
            {*/
                nextBiome = new EntranceScript().GetPotentialBiome(SpawnerScript.curBiome);
            //}

            GameScript.endPortal[id] = (GameObject)Network.Instantiate((GameObject)Resources.Load("portal"), new Vector3(worldX, worldY + 1.75f, -1f), Quaternion.identity, 0);
            TerrainGenerators.Log("Portal name: " + GameScript.endPortal[id].name);
            GameScript.endPortalUA[id] = GameScript.endPortal[id].transform.GetChild(0).gameObject;
#pragma warning disable CS0618 // Type or member is obsolete
            GameScript.endPortal[id].GetComponent<NetworkView>().RPC("Activate", RPCMode.All, new object[0]);
            GameScript.endPortalUA[id].GetComponent<NetworkView>().RPC("Set", RPCMode.AllBuffered, new object[]
            {
                nextBiome,
                0,
                id
            });
            
#pragma warning restore CS0618 // Type or member is obsolete
            TeleporterPositions.Add(new Vector2Int(x, y));
            Spawned.Add(GameScript.endPortal[id]);
        }

        public virtual IEnumerator SpawnVanillaSpawnables(RNG rng, bool[,] wallsGrid)
        {
            // a delay is needed so it runs after pirates/meteors are decided, for subworlds to work properly
            yield return new WaitForSeconds(0.2f);
            int width = wallsGrid.GetLength(0);
            int height = wallsGrid.GetLength(1);
            if (ChunkScript != null)
                GameObject.Destroy(ChunkScript); // ChunkScript's OnDestroy destroys its spawns
            ChunkScript = new GameObject("Jdewi Chunk").AddComponent<Chunk>();
            ChunkScript.spawnSpot[0] = ChunkScript.gameObject; // spawns will happen at ChunkScript's position
            ChunkScript.transform.rotation = Quaternion.Euler(180, 0, 0);
            NetworkStuffField.SetValue(ChunkScript, new GameObject[width*height]);

            List<Vector2Int> groundSpawnSpots = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
            {
                for (int y = -1; y < height - 1; y++) // start from -1 because these are the ground tiles underneath the spawn spot
                {
                    if (x == PlayerSpawn.x && (y == PlayerSpawn.y)) // the player spawns here
                        continue;
                    else if (TeleporterPositions.Contains(new Vector2Int(x, y))) // there is a teleporter here
                        continue;
                    if (wallsGrid.IsWall(x, y) && !wallsGrid.IsWall(x, y + 1)) // if this tile is ground and the one above is air
                    {
                        // only spawn if not too close to player spawn
                        Vector2Int vec = new Vector2Int(x, y);
                        int spawnDistanceAllowed = 4 * 16 / BlockSize; // 4 chunks at size 16; 8 chunks at size 8
                        if (Vector2Int.SqrDistance(vec, PlayerSpawn) > spawnDistanceAllowed * spawnDistanceAllowed)
                        {
                            groundSpawnSpots.Add(vec);
                        }
                    }
                }
            }


            Dictionary<Vector2Int, float> distancesFromSpawn = new Dictionary<Vector2Int, float>();
            Vector2Int startPos = PlayerSpawn;
            float maxDist = 0; // technically not necessarily the maximum distance because a shorter path to a position may be found afterwards.
            FloodDistance(startPos, 0);
            void FloodDistance(Vector2Int pos, float existingDistance) // calculate each position's distance from spawn
            {
                if (!distancesFromSpawn.ContainsKey(pos))
                    distancesFromSpawn.Add(pos, existingDistance);
                else if (distancesFromSpawn[pos] > existingDistance)
                    distancesFromSpawn[pos] = existingDistance;
                else // we've already visited this position via a shorter route
                    return;
                if (existingDistance > maxDist)
                    maxDist = existingDistance;
                var up = pos + Vector2Int.up;
                var right = pos + Vector2Int.right;
                var down = pos + Vector2Int.down;
                var left = pos + Vector2Int.left;
                Vector2Int[] neighbours = new Vector2Int[4] { up, right, down, left };
                float nextDistance = existingDistance + 1;
                foreach(var neighbour in neighbours)
                {
                    if (neighbour.x >= width
                        || neighbour.y >= height
                        || neighbour.x < 0
                        || neighbour.y < 0)
                        continue;
                    if (wallsGrid[neighbour.x, neighbour.y])
                        continue;
                    if (neighbour == up)
                        nextDistance += 0.5f; // going up is annoying
                    FloodDistance(neighbour, nextDistance);
                }
            }
            /*
            string s = "";
            for (int j = height - 1; j >= 0 ; j--)
            {
                s += "\r\n";
                for (int i = 0; i < width; i++)
                {

                    if (wallsGrid[i, j])
                        s += "#";
                    else if (distancesFromSpawn.TryGetValue(new Vector2Int(i, j), out float distance))
                        s += Mathf.FloorToInt(distance / (GridWidth + GridHeight) * 9); // 0-9
                    else
                        s += "?";

                }
            }
            TerrainGenerators.Log(s);
            */

            groundSpawnSpots = groundSpawnSpots.OrderByDescending(
                vec => distancesFromSpawn.TryGetValue(vec + Vector2Int.up, out float dist) ? dist : Vector2.Distance(vec + Vector2Int.up, PlayerSpawn)
                ).ToList();

            // loop from closest to furthest, destroying spawn spots where the number of spawn spots is too dense.
            for(int i = groundSpawnSpots.Count - 1; i >= 0; i--)
            {
                var pos = groundSpawnSpots[i];
                int leftX = pos.x - 1;
                int rightX = pos.x + 1;
                int botX = pos.y - 3;
                int topX = pos.y + 3;
                // numNearbySpots includes the spot i
                int numNearbySpots = groundSpawnSpots.Count(spot => spot.x >= leftX && spot.x <= rightX && spot.y >= botX && spot.y <= topX);
                if (numNearbySpots > 3)
                    groundSpawnSpots.RemoveAt(i);
            }

            TerrainGenerators.Log("Spawn spots: " + groundSpawnSpots.Count);

            bool spawnedSpecial = false;
            List<GameObject> teleporterSpots = new List<GameObject>();
            int teleportersToSpawn = Gadgets.GetGadget("LoopPortal")?.Enabled == true ? 4 : 3;
            while (groundSpawnSpots.Count > 0)
            {
                if(SpawnerScript.curBiome == 12 && !spawnedSpecial) // Arena
                {
                    // spawn chalice close to player
                    Vector2Int bookSpot = distancesFromSpawn.FirstOrDefault(posAndDist => 
                    {
                        Vector2Int pos = posAndDist.Key;
                        if (!WallsGrid.IsWall(pos.x, pos.y - 1)) // only spawn on ground
                            return false;
                        float posDist = posAndDist.Value;
                        return posDist > 1 && posDist < 4; // chunks
                    }).Key;
                    if (Vector2Int.Equals(bookSpot, default(Vector2Int)))
                        bookSpot = PlayerSpawn;

                    var book = (GameObject)Network.Instantiate(Resources.Load("obj/chaliceBook"), (Vector3)WorldOffset + bookSpot * BlockSize + new Vector3(0, -2, 0.2f), Quaternion.identity, 0);
                    Spawned.Add(book);
                    spawnedSpecial = true;
                }

                // pick one of n furthest spots from the player remaining
                const float portalDistanceMaxPercentileFromEnd = 0.2f;
                int maxIndex = Mathf.RoundToInt(portalDistanceMaxPercentileFromEnd * (groundSpawnSpots.Count - 1));
                int i = rng.Next(0, maxIndex + 1);
                Vector2Int spawnSpot = groundSpawnSpots[i];
                groundSpawnSpots.RemoveAt(i);
                //TerrainGenerators.Log($"({spawnSpot.x},{spawnSpot.y}) {(distancesFromSpawn.TryGetValue(spawnSpot + Vector2Int.up, out float dist) ? dist.ToString() : "?")}");
                /*if (TeleporterPositions.Count < 3 || (TeleporterPositions.Count == 3 && Gadgets.GetGadget("LoopPortal")?.Enabled == true))
                {
                    SpawnTeleporter(spawnSpot.x, spawnSpot.y, TeleporterPositions.Count);
                }*/
                if(teleporterSpots.Count < teleportersToSpawn)
                {
                    var spot = new GameObject("Teleporter Spot");
                    spot.transform.position = new Vector3(
                        WorldOffset.x + spawnSpot.x * BlockSize, 
                        WorldOffset.y + (spawnSpot.y + 0.5f) * BlockSize + 0.55f,
                        0f);
                    teleporterSpots.Add(spot);
                    Spawned.Add(spot);
                    if (teleporterSpots.Count == teleportersToSpawn)
                    {
                        var entranceScript = new GameObject("EntranceScript").AddComponent<EntranceScript>();
                        entranceScript.spawnSpot = teleporterSpots.ToArray();
                        entranceScript.SpawnEndPortal();
                        // todo: Patch EntranceScript.Start
                        entranceScript.enabled = false; // prevent start running
                        Spawned.Add(entranceScript.gameObject);
                    }
                }
                else
                {
                    float multiplier = 1; // todo: ?
                    DoVanillaGroundSpawn(spawnSpot, rng, multiplier);
                }
            }
        }

        public virtual void DoVanillaGroundSpawn(Vector2Int spawnSpot, RNG rng, float chanceMultiplier)
        {
            // an objective (resource machine) can spawn in addition to normal spawns.
            float objectiveChance = 0.08f * BlockSize / 16 * chanceMultiplier; // 8% per chunks at size 16; 4% at size 8
            if (rng.Next(0f, 1f) < objectiveChance) //
                SpawnObjective(spawnSpot.x, spawnSpot.y);


            // do vanilla spawns
            // each spawn gets at least minSpacing world units of space (before randomness or offset in spawn pos)
            const float minSpacing = 5f;
            const float rngVariation = 1f; // todo: Scale with BlockSize?
            int spotsPerChunk = Mathf.FloorToInt(BlockSize / minSpacing);
            if (spotsPerChunk < 1)
                spotsPerChunk = 1;
            float spotSeparation = BlockSize / spotsPerChunk;

            for (int s = 0; s < spotsPerChunk; s++)
            {
                float spotsPerM = spotsPerChunk / BlockSize;
                float spawnChance = 0.67f / spotsPerM * (3/16f) * chanceMultiplier; // try to spawn around 2 spawnables every 16 units
                if (rng.Next(0f, 1f) > spawnChance)
                    continue;
                float distanceIn = s * spotSeparation + spotSeparation / 2f;
                ChunkScript.transform.position = (Vector3)WorldOffset + new Vector3(
                    x: (spawnSpot.x - 0.5f) * BlockSize + distanceIn + rng.Next(-rngVariation, rngVariation),
                    y: (spawnSpot.y + 0.5f) * BlockSize + 0.375f,
                    z: 0f);
                ChunkScript.SpawnBiomeSlot(SpawnerScript.curBiome, 0, 0);
            }
        }
    }

    public class Spawnable
    {
        public string Name;
        public float SpawnWeight;
        public float YOffset;

        public Spawnable(string name, double spawnWeight, float yOffset = 0)
        {
            this.Name = name;
            this.SpawnWeight = (float)spawnWeight;
            this.YOffset = yOffset;
        }
    }
}
