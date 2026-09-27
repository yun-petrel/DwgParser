using DwgParser.Core;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    public sealed partial class WalkResult
    {
        public BlockTable Blocks => _blocks ??= new BlockTable(BlockRegions, Entities, ModelSpaceHandle, PaperSpaceHandle);

        private BlockTable? _blocks;
    }

    public sealed class BlockRegion
    {
        public ulong HeaderHandle { get; init; }
        public int EntityListStart { get; init; }
        public int EntityCount { get; set; }
        public string NameGuess { get; set; } = "";

        public string Name { get; set; } = "";

        public bool IsAnonymous { get; set; }

        public bool IsAnonymousDimBlock { get; set; }

        public Vec2 BasePoint { get; set; }

        public bool HasBasePoint { get; set; }


        public bool FieldsCaptured { get; set; }

        public bool IsXrefRef { get; set; }

        public short XrefResolved { get; set; }

        public bool IsXrefDep { get; set; }

        public bool HasAttrs { get; set; }

        public bool IsXref { get; set; }

        public bool IsXrefOverlaid { get; set; }

        public bool IsXrefLoaded { get; set; }

        public int NumOwned { get; set; } = -1;

        public string XrefPathName { get; set; } = "";

        public string Description { get; set; } = "";

        public int NumInserts { get; set; } = -1;

        public int PreviewSize { get; set; } = -1;

        public byte[]? Preview { get; set; }

        public short InsertUnits { get; set; } = -1;

        public bool? Explodable { get; set; }

        public byte BlockScaling { get; set; }
    }
}
