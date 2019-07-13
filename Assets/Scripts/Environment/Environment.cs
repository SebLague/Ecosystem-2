using System.Collections;
using System.Collections.Generic;
using TerrainGeneration;
using UnityEngine;

public class Environment : MonoBehaviour {

    public int spawnSeed;

    public Population[] initialPopulations;

    // Cached data:
    public static Vector3[, ] tileCentres;
    public static bool[, ] walkable;
    static int size;
    static Vector2Int[, ][] walkableNeighboursMap;
    static Vector2Int[, ][] visibleTilesMap;
    static bool[, ] visibilityCalculatedFlags;
    static System.Random prng;
    TerrainGenerator.TerrainData terrainData;

    // Temp:
    static Vector2Int[] viewOffsetsByDistance;
    static Vector2Int[] visibleTilesTempBuffer;
    static int[] visibleTileDistancesTempBuffer;

    void Start () {
        prng = new System.Random ();

        CreateTerrain ();
        SpawnInitialPopulations ();
    }

    public static Vector2Int[] Sense (Vector2Int coord) {
        if (!visibilityCalculatedFlags[coord.x, coord.y]) {
            CacheVisiblity (coord);
        }
        return visibleTilesMap[coord.x, coord.y];
    }

    public static Vector2Int GetNextTileRandom (Vector2Int current) {
        var neighbours = walkableNeighboursMap[current.x, current.y];
        if (neighbours.Length == 0) {
            return current;
        }
        return neighbours[prng.Next (neighbours.Length)];
    }

    /// Get random neighbour tile, weighted towards those in similar direction as currently facing
    public static Vector2Int GetNextTileWeighted (Vector2Int current, Vector2Int previous, double forwardProbability = 0.2, int weightingIterations = 3) {

        if (current == previous) {
            return GetNextTileRandom (current);
        }

        Vector2Int forwardOffset = (current - previous);
        // Random chance of returning foward tile (if walkable)
        if (prng.NextDouble () < forwardProbability) {
            Vector2Int forwardCoord = current + forwardOffset;

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
        Vector2Int bestNeighbour = current;

        for (int i = 0; i < weightingIterations; i++) {
            Vector2Int neighbour = neighbours[prng.Next (neighbours.Length)];
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
    void CreateTerrain () {
        var terrainGenerator = FindObjectOfType<TerrainGenerator> ();
        terrainData = terrainGenerator.Generate ();
        tileCentres = terrainData.tileCentres;
        walkable = terrainData.walkable;
        size = terrainData.size;

        walkableNeighboursMap = new Vector2Int[size, size][];
        visibleTilesMap = new Vector2Int[size, size][];
        visibilityCalculatedFlags = new bool[size, size];

        int bufferSize = (Animal.maxViewDistance * 2 + 1) * (Animal.maxViewDistance * 2 + 1);
        visibleTilesTempBuffer = new Vector2Int[bufferSize];
        visibleTileDistancesTempBuffer = new int[bufferSize];

        // Find and store all walkable neighbours for each walkable tile on the map
        for (int y = 0; y < terrainData.size; y++) {
            for (int x = 0; x < terrainData.size; x++) {
                if (walkable[x, y]) {
                    List<Vector2Int> walkableNeighbours = new List<Vector2Int> ();
                    for (int offsetY = -1; offsetY <= 1; offsetY++) {
                        for (int offsetX = -1; offsetX <= 1; offsetX++) {
                            if (offsetX != 0 || offsetY != 0) {
                                int neighbourX = x + offsetX;
                                int neighbourY = y + offsetY;
                                if (neighbourX >= 0 && neighbourX < size && neighbourY >= 0 && neighbourY < size) {
                                    if (walkable[neighbourX, neighbourY]) {
                                        walkableNeighbours.Add (new Vector2Int (neighbourX, neighbourY));
                                    }
                                }
                            }
                        }
                    }
                    walkableNeighboursMap[x, y] = walkableNeighbours.ToArray ();
                }
            }
        }
    }

    void SpawnInitialPopulations () {
        var spawnPrng = new System.Random (spawnSeed);
        var spawnCoords = new List<Vector2Int> (terrainData.landCoords);

        foreach (var pop in initialPopulations) {
            for (int i = 0; i < pop.count; i++) {
                if (spawnCoords.Count == 0) {
                    Debug.Log ("Ran out of empty tiles to spawn initial population");
                    break;
                }
                int spawnCoordIndex = spawnPrng.Next (0, spawnCoords.Count);
                Vector2Int coord = spawnCoords[spawnCoordIndex];
                spawnCoords.RemoveAt (spawnCoordIndex);

                var entity = Instantiate (pop.prefab);
                entity.SetCoord (coord);
            }
        }
    }

    static void CacheVisiblity (Vector2Int coord) {
        visibilityCalculatedFlags[coord.x, coord.y] = true;

        int x = coord.x;
        int y = coord.y;
        int viewRadius = Animal.maxViewDistance;
        int sqrViewRadius = viewRadius * viewRadius;

        int visibleTileIndex = 0;
        for (int offsetY = -viewRadius; offsetY <= viewRadius; offsetY++) {
            for (int offsetX = -viewRadius; offsetX <= viewRadius; offsetX++) {
                int sqrOffsetDst = offsetX * offsetX + offsetY * offsetY;
                if ((offsetX != 0 || offsetY != 0) && sqrOffsetDst <= sqrViewRadius) {
                    int targetX = x + offsetX;
                    int targetY = y + offsetY;
                    if (targetX >= 0 && targetX < size && targetY >= 0 && targetY < size) {
                        if (walkable[targetX, targetY]) {
                            if (EnvironmentUtility.TileIsVisibile (x, y, targetX, targetY)) {
                                visibleTilesTempBuffer[visibleTileIndex] = new Vector2Int (targetX, targetY);
                                visibleTileDistancesTempBuffer[visibleTileIndex] = sqrOffsetDst;
                                visibleTileIndex++;
                            }
                        }
                    }
                }
            }
        }

        int numVisibleTiles = visibleTileIndex;
        visibleTilesMap[x, y] = new Vector2Int[numVisibleTiles];

        // Sort visible tiles from closest to furthest
        for (int i = 0; i < numVisibleTiles - 1; i++) {
            for (int j = i + 1; j > 0; j--) {
                if (visibleTileDistancesTempBuffer[j - 1] > visibleTileDistancesTempBuffer[j]) {
                    // Swap distances
                    int tempDst = visibleTileDistancesTempBuffer[j - 1];
                    visibleTileDistancesTempBuffer[j - 1] = visibleTileDistancesTempBuffer[j];
                    visibleTileDistancesTempBuffer[j] = tempDst;

                    // Swap tile
                    Vector2Int tempTile = visibleTilesTempBuffer[j - 1];
                    visibleTilesTempBuffer[j - 1] = visibleTilesTempBuffer[j];
                    visibleTilesTempBuffer[j] = tempTile;
                }
            }
        }

        for (int i = 0; i < visibleTileIndex; i++) {
            visibleTilesMap[x, y][i] = visibleTilesTempBuffer[i];
        }

    }

    [System.Serializable]
    public struct Population {
        public LivingEntity prefab;
        public int count;
    }
}