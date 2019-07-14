using System.Collections;
using System.Collections.Generic;
using TerrainGeneration;
using UnityEngine;

public class Environment : MonoBehaviour {

    public int seed;

    [Header ("Trees")]
    public MeshRenderer treePrefab;
    [Range (0, 1)]
    public float treePlacementProbability;

    [Header ("Populations")]
    public Population[] initialPopulations;

    [Header ("Debug")]
    public bool showMapDebug;
    public Transform mapCoordTransform;
    public float mapViewDst;

    // Cached data:
    public static Vector3[, ] tileCentres;
    public static bool[, ] walkable;
    static int size;
    static Coord[, ][] walkableNeighboursMap;
    static List<Coord> walkableCoords;

    // array of visible tiles from any tile; value is Coord.invalid if no visible water tile
    static Coord[, ] closestVisibleWaterMap;

    static System.Random prng;
    TerrainGenerator.TerrainData terrainData;

    static Map preyMap;
    static Map plantMap;

    void Start () {
        prng = new System.Random ();

        Init ();
        SpawnInitialPopulations ();

    }

    void OnDrawGizmos () {
        if (showMapDebug) {
            if (preyMap != null && mapCoordTransform != null) {
                Coord coord = new Coord ((int) mapCoordTransform.position.x, (int) mapCoordTransform.position.z);
                preyMap.DrawDebugGizmos (coord, mapViewDst);
            }
        }
    }

    public static void RegisterMove (LivingEntity entity, Coord from, Coord to) {
        preyMap.Move (entity, from, to);
    }

    public static void RegisterPlantDeath (Plant plant) {
        plantMap.Remove (plant, plant.coord);
    }

    public static Surroundings Sense (Coord coord) {
        var closestPlant = plantMap.ClosestEntity (coord, Animal.maxViewDistance);
        var surroundings = new Surroundings ();
        surroundings.nearestFoodSource = closestPlant;
        surroundings.nearestWaterTile = closestVisibleWaterMap[coord.x, coord.y];

        return surroundings;
    }

    public static Coord GetNextTileRandom (Coord current) {
        var neighbours = walkableNeighboursMap[current.x, current.y];
        if (neighbours.Length == 0) {
            return current;
        }
        return neighbours[prng.Next (neighbours.Length)];
    }

    /// Get random neighbour tile, weighted towards those in similar direction as currently facing
    public static Coord GetNextTileWeighted (Coord current, Coord previous, double forwardProbability = 0.2, int weightingIterations = 3) {

        if (current == previous) {
            return GetNextTileRandom (current);
        }

        Coord forwardOffset = (current - previous);
        // Random chance of returning foward tile (if walkable)
        if (prng.NextDouble () < forwardProbability) {
            Coord forwardCoord = current + forwardOffset;

            if (forwardCoord.x >= 0 && forwardCoord.x < size && forwardCoord.y >= 0 && forwardCoord.y < size) {
                if (walkable[forwardCoord.x, forwardCoord.y]) {
                    return forwardCoord;
                }
            }
        }

        // Get walkable neighbours
        var neighbours = walkableNeighboursMap[current.x, current.y];
        if (neighbours.Length == 0) {
            return current;
        }

        // From n random tiles, pick the one that is most aligned with the forward direction:
        Vector2 forwardDir = new Vector2 (forwardOffset.x, forwardOffset.y).normalized;
        float bestScore = float.MinValue;
        Coord bestNeighbour = current;

        for (int i = 0; i < weightingIterations; i++) {
            Coord neighbour = neighbours[prng.Next (neighbours.Length)];
            Vector2 offset = neighbour - current;
            float score = Vector2.Dot (offset.normalized, forwardDir);
            if (score > bestScore) {
                bestScore = score;
                bestNeighbour = neighbour;
            }
        }

        return bestNeighbour;
    }

    // Call terrain generator and cache useful info
    void Init () {
        var sw = System.Diagnostics.Stopwatch.StartNew ();

        var terrainGenerator = FindObjectOfType<TerrainGenerator> ();
        terrainData = terrainGenerator.Generate ();

        tileCentres = terrainData.tileCentres;
        walkable = terrainData.walkable;
        size = terrainData.size;

        SpawnTrees ();

        walkableNeighboursMap = new Coord[size, size][];

        preyMap = new Map (size, 10);
        plantMap = new Map (size, 10);

        // Find and store all walkable neighbours for each walkable tile on the map
        for (int y = 0; y < terrainData.size; y++) {
            for (int x = 0; x < terrainData.size; x++) {
                if (walkable[x, y]) {
                    List<Coord> walkableNeighbours = new List<Coord> ();
                    for (int offsetY = -1; offsetY <= 1; offsetY++) {
                        for (int offsetX = -1; offsetX <= 1; offsetX++) {
                            if (offsetX != 0 || offsetY != 0) {
                                int neighbourX = x + offsetX;
                                int neighbourY = y + offsetY;
                                if (neighbourX >= 0 && neighbourX < size && neighbourY >= 0 && neighbourY < size) {
                                    if (walkable[neighbourX, neighbourY]) {
                                        walkableNeighbours.Add (new Coord (neighbourX, neighbourY));
                                    }
                                }
                            }
                        }
                    }
                    walkableNeighboursMap[x, y] = walkableNeighbours.ToArray ();
                }
            }
        }

        // Generate offsets within max view distance, sorted by distance ascending
        // Used to speed up per-tile search for closest water tile
        List<Coord> viewOffsets = new List<Coord> ();
        int viewRadius = Animal.maxViewDistance;
        int sqrViewRadius = viewRadius * viewRadius;
        for (int offsetY = -viewRadius; offsetY <= viewRadius; offsetY++) {
            for (int offsetX = -viewRadius; offsetX <= viewRadius; offsetX++) {
                int sqrOffsetDst = offsetX * offsetX + offsetY * offsetY;
                if ((offsetX != 0 || offsetY != 0) && sqrOffsetDst <= sqrViewRadius) {
                    viewOffsets.Add (new Coord (offsetX, offsetY));
                }
            }
        }
        viewOffsets.Sort ((a, b) => (a.x * a.x + a.y * a.y).CompareTo (b.x * b.x + b.y * b.y));
        Coord[] viewOffsetsArr = viewOffsets.ToArray ();

        // Find closest accessible water tile for each tile on the map:
        closestVisibleWaterMap = new Coord[size, size];
        for (int y = 0; y < terrainData.size; y++) {
            for (int x = 0; x < terrainData.size; x++) {
                bool foundWater = false;
                if (walkable[x, y]) {
                    for (int i = 0; i < viewOffsets.Count; i++) {
                        int targetX = x + viewOffsetsArr[i].x;
                        int targetY = y + viewOffsetsArr[i].y;
                        if (targetX >= 0 && targetX < size && targetY >= 0 && targetY < size) {
                            if (terrainData.shore[targetX, targetY]) {
                                if (EnvironmentUtility.TileIsVisibile (x, y, targetX, targetY)) {
                                    closestVisibleWaterMap[x, y] = new Coord (targetX, targetY);
                                    foundWater = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (!foundWater) {
                    closestVisibleWaterMap[x, y] = Coord.invalid;
                }
            }
        }
        Debug.Log ("Init time: " + sw.ElapsedMilliseconds);
    }

    void SpawnTrees () {
        // Settings:
        float maxRot = 4;
        float maxScaleDeviation = .2f;
        float colVariationFactor = 0.15f;
        float minCol = .8f;

        var spawnPrng = new System.Random (seed);
        var treeHolder = new GameObject ("Tree holder").transform;
        walkableCoords = new List<Coord> ();

        for (int y = 0; y < terrainData.size; y++) {
            for (int x = 0; x < terrainData.size; x++) {
                if (walkable[x, y]) {
                    if (prng.NextDouble () < treePlacementProbability) {
                        // Randomize rot/scale
                        float rotX = Mathf.Lerp (-maxRot, maxRot, (float) spawnPrng.NextDouble ());
                        float rotZ = Mathf.Lerp (-maxRot, maxRot, (float) spawnPrng.NextDouble ());
                        float rotY = (float) spawnPrng.NextDouble () * 360f;
                        Quaternion rot = Quaternion.Euler (rotX, rotY, rotZ);
                        float scale = 1 + ((float) spawnPrng.NextDouble () * 2 - 1) * maxScaleDeviation;

                        // Randomize colour
                        float col = Mathf.Lerp (minCol, 1, (float) spawnPrng.NextDouble ());
                        float r = col + ((float) spawnPrng.NextDouble () * 2 - 1) * colVariationFactor;
                        float g = col + ((float) spawnPrng.NextDouble () * 2 - 1) * colVariationFactor;
                        float b = col + ((float) spawnPrng.NextDouble () * 2 - 1) * colVariationFactor;

                        // Spawn
                        MeshRenderer tree = Instantiate (treePrefab, tileCentres[x, y], rot);
                        tree.transform.parent = treeHolder;
                        tree.transform.localScale = Vector3.one * scale;
                        tree.material.color = new Color (r, g, b);

                        // Mark tile unwalkable
                        walkable[x, y] = false;
                    } else {
                        walkableCoords.Add (new Coord (x, y));
                    }
                }
            }
        }
    }

    void SpawnInitialPopulations () {
        var spawnPrng = new System.Random (seed);
        var spawnCoords = new List<Coord> (walkableCoords);

        foreach (var pop in initialPopulations) {
            for (int i = 0; i < pop.count; i++) {
                if (spawnCoords.Count == 0) {
                    Debug.Log ("Ran out of empty tiles to spawn initial population");
                    break;
                }
                int spawnCoordIndex = spawnPrng.Next (0, spawnCoords.Count);
                Coord coord = spawnCoords[spawnCoordIndex];
                spawnCoords.RemoveAt (spawnCoordIndex);

                var entity = Instantiate (pop.prefab);
                entity.SetCoord (coord);

                if (entity is Plant) {
                    plantMap.Add (entity, coord);
                } else {
                    preyMap.Add (entity, coord);
                    mapCoordTransform = entity.transform;
                }
            }
        }
    }

    [System.Serializable]
    public struct Population {
        public LivingEntity prefab;
        public int count;
    }

}