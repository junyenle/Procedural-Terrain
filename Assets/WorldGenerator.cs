using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum NoiseType { Perlin, FBM, Warp, Turbulence, Ridge, Random }

public class WorldGenerator : MonoBehaviour
{
    [Header("High level")]
    public bool updateContinuously = false;
    public bool combineMeshes = true;
    public bool generateCollisionMesh = false;

    // would love to put these in a struct, but they don't play nice with Unity's inspector
    [Header("Color")]
    public List<float> terrainHeightThresholds;
    public List<Color> terrainHeightColors;

    [Header("Shape")]
    public int xBlocks;
    public int yBlocks;
    public int zBlocks;

    [Header("Noise")]
    public NoiseType noiseType;
    [Range(0, 10)]
    public float amplitude;
    [Range(0, 2)]
    public float frequency;
    [Range(0, 10)]
    public float noiseOffset; // allows us to scroll through the noise function
    [Range(1, 10)]
    public int octaves;
    [Range(0, 1)]
    public float heightOffset; // necessary to offset height upwards for some of the noise types

    void Update()
    {
        Vector3 center = new Vector3(xBlocks, yBlocks, zBlocks) / 2.0f;
        transform.RotateAround(center, new Vector3(0, 1, 0), Time.deltaTime * 4);
    }

    public void Ungenerate()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }

    public void Generate()
    {
        Ungenerate();

        for (int x = 0; x < xBlocks; x++)
        {
            for (int z = 0; z < zBlocks; z++)
            {
                float height = amplitude * Noise2(x * frequency + noiseOffset, z * frequency + noiseOffset);
                height += heightOffset;
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                MeshRenderer meshRenderer = cube.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = new Material(Shader.Find("Standard"));
                for (int i = 0; i < terrainHeightThresholds.Count; i++)
                {
                    if (height > terrainHeightThresholds[i])
                    {
                        meshRenderer.sharedMaterial.color = terrainHeightColors[i];
                    }
                }

                cube.transform.position = new Vector3(x, height * yBlocks / 2, z);
                cube.transform.localScale = new Vector3(1, height * yBlocks, 1);

                // for organization purposes, make the cube a child of the generator
                cube.name = "(" + x + ", " + z + ")";
                cube.transform.SetParent(this.transform);
            }
        }
        if (combineMeshes)
        {
            CombineMeshes();
        }
    }

    public float Noise2(float x, float z)
    {
        x = x * frequency + noiseOffset;
        z = z * frequency + noiseOffset;
        switch (noiseType)
        {
            case NoiseType.Warp:
            {
                // taken from https://iquilezles.org/articles/warp/
                float inner = Perlin.Fbm(x, z, octaves);
                float middle = Perlin.Fbm(x + inner, z + inner, octaves);
                float outer = Perlin.Fbm(x + middle, z + middle, octaves);
                return outer;
            }
            case NoiseType.Ridge: return Perlin.Ridge(x, z, octaves);
            case NoiseType.Turbulence: return Perlin.Turbulence(x, z, octaves);
            case NoiseType.FBM: return Perlin.Fbm(x, z, octaves);
            case NoiseType.Perlin: return Mathf.PerlinNoise(x, z);
            case NoiseType.Random:
            default:
            {
                return Random.Range(0.0f, 1.0f);
            }
        }
    }

    // based on https://docs.unity3d.com/ScriptReference/Mesh.CombineMeshes.html
    // adapted to account for the maximum indexible 65536 vertices using uint16 indices
    // as well as for the need to combine meshes of each color separately
    public void CombineMeshes()
    {
        // we have to combine cubes of each color separately, as each mesh only has one material
        int numColors = terrainHeightThresholds.Count;
        // these are all cubes, so they each have 24 vertices and should fit perfectly in groups of 65520
        // could be optimized to fit entire 65536 per mesh, but is less convenient and seems unnecessary
        int cubeVertices = 24;
        int maxMeshVertices = 65520;
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();

        // combine instances collect cube meshes as we build up to 65520 vertices
        List<CombineInstance>[] combineInstances = new List<CombineInstance>[numColors];
        for (int i = 0; i < numColors; i++)
        {
            combineInstances[i] = new List<CombineInstance>();
        }
        // keeps track of how many cubes we've added to the current combine instance, per color
        int[] meshHolderVertexIterator = new int[numColors];

        // iterate through cubes, combining their meshes in groups of 65520 and placing them onto mesh holders
        for (int i = 0; i < meshFilters.Length; i++)
        {
            // find the color of this cube
            Color color = meshRenderers[i].sharedMaterial.color;
            int colorIndex = 0;
            for (int j = 0; j < numColors; j++)
            {
                if (color == terrainHeightColors[j])
                {
                    colorIndex = j;
                    break;
                }
            }
            CombineInstance combine = new CombineInstance();
            combine.mesh = meshFilters[i].sharedMesh;
            combine.transform = meshFilters[i].transform.localToWorldMatrix;
            DestroyImmediate(meshFilters[i].gameObject);
            // if we have overshot the maximum vertices for this mesh holder, dump our current vertices into a mesh holder
            if (cubeVertices * meshHolderVertexIterator[colorIndex] >= maxMeshVertices)
            {
                CreateMeshHolder(combineInstances[colorIndex].ToArray(), colorIndex);
                combineInstances[colorIndex].Clear();
                meshHolderVertexIterator[colorIndex] = 0;
            }
            combineInstances[colorIndex].Add(combine);
            meshHolderVertexIterator[colorIndex]++;
        }
        // handle the last mesh holder
        for (int i = 0; i < numColors; i++)
        {
            CreateMeshHolder(combineInstances[i].ToArray(), i);
        }
    }

    void CreateMeshHolder(CombineInstance[] combineInstances, int colorIndex)
    {
        GameObject meshHolder = new GameObject();
        meshHolder.name = "combined mesh";
        meshHolder.transform.parent = this.transform;
        MeshFilter meshFilter = meshHolder.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = new Mesh();
        meshFilter.sharedMesh.CombineMeshes(combineInstances);
        MeshRenderer meshRenderer = meshHolder.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = new Material(Shader.Find("Standard"));
        meshRenderer.sharedMaterial.color = terrainHeightColors[colorIndex];
        if (generateCollisionMesh)
        {
            MeshCollider meshCollider = meshHolder.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
        }
    }

    private void OnDrawGizmos()
    {
        Vector3 size = new Vector3(xBlocks, yBlocks, zBlocks);
        Vector3 center = size / 2.0f;
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(center, size);
    }
}
