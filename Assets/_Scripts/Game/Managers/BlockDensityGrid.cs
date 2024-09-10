using UnityEngine;
using CosmicShore.Core;
using System;


public class BlockDensityGrid
{
    public float resolution = 5f;
    // Assume total grid volume is a cube, specified by one length (skybox diam).  TODO: Import this constant.
    public float totalLength = 1000f;
    public Vector3 origin;
    public Teams team;
    public byte[,,] values;

    public void Init(Teams Team)
    {
        team = Team;
        // Corner of skybox corresponding to array index [0][0][0]
        this.origin = new Vector3(-this.totalLength / 2, -this.totalLength / 2, -this.totalLength / 2);
    }

    public Vector3Int MapCoordinatesToGridIndices(Vector3 coords)
    {
        Vector3 translatedCoords = coords - this.origin;
        Vector3 unroundedIndices = translatedCoords / this.resolution;  // TODO: Integer division
        Vector3Int indices = Vector3Int.RoundToInt(unroundedIndices);   //
        return indices;
    }

    public Vector3 MapGridIndicesToCoordinates(Vector3Int indices)
    {
        Vector3 untranslatedCoords = (Vector3)indices * this.resolution;  // TODO: Use center of grid cell?
        Vector3 coords = untranslatedCoords + this.origin;
        return coords;
    }

    public int GetDensityAtPosition(Vector3 coords)
    {
        Vector3Int indices = MapCoordinatesToGridIndices(coords);
        return this.values[indices[0], indices[1], indices[2]];
    }

    public Vector3 FindDensestRegion()  // TODO: Generalize to top n > 1 regions, if needed.
    {
        int bestValueSoFar = 0;
        Vector3Int bestIndicesSoFar = new Vector3Int(0, 0, 0);
        for (int i = this.values.GetLowerBound(0); i <= this.values.GetUpperBound(0); i++)
            for (int j = this.values.GetLowerBound(1); j <= this.values.GetUpperBound(1); j++)
                for (int k = this.values.GetLowerBound(2); k <= this.values.GetUpperBound(2); k++)
                    if (this.values[i,j,k] > bestValueSoFar)
                    {
                        bestValueSoFar = this.values[i, j, k];
                        bestIndicesSoFar = new Vector3Int(i, j, k);
                    }
        Vector3 bestCoords = MapGridIndicesToCoordinates(bestIndicesSoFar);
        return bestCoords;
    }

    public virtual void AddBlock(TrailBlock block) {}

    public virtual void RemoveBlock(TrailBlock block) {}
}


public class BlockCountDensityGrid : BlockDensityGrid
{
    int nGridPointsPerDimension;
    public BlockCountDensityGrid(Teams team)
    {
        base.Init(team);
        nGridPointsPerDimension = (int) Math.Floor(this.totalLength / this.resolution) + 1;
        values = new byte[nGridPointsPerDimension, nGridPointsPerDimension, nGridPointsPerDimension];
    }

    public override void AddBlock(TrailBlock block)
    {
        Vector3Int indicesOfDestinationCell = MapCoordinatesToGridIndices(block.transform.position);
        if (indicesOfDestinationCell.x >=0 && indicesOfDestinationCell.x < nGridPointsPerDimension &&
            indicesOfDestinationCell.y >=0 && indicesOfDestinationCell.y < nGridPointsPerDimension &&
            indicesOfDestinationCell.z >=0 && indicesOfDestinationCell.z < nGridPointsPerDimension)
            this.values[indicesOfDestinationCell.x, indicesOfDestinationCell.y, indicesOfDestinationCell.z] += 1;
    }

    public override void RemoveBlock(TrailBlock block)
    {
        Vector3Int indicesOfDestinationCell = MapCoordinatesToGridIndices(block.transform.position);

        if (indicesOfDestinationCell.x >= 0 && indicesOfDestinationCell.x < nGridPointsPerDimension &&
            indicesOfDestinationCell.y >= 0 && indicesOfDestinationCell.y < nGridPointsPerDimension &&
            indicesOfDestinationCell.z >= 0 && indicesOfDestinationCell.z < nGridPointsPerDimension)
            this.values[indicesOfDestinationCell.x, indicesOfDestinationCell.y, indicesOfDestinationCell.z] -= 1;
    }
}


//
// Use 2 bytes instead of 1 because a block can have volume up to 10*10*10 > 255.  Assuming blocks can't
// overlap, storage upper bound is 8*10*10*10 = 8 kB (8 side-length-10 blocks whose centers are all barely
// within a 10*10*10 grid block), within ushort's max of 65 kB.
//
//ushort[,,] gridVolumeDensityRuby;
// ...
// Ditto for all combos of {count density, volume density} x {ruby, gold, teal}
public class BlockVolumeDensityGrid : BlockDensityGrid {}