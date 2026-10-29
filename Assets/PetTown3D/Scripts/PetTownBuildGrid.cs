using UnityEngine;

// Horizontal construction coordinates only. Does not change terrain or existing objects.
public static class PetTownBuildGrid
{
    public static float ValidCellSize(float size)
    {
        return float.IsNaN(size) || float.IsInfinity(size) ? 1f : Mathf.Max(.25f,size);
    }
    public static Vector2Int WorldToCell(Vector3 point,float size,Vector2 origin)
    {
        size=ValidCellSize(size);
        return new Vector2Int(Mathf.RoundToInt((point.x-origin.x)/size),Mathf.RoundToInt((point.z-origin.y)/size));
    }
    public static Vector3 SnapHorizontal(Vector3 point,float size,Vector2 origin)
    {
        size=ValidCellSize(size);
        Vector2Int cell=WorldToCell(point,size,origin);
        return new Vector3(origin.x+cell.x*size,point.y,origin.y+cell.y*size);
    }
}