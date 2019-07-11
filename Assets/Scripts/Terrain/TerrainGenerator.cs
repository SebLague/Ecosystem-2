using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class TerrainGenerator : MonoBehaviour {

    const string meshHolderName = "Terrain Mesh";

    public float waterDepth = .2f;
    public int worldSize = 20;
    [Range (0.2f, 2)]
    public float cellSize = 1;

    public NoiseSettings terrainNoise;
    public Material mat;
    public Material sideMat;

    public Biome water;
    public Biome sand;
    public Biome grass;

    [Header ("Info")]
    public int numLandTiles;
    public int numWaterTiles;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;

    bool needsUpdate;

    void Update () {
        if (needsUpdate) {
            needsUpdate = false;
            Generate ();
        }
    }

    public void Generate () {
        CreateMeshComponents ();
        numLandTiles = 0;
        numWaterTiles = 0;

        var biomes = new Biome[] { water, sand, grass };

        int numCellsPerLine = Mathf.CeilToInt (worldSize / cellSize);
        float actualWorldSize = cellSize * numCellsPerLine;
        float min = -actualWorldSize / 2;
        float[, ] map = HeightmapGenerator.GenerateHeightmap (terrainNoise, numCellsPerLine);

        var vertices = new List<Vector3> ();
        var triangles = new List<int> ();
        var uvs = new List<Vector2> ();
        var normals = new List<Vector3> ();

        var sideTriangles = new List<int> ();

        // 
        Vector3[] normalsSet = { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        Vector2Int[] nswe = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        // Tile verts are in order: NW, NE, SW, SE
        int[][] vertIndexByDir = { new int[] { 3, 2 }, new int[] { 0, 1 }, new int[] { 2, 0 }, new int[] { 1, 3 } };
        Vector3[] sideNormalsByDir = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        for (int y = 0; y < numCellsPerLine; y++) {
            for (int x = 0; x < numCellsPerLine; x++) {
                // Calculate biome
                int biomeIndex = 0;
                float biomeStartHeight = 0;
                for (int i = 0; i < biomes.Length; i++) {
                    if (map[x, y] <= biomes[i].height) {
                        biomeIndex = i;
                        break;
                    }
                    biomeStartHeight = biomes[i].height;
                }

                Biome biome = biomes[biomeIndex];
                float sampleT = Mathf.InverseLerp (biomeStartHeight, biome.height, map[x, y]);
                sampleT = (int) (sampleT * biome.numSteps) / (float) Mathf.Max (biome.numSteps, 1);

                Vector2 uv = new Vector2 (biomeIndex, sampleT);
                uvs.Add (uv);
                uvs.Add (uv);
                uvs.Add (uv);
                uvs.Add (uv);

                bool isWaterTile = biomeIndex == 0;
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

                // Add vertices
                vertices.AddRange (tileVertices);
                normals.AddRange (normalsSet);

                // Add triangles
                triangles.Add (vertIndex);
                triangles.Add (vertIndex + 1);
                triangles.Add (vertIndex + 2);
                triangles.Add (vertIndex + 1);
                triangles.Add (vertIndex + 3);
                triangles.Add (vertIndex + 2);

                if (isWaterTile) {
                    for (int i = 0; i < nswe.Length; i++) {
                        int neighbourX = x + nswe[i].x;
                        int neighbourY = y + nswe[i].y;
                        if (neighbourX >= 0 && neighbourX < numCellsPerLine && neighbourY >= 0 && neighbourY < numCellsPerLine) {
                            float neighbourHeight = map[neighbourX, neighbourY];
                            bool neighbourIsLand = neighbourHeight > biomes[0].height;
                            if (neighbourIsLand) {
                                vertIndex = vertices.Count;
                                int edgeVertIndexA = vertIndexByDir[i][0];
                                int edgeVertIndexB = vertIndexByDir[i][1];
                                vertices.Add (tileVertices[edgeVertIndexA]);
                                vertices.Add (tileVertices[edgeVertIndexA] + Vector3.up * waterDepth);
                                vertices.Add (tileVertices[edgeVertIndexB]);
                                vertices.Add (tileVertices[edgeVertIndexB] + Vector3.up * waterDepth);
                                uvs.AddRange (new Vector2[] { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero });
                                sideTriangles.Add (vertIndex);
                                sideTriangles.Add (vertIndex + 1);
                                sideTriangles.Add (vertIndex + 2);
                                sideTriangles.Add (vertIndex + 1);
                                sideTriangles.Add (vertIndex + 3);
                                sideTriangles.Add (vertIndex + 2);
                                normals.AddRange (new Vector3[] { sideNormalsByDir[i], sideNormalsByDir[i], sideNormalsByDir[i], sideNormalsByDir[i] });
                            }
                        }
                    }
                }
            }
        }

        mesh.subMeshCount = 2;
        mesh.SetVertices (vertices);
        mesh.SetTriangles (triangles, 0, true);
        mesh.SetTriangles (sideTriangles, 1);
        mesh.SetUVs (0, uvs);
        mesh.SetNormals (normals);

        if (mat != null && sideMat != null) {
             meshRenderer.sharedMaterials = new Material[] { mat, sideMat};

            Color[] startCols = { water.startCol, sand.startCol, grass.startCol };
            Color[] endCols = { water.endCol, sand.endCol, grass.endCol };
            mat.SetColorArray ("_StartCols", startCols);
            mat.SetColorArray ("_EndCols", endCols);
        }
    }

    [System.Serializable]
    public class Biome {
        [Range (0, 1)]
        public float height;
        public Color startCol;
        public Color endCol;
        public int numSteps;
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

}