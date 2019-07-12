using System.Collections;
using System.Collections.Generic;
using TerrainGeneration;
using UnityEngine;

public class Environment : MonoBehaviour {

    public int spawnSeed;
    public Population[] initialPopulations;
    public static Vector3[, ] tileCentres;
    public static bool[, ] walkable;

    static WalkableNeigbours[, ] walkableNeighboursMap;
    static System.Random prng;

    TerrainGenerator.TerrainData terrainData;

    void Start () {
        prng = new System.Random ();

        CreateTerrain ();
        SpawnInitialPopulations ();
    }

    public static Vector2Int GetRandomWalkableNeighbour (Vector2Int coord) {
        var neighbours = walkableNeighboursMap[coord.x, coord.y];
        if (neighbours.count == 0) {
            return coord;
        }
        return neighbours.coords[prng.Next (neighbours.count)];
    }

    void CreateTerrain () {
        var terrainGenerator = FindObjectOfType<TerrainGenerator> ();
        terrainData = terrainGenerator.Generate ();
        tileCentres = terrainData.tileCentres;
        walkable = terrainData.walkable;
        int size = terrainData.size;
        walkableNeighboursMap = new WalkableNeigbours[size, size];

        // Find and store all walkable neighbours for each walkable tile on the map
        for (int y = 0; y < terrainData.size; y++) {
            for (int x = 0; x < terrainData.size; x++) {
                if (walkable[x, y]) {
                    List<Vector2Int> walkableNeighbours = new List<Vector2Int> ();
                    for (int offsetY = 0; offsetY <= 1; offsetY++) {
                        for (int offsetX = 0; offsetX <= 1; offsetX++) {
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