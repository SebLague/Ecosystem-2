using System.Collections;
using System.Collections.Generic;
using TerrainGeneration;
using UnityEngine;

public class Environment : MonoBehaviour {

    public int spawnSeed;
    public Population[] initialPopulations;
    public static Vector3[, ] tileCentres;
    public static bool[, ] walkable;

    void Start () {
        var spawnPrng = new System.Random (spawnSeed);

        var terrainGenerator = FindObjectOfType<TerrainGenerator> ();
        var terrainData = terrainGenerator.Generate ();
        tileCentres = terrainData.tileCentres;
        walkable = terrainData.walkable;

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
}