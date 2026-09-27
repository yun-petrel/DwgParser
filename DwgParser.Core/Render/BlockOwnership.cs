using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal static class BlockOwnership
{
    public static HashSet<ulong> ContentOwners(DwgObjectReader.WalkResult walk)
    {
        var owners = new HashSet<ulong>(walk.BlockHeaderByHandle.Keys);
        foreach (var e in walk.Entities)
        {
            if (e.BlockHeaderHandle != 0) owners.Add(e.BlockHeaderHandle);
            if (e.DimBlockHandle != 0) owners.Add(e.DimBlockHandle);
        }
        return owners;
    }
}
