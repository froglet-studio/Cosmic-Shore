using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using CosmicShore.Core;
using System;

[BurstCompile]
public struct FindDensestRegionJob : IJob
{
    [ReadOnly] public NativeArray<byte> values;
    public NativeArray<int3> result; // Will store [maxDensity, x, y, z]
    public int width;
    public int height;
    public int depth;

    public void Execute()
    {
        int maxDensity = 0;
        int3 maxIndices = new int3(0, 0, 0);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    int index = x + y * width + z * width * height;
                    int density = values[index];
                    if (density > maxDensity)
                    {
                        maxDensity = density;
                        maxIndices = new int3(x, y, z);
                    }
                }
            }
        }

        result[0] = new int3(maxDensity, maxIndices.x, maxIndices.y);
        result[1] = new int3(maxIndices.z, 0, 0); // Using first element for z since we only need one value
    }
}

public class BlockDensityGrid
{
    public float Stride = 60f;
    public float totalLength = 1000f;
    public Vector3 origin;
    public Domains Domain;
    public byte[,,] values;

    protected int nGridPointsPerDimension;
    protected NativeArray<byte> jobValues;
    protected NativeArray<int3> jobResult;
    protected bool jobSystemInitialized = false;

    public void Init(Domains domain)
    {
        Domain = domain;
        this.origin = new Vector3(-this.totalLength / 2, -this.totalLength / 2, -this.totalLength / 2);
        nGridPointsPerDimension = (int)Math.Floor(this.totalLength / this.Stride) + 1;
        
        // Initialize job system arrays
        int totalSize = nGridPointsPerDimension * nGridPointsPerDimension * nGridPointsPerDimension;
        jobValues = new NativeArray<byte>(totalSize, Allocator.Persistent);
        jobResult = new NativeArray<int3>(2, Allocator.Persistent); // Store result and extra data
        jobSystemInitialized = true;
    }

    protected void OnDestroy()
    {
        if (jobSystemInitialized)
        {
            jobValues.Dispose();
            jobResult.Dispose();
        }
    }

    public Vector3Int MapCoordinatesToGridIndices(Vector3 coords)
    {
        Vector3 translatedCoords = coords - this.origin;
        Vector3 unroundedIndices = translatedCoords / this.Stride;
        Vector3Int indices = Vector3Int.RoundToInt(unroundedIndices);
        return indices;
    }

    public Vector3 MapGridIndicesToCoordinates(Vector3Int indices)
    {
        Vector3 untranslatedCoords = (Vector3)indices * this.Stride;
        Vector3 coords = untranslatedCoords + this.origin;
        return coords;
    }

    public int GetDensityAtPosition(Vector3 coords)
    {
        Vector3Int indices = MapCoordinatesToGridIndices(coords);
        return this.values[indices[0], indices[1], indices[2]];
    }

    protected void UpdateJobValues()
    {
        // Convert 3D array to flat array for job system
        for (int x = 0; x < nGridPointsPerDimension; x++)
        {
            for (int y = 0; y < nGridPointsPerDimension; y++)
            {
                for (int z = 0; z < nGridPointsPerDimension; z++)
                {
                    int index = x + y * nGridPointsPerDimension + z * nGridPointsPerDimension * nGridPointsPerDimension;
                    jobValues[index] = values[x, y, z];
                }
            }
        }
    }

    public Vector3 FindDensestRegion()
    {
        if (!jobSystemInitialized) return Vector3.zero;

        UpdateJobValues();

        var job = new FindDensestRegionJob
        {
            values = jobValues,
            result = jobResult,
            width = nGridPointsPerDimension,
            height = nGridPointsPerDimension,
            depth = nGridPointsPerDimension
        };

        JobHandle handle = job.Schedule();
        handle.Complete();

        // Extract results
        int3 result1 = jobResult[0];
        int3 result2 = jobResult[1];
        Vector3Int bestIndices = new Vector3Int(result1.y, result1.z, result2.x);

        return MapGridIndicesToCoordinates(bestIndices);
    }

    public virtual void AddBlock(Prism block) {}

    public virtual void RemoveBlock(Prism block) {}
}

public class BlockCountDensityGrid : BlockDensityGrid
{
    public BlockCountDensityGrid(Domains domain)
    {
        base.Init(domain);
        values = new byte[nGridPointsPerDimension, nGridPointsPerDimension, nGridPointsPerDimension];
    }

    public override void AddBlock(Prism block)
    {
        Vector3Int indicesOfDestinationCell = MapCoordinatesToGridIndices(block.transform.position);
        if (indicesOfDestinationCell.x >= 0 && indicesOfDestinationCell.x < nGridPointsPerDimension &&
            indicesOfDestinationCell.y >= 0 && indicesOfDestinationCell.y < nGridPointsPerDimension &&
            indicesOfDestinationCell.z >= 0 && indicesOfDestinationCell.z < nGridPointsPerDimension)
            this.values[indicesOfDestinationCell.x, indicesOfDestinationCell.y, indicesOfDestinationCell.z] += 1;
    }

    public override void RemoveBlock(Prism block)
    {
        Vector3Int indicesOfDestinationCell = MapCoordinatesToGridIndices(block.transform.position);
        if (indicesOfDestinationCell.x >= 0 && indicesOfDestinationCell.x < nGridPointsPerDimension &&
            indicesOfDestinationCell.y >= 0 && indicesOfDestinationCell.y < nGridPointsPerDimension &&
            indicesOfDestinationCell.z >= 0 && indicesOfDestinationCell.z < nGridPointsPerDimension)
            this.values[indicesOfDestinationCell.x, indicesOfDestinationCell.y, indicesOfDestinationCell.z] -= 1;
    }
}

public class BlockVolumeDensityGrid : BlockDensityGrid {}
