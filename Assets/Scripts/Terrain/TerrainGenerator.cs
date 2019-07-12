﻿using System.Collections.Generic;
using UnityEngine;

namespace TerrainGeneration {
    [ExecuteInEditMode]
    public class TerrainGenerator : MonoBehaviour {

        const string meshHolderName = "Terrain Mesh";

        public bool autoUpdate = true;

        public int worldSize = 20;
        [Range (0.2f, 2)]
        public float cellSize = 1;
        public float waterDepth = .2f;
        public float edgeDepth = .2f;

        public NoiseSettings terrainNoise;
        public Material mat;

        public Biome water;
        public Biome sand;
        public Biome grass;

        [Header ("Info")]
        public int numTiles;
        public int numLandTiles;
        public int numWaterTiles;
        public float waterPercent;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        Mesh mesh;

        bool needsUpdate;

        void Update () {
            if (needsUpdate && autoUpdate) {
                needsUpdate = false;
                Generate ();
            } else {
                if (!Application.isPlaying) {
                    UpdateColours ();
                }
            }
        }

        public void Generate () {
            CreateMeshComponents ();

            int numCellsPerLine = Mathf.CeilToInt (worldSize / cellSize);
            float actualWorldSize = cellSize * numCellsPerLine;
            float min = -actualWorldSize / 2f;
            float[, ] map = HeightmapGenerator.GenerateHeightmap (terrainNoise, numCellsPerLine);

            var vertices = new List<Vector3> ();
            var triangles = new List<int> ();
            var uvs = new List<Vector2> ();
            var normals = new List<Vector3> ();

            // Some convenience stuff:
            var biomes = new Biome[] { water, sand, grass };
            Vector3[] upVectorX4 = { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            Vector2Int[] nswe = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            int[][] sideVertIndexByDir = { new int[] { 3, 2 }, new int[] { 0, 1 }, new int[] { 2, 0 }, new int[] { 1, 3 } };
            Vector3[] sideNormalsByDir = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

            numLandTiles = 0;
            numWaterTiles = 0;

            for (int y = 0; y < numCellsPerLine; y++) {
                for (int x = 0; x < numCellsPerLine; x++) {
                    Vector2 uv = GetBiomeInfo (map[x, y], biomes);
                    uvs.AddRange (new Vector2[] { uv, uv, uv, uv });

                    bool isWaterTile = uv.x == 0f;
                    bool isLandTile = !isWaterTile;
                    if (isWaterTile) {
                        numWaterTiles++;
                    } else {
                        numLandTiles++;
                    }

                    // Vertices
                    int vertIndex = vertices.Count;
                    float height = (isWaterTile) ? -waterDepth : 0;
                    Vector3 nw = new Vector3 (min + cellSize * x, height, -min - cellSize * y);
                    Vector3 ne = nw + Vector3.right * cellSize;
                    Vector3 sw = nw - Vector3.forward * cellSize;
                    Vector3 se = sw + Vector3.right * cellSize;
                    Vector3[] tileVertices = { nw, ne, sw, se };
                    vertices.AddRange (tileVertices);
                    normals.AddRange (upVectorX4);

                    // Add triangles
                    triangles.Add (vertIndex);
                    triangles.Add (vertIndex + 1);
                    triangles.Add (vertIndex + 2);
                    triangles.Add (vertIndex + 1);
                    triangles.Add (vertIndex + 3);
                    triangles.Add (vertIndex + 2);

                    // Bridge gaps between water and land tiles, and also fill in sides of map
                    bool isEdgeTile = x == 0 || x == numCellsPerLine - 1 || y == 0 || y == numCellsPerLine - 1;
                    if (isLandTile || isEdgeTile) {
                        for (int i = 0; i < nswe.Length; i++) {
                            int neighbourX = x + nswe[i].x;
                            int neighbourY = y + nswe[i].y;
                            bool neighbourIsOutOfBounds = neighbourX < 0 || neighbourX >= numCellsPerLine || neighbourY < 0 || neighbourY >= numCellsPerLine;
                            bool neighbourIsWater = false;
                            if (!neighbourIsOutOfBounds) {
                                float neighbourHeight = map[neighbourX, neighbourY];
                                neighbourIsWater = neighbourHeight <= biomes[0].height;
                            }

                            if (neighbourIsOutOfBounds || (isLandTile && neighbourIsWater)) {
                                float depth = waterDepth;
                                if (neighbourIsOutOfBounds) {
                                    depth = (isWaterTile) ? edgeDepth : edgeDepth + waterDepth;
                                }
                                vertIndex = vertices.Count;
                                int edgeVertIndexA = sideVertIndexByDir[i][0];
                                int edgeVertIndexB = sideVertIndexByDir[i][1];
                                vertices.Add (tileVertices[edgeVertIndexA]);
                                vertices.Add (tileVertices[edgeVertIndexA] + Vector3.down * depth);
                                vertices.Add (tileVertices[edgeVertIndexB]);
                                vertices.Add (tileVertices[edgeVertIndexB] + Vector3.down * depth);

                                uvs.AddRange (new Vector2[] { uv, uv, uv, uv });
                                int[] sideTriIndices = { vertIndex, vertIndex + 1, vertIndex + 2, vertIndex + 1, vertIndex + 3, vertIndex + 2 };
                                triangles.AddRange (sideTriIndices);
                                normals.AddRange (new Vector3[] { sideNormalsByDir[i], sideNormalsByDir[i], sideNormalsByDir[i], sideNormalsByDir[i] });
                            }
                        }
                    }
                }
            }

            // Update mesh:
            mesh.SetVertices (vertices);
            mesh.SetTriangles (triangles, 0, true);
            mesh.SetUVs (0, uvs);
            mesh.SetNormals (normals);

            meshRenderer.sharedMaterial = mat;
            UpdateColours ();

            numTiles = numLandTiles + numWaterTiles;
            waterPercent = numWaterTiles / (float) numTiles;
        }

        void UpdateColours () {
            if (mat != null) {
                Color[] startCols = { water.startCol, sand.startCol, grass.startCol };
                Color[] endCols = { water.endCol, sand.endCol, grass.endCol };
                mat.SetColorArray ("_StartCols", startCols);
                mat.SetColorArray ("_EndCols", endCols);
            }
        }

        Vector2 GetBiomeInfo (float height, Biome[] biomes) {
            // Find current biome
            int biomeIndex = 0;
            float biomeStartHeight = 0;
            for (int i = 0; i < biomes.Length; i++) {
                if (height <= biomes[i].height) {
                    biomeIndex = i;
                    break;
                }
                biomeStartHeight = biomes[i].height;
            }

            Biome biome = biomes[biomeIndex];
            float sampleT = Mathf.InverseLerp (biomeStartHeight, biome.height, height);
            sampleT = (int) (sampleT * biome.numSteps) / (float) Mathf.Max (biome.numSteps, 1);

            // UV stores x: biomeIndex and y: val between 0 and 1 for how close to prev/next biome
            Vector2 uv = new Vector2 (biomeIndex, sampleT);
            return uv;
        }

        void CreateMeshComponents () {
            GameObject holder = null;

            if (meshFilter == null) {
                if (GameObject.Find (meshHolderName)) {
                    holder = GameObject.Find (meshHolderName);
                } else {
                    holder = new GameObject (meshHolderName);
                    holder.AddComponent<MeshRenderer> ();
                    holder.AddComponent<MeshFilter> ();
                }
                meshFilter = holder.GetComponent<MeshFilter> ();
                meshRenderer = holder.GetComponent<MeshRenderer> ();
            }

            if (meshFilter.sharedMesh == null) {
                mesh = new Mesh ();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                meshFilter.sharedMesh = mesh;
            } else {
                mesh = meshFilter.sharedMesh;
                mesh.Clear ();
            }

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void OnValidate () {
            needsUpdate = true;
        }

        [System.Serializable]
        public class Biome {
            [Range (0, 1)]
            public float height;
            public Color startCol;
            public Color endCol;
            public int numSteps;
        }
    }
}