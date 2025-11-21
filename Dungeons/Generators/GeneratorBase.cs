using GadgetCore.API;
using System;
using System.Collections;
using System.Collections.Generic;
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
        public virtual int BlockSize { get; } = 16;
        public virtual int GridWidth { get; } = 32;
        public virtual int GridHeight { get; } = 24;
        public abstract bool[,] WallsGrid { get; }
        public virtual Color MinimapWallsColor { get => GetMinimapColor(); }
        public abstract Vector2Int PlayerSpawn { get; } // grid position

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
                leftWorld: (-padding - 0.5f) * BlockSize, 
                rightWorld: (minimapWidth - padding - 0.5f) * BlockSize,
                topWorld: (minimapHeight - padding - 1.5f) * BlockSize, 
                botWorld: (-padding - 1.5f) * BlockSize, 
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
                    Spawned.Add((GameObject)Network.Instantiate(standToSpawn, new Vector3(x * BlockSize + xOffset, (y + 0.5f) * BlockSize + yOffset, 0), Quaternion.identity, 0));
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

                Spawned.Add((GameObject)Network.Instantiate(toSpawn, new Vector3(x * BlockSize + rng.Next(-(BlockSize / 2f - 2), (BlockSize / 2f - 2)), (y + 0.5f) * BlockSize + centreY + yOffset, z), Quaternion.identity, 0));
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
                new Vector3(x * BlockSize, y * BlockSize, 2f/*0.15f*/), Quaternion.Euler(180, 0, 0));
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
            float worldX = x * BlockSize;
            float worldY = (y + 0.5f) * BlockSize + 1.75f; // some vertical offset needed
            Spawned.Add((GameObject)Network.Instantiate((GameObject)Resources.Load("objective/objective1"), new Vector3(worldX, worldY, 4.75f /*z may vary a bit*/), Quaternion.identity, 0));
        }

        public virtual void SpawnTeleporter(int x, int y, int id)
        {
            float worldX = x * BlockSize;
            float worldY = (y + 0.5f) * BlockSize;
            int nextBiome;
            if (id == 3)
                //nextBiome = 98; // back to ship
                nextBiome = SpawnerScript.curBiome;
            else
            {
                nextBiome = new EntranceScript().GetPotentialBiome(SpawnerScript.curBiome);
            }

            GameScript.endPortal[id] = (GameObject)Network.Instantiate((GameObject)Resources.Load("portal"), new Vector3(worldX, worldY + 1.75f, -1f), Quaternion.identity, 0);
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
            float maxHeuristic = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < wallsGrid.GetLength(1) - 1; y++)
                {
                    if (x == PlayerSpawn.x && (y == PlayerSpawn.y)) // the player spawns here
                        continue;
                    else if (TeleporterPositions.Contains(new Vector2Int(x, y))) // there is a teleporter here
                        continue;
                    if (wallsGrid.IsWall(x, y) && !wallsGrid.IsWall(x, y + 1)) // if this tile is ground and the one above is air
                    {
                        Vector2Int vec = new Vector2Int(x, y);
                        if (Vector2Int.SqrDistance(vec, PlayerSpawn) > 3 * 3) // only spawn if not too close to player spawn
                        {
                            float heuristic = Mathf.Abs(x - PlayerSpawn.x) - y / 8f;
                            if (heuristic > maxHeuristic)
                            {
                                groundSpawnSpots.Insert(rng.Next(0, groundSpawnSpots.Count / 2), vec);
                                maxHeuristic = heuristic;
                            }
                            else
                                groundSpawnSpots.Add(vec);
                        }
                    }
                }
            }

            foreach(var spawnSpot in groundSpawnSpots)
            {
                if (TeleporterPositions.Count < 3)
                {
                    SpawnTeleporter(spawnSpot.x, spawnSpot.y, TeleporterPositions.Count);
                    continue;
                }

                DoVanillaGroundSpawn(spawnSpot, rng);
            }
        }

        public virtual void DoVanillaGroundSpawn(Vector2Int spawnSpot, RNG rng)
        {
            // an objective (resource machine) can spawn in addition to normal spawns.
            if (rng.Next(0f, 1f) < 0.08f) // 8% chance per chunk
                SpawnObjective(spawnSpot.x, spawnSpot.y);


            // do vanilla spawns
            // each spawn gets at least minSpacing world units of space (before randomness or offset in spawn pos)
            const float minSpacing = 5f;
            const float rngVariation = 1f;
            int spawnsPerChunk = Mathf.FloorToInt(BlockSize / minSpacing);
            if (spawnsPerChunk < 1)
                spawnsPerChunk = 1;
            float spawnSeparation = BlockSize / spawnsPerChunk;

            for (int s = 0; s < spawnsPerChunk; s++)
            {
                const float spawnChance = 0.67f;
                if (rng.Next(0f, 1f) > spawnChance)
                    continue;
                float distanceIn = s * spawnSeparation + spawnSeparation / 2f;
                ChunkScript.transform.position = new Vector3(
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
