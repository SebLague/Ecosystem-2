using System.Collections;
using System.Collections.Generic;
using TerrainGeneration;
using UnityEngine;

public class Environment : MonoBehaviour {

    public int spawnSeed;
    public Population[] initialPopulations;
    public static Vector3[, ] tileCentres;
    public static bool[, ] walkable;

    static int size;
    static WalkableNeigbours[, ] walkableNeighboursMap;
    static System.Random prng;

    TerrainGenerator.TerrainData terrainData;

    void Start () {
        prng = new System.Random ();

        CreateTerrain ();
        SpawnInitialPopulations ();
    }

    public static Vector2Int GetNextTileRandom (Vector2Int current) {
        var neighbours = walkableNeighboursMap[current.x, current.y];
        if (neighbours.count == 0) {
            return current;
        }
        return neighbours.coords[prng.Next (neighbours.count)];
    }

    /// Get random neighbour tile, weighted towards those in similar direction as currently facing
    public static Vector2Int GetNextTileWeighted (Vector2Int current, Vector2Int previous, double forwardProbability = 0.0, int weightingIterations = 3) {

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
        if (neighbours.count == 0) {
            return current;
        }

        // From n random tiles, pick the one that is most aligned with the forward direction:
        Vector2 forwardDir = new Vector2 (forwardOffset.x, forwardOffset.y).normalized;
        float bestScore = float.MinValue;
        Vector2Int bestNeighbour = current;

        for (int i = 0; i < weightingIterations; i++) {
            Vector2Int neighbour = neighbours.coords[prng.Next (neighbours.count)];
            Vector2 offset = neighbour - current;
            float score = Vector2.Dot (offset.normalized, forwardDir);
            if (score > bestScore) {
                bestScore = score;
                bestNeighbour = neighbour;
            }
        }

        return bestNeighbour;
    }

    void CreateTerrain () {
        var terrainGenerator = FindObjectOfType<TerrainGenerator> ();
        terrainData = terrainGenerator.Generate ();
        tileCentres = terrainData.tileCentres;
        walkable = terrainData.walkable;
        size = terrainData.size;

        walkableNeighboursMap = new WalkableNeigbours[size, size];

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
                    walkableNeighboursMap[x, y] = new WalkableNeigbours { coords = walkableNeighbours.ToArray (), count = walkableNeighbours.Count };
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

                var animal = Instantiate (pop.animalPrefab);
                animal.SetCoord (coord);
            }
        }
    }

    [System.Serializable]
    public struct Population {
        public Animal animalPrefab;
        public int count;
    }

    public struct WalkableNeigbours {
        public int count;
        public Vector2Int[] coords;
    }
}