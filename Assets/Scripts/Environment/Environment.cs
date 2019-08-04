using System.Collections;
using System.Collections.Generic;
using TerrainGeneration;
using UnityEngine;

public class Environment : MonoBehaviour {

    const int mapRegionSize = 10;

    public int seed;

    [Header ("Trees")]
    public MeshRenderer treePrefab;
    [Range (0, 1)]
    public float treeProbability;

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

    static Dictionary<Species, List<Species>> preyBySpecies;
    static Dictionary<Species, List<Species>> predatorsBySpecies;

    // array of visible tiles from any tile; value is Coord.invalid if no visible water tile
    static Coord[, ] closestVisibleWaterMap;

    static System.Random prng;
    TerrainGenerator.TerrainData terrainData;

    static Dictionary<Species, Map> speciesMaps;

    void Start () {
        prng = new System.Random ();

        Init ();
        SpawnInitialPopulations ();

    }

    void OnDrawGizmos () {
        /* 
        if (showMapDebug) {
            if (preyMap != null && mapCoordTransform != null) {
                Coord coord = new Coord ((int) mapCoordTransform.position.x, (int) mapCoordTransform.position.z);
                preyMap.DrawDebugGizmos (coord, mapViewDst);
            }
        }
        */
    }

    public static void RegisterMove (LivingEntity entity, Coord from, Coord to) {
        speciesMaps[entity.species].Move (entity, from, to);
    }

    public static void RegisterDeath (LivingEntity entity) {
        speciesMaps[entity.species].Remove (entity, entity.coord);
    }

    public static Coord SenseWater (Coord coord) {
        var closestWaterCoord = closestVisibleWaterMap[coord.x, coord.y];
        if (closestWaterCoord != Coord.invalid) {
            float sqrDst = (tileCentres[coord.x, coord.y] - tileCentres[closestWaterCoord.x, closestWaterCoord.y]).sqrMagnitude;
            if (sqrDst <= Animal.maxViewDistance * Animal.maxViewDistance) {
                return closestWaterCoord;
            }
        }
        return Coord.invalid;
    }

    public static LivingEntity SenseFood (Coord coord, Animal self, System.Func<LivingEntity, LivingEntity, int> foodPreference) {
        var foodSources = new List<LivingEntity> ();

        List<Species> prey = preyBySpecies[self.species];
        for (int i = 0; i < prey.Count; i++) {

            Map speciesMap = speciesMaps[prey[i]];

            foodSources.AddRange (speciesMap.GetEntities (coord, Animal.maxViewDistance));
        }

        // Sort food sources based on preference function
        foodSources.Sort ((a, b) => foodPreference (self, a).CompareTo (foodPreference (self, b)));

        // Return first visible food source
        for (int i = 0; i < foodSources.Count; i++) {
            Coord targetCoord = foodSources[i].coord;
            if (EnvironmentUtility.TileIsVisibile (coord.x, coord.y, targetCoord.x, targetCoord.y)) {
                return foodSources[i];
            }
        }

        return null;
    }

    // Return list of animals of the same species, with the opposite gender, who are also searching for a mate
    public static List<Animal> SensePotentialMates (Coord coord, Animal self) {
        Map speciesMap = speciesMaps[self.species];
        List<LivingEntity> visibleEntities = speciesMap.GetEntities (coord, Animal.maxViewDistance);
        var potentialMates = new List<Animal> ();

        for (int i = 0; i < visibleEntities.Count; i++) {
            var visibleAnimal = (Animal) visibleEntities[i];
            if (visibleAnimal != self && visibleAnimal.genes.isMale != self.genes.isMale) {
                if (visibleAnimal.currentAction == CreatureAction.SearchingForMate) {
                    potentialMates.Add (visibleAnimal);
                }
            }
        }

        return potentialMates;
    }

    public static Surroundings Sense (Coord coord) {
        var closestPlant = speciesMaps[Species.Plant].ClosestEntity (coord, Animal.maxViewDistance);
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

        int numSpecies = System.Enum.GetNames (typeof (Species)).Length;
        preyBySpecies = new Dictionary<Species, List<Species>> ();
        predatorsBySpecies = new Dictionary<Species, List<Species>> ();

        // Init species maps
        speciesMaps = new Dictionary<Species, Map> ();
        for (int i = 0; i < numSpecies; i++) {
            Species species = (Species) (1 << i);
            speciesMaps.Add (species, new Map (size, mapRegionSize));

            preyBySpecies.Add (species, new List<Species> ());
            predatorsBySpecies.Add (species, new List<Species> ());
        }

        // Store predator/prey relationships for all species
        for (int i = 0; i < initialPopulations.Length; i++) {

            if (initialPopulations[i].prefab is Animal) {
                Animal hunter = (Animal) initialPopulations[i].prefab;
                Species diet = hunter.diet;

                for (int huntedSpeciesIndex = 0; huntedSpeciesIndex < numSpecies; huntedSpeciesIndex++) {
                    int bit = ((int) diet >> huntedSpeciesIndex) & 1;
                    // this bit of diet mask set (i.e. the hunter eats this species)
                    if (bit == 1) {
                        int huntedSpecies = 1 << huntedSpeciesIndex;
                        preyBySpecies[hunter.species].Add ((Species) huntedSpecies);
                        predatorsBySpecies[(Species) huntedSpecies].Add (hunter.species);
                    }
                }
            }
        }

        //LogPredatorPreyRelationships ();

        SpawnTrees ();

        walkableNeighboursMap = new Coord[size, size][];

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
                    if (prng.NextDouble () < treeProbability) {
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
                entity.Init (coord);

                speciesMaps[entity.species].Add (entity, coord);
            }
        }
    }

    void LogPredatorPreyRelationships () {
        int numSpecies = System.Enum.GetNames (typeof (Species)).Length;
        for (int i = 0; i < numSpecies; i++) {
            string s = "(" + System.Enum.GetNames (typeof (Species)) [i] + ") ";
            int enumVal = 1 << i;
            var prey = preyBySpecies[(Species) enumVal];
            var predators = predatorsBySpecies[(Species) enumVal];

            s += "Prey: " + ((prey.Count == 0) ? "None" : "");
            for (int j = 0; j < prey.Count; j++) {
                s += prey[j];
                if (j != prey.Count - 1) {
                    s += ", ";
                }
            }

            s += " | Predators: " + ((predators.Count == 0) ? "None" : "");
            for (int j = 0; j < predators.Count; j++) {
                s += predators[j];
                if (j != predators.Count - 1) {
                    s += ", ";
                }
            }
            print (s);
        }
    }

    [System.Serializable]
    public struct Population {
        public LivingEntity prefab;
        public int count;
    }

}