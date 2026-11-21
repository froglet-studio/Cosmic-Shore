namespace CosmicShore.Engine
{
    /// <summary>
    /// Unity's built-in meshes as serialized references name them
    /// (<c>{fileID: 10202, guid: 0000000000000000e000000000000000}</c> = Cube, …) — the same
    /// shared instances <see cref="GameObject.CreatePrimitive"/> uses.
    /// </summary>
    public static class BuiltinMeshes
    {
        public static Mesh ForFileId(long fileId) => fileId switch
        {
            10202 => PrimitiveMeshes.GetShared(PrimitiveType.Cube),
            10206 => PrimitiveMeshes.GetShared(PrimitiveType.Cylinder),
            10207 => PrimitiveMeshes.GetShared(PrimitiveType.Sphere),
            10208 => PrimitiveMeshes.GetShared(PrimitiveType.Capsule),
            10209 => PrimitiveMeshes.GetShared(PrimitiveType.Plane),
            10210 => PrimitiveMeshes.GetShared(PrimitiveType.Quad),
            _ => null,
        };
    }
}
