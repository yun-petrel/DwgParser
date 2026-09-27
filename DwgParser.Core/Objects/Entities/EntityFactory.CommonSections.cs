namespace DwgParser.Core.Objects;

public static partial class EntityFactory
{
    internal enum EntitySectionBucket
    {
        R2000,
        R2004,
        R2007,
        R2010,
        R2013,
    }

    private static EntitySectionBucket SectionBucketOf(DwgVersion version)
    {
        if (version.IsR2000()) return EntitySectionBucket.R2000;
        if (version >= DwgVersion.AC1027) return EntitySectionBucket.R2013;
        if (version == DwgVersion.AC1024) return EntitySectionBucket.R2010;
        if (version == DwgVersion.AC1021) return EntitySectionBucket.R2007;
        return EntitySectionBucket.R2004;
    }

    public static CommonEntityData CreateCommonEntityData(DwgVersion version)
        => SectionBucketOf(version) switch
        {
            EntitySectionBucket.R2000 => new CommonEntityDataAcR2000(),
            EntitySectionBucket.R2007 => new CommonEntityDataAcR2007(),
            EntitySectionBucket.R2010 => new CommonEntityDataAcR2010(),
            EntitySectionBucket.R2013 => new CommonEntityDataAcR2013(),
            _ => new CommonEntityDataAcR2004(),
        };

    public static EntityHandleStream CreateEntityHandleStream(DwgVersion version)
        => SectionBucketOf(version) switch
        {
            EntitySectionBucket.R2000 => new EntityHandleStreamAcR2000(),
            EntitySectionBucket.R2004 => new EntityHandleStreamAcR2004(),
            EntitySectionBucket.R2007 => new EntityHandleStreamAcR2007(),
            _ => (EntityHandleStream)new EntityHandleStreamAcR2010Plus(),
        };
}
