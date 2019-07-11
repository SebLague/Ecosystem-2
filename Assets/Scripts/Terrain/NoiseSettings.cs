using UnityEngine;

namespace TerrainGeneration {
    [System.Serializable]
    public class NoiseSettings {
        public int seed;
        [Range (1, 8)]
        public int numLayers = 4;
        public float persistence = 0.5f;
        public float lacunarity = 2;
        public float scale = 1;
        public Vector2 offset;
    }
}