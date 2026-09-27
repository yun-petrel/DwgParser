using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class ViewStyleTemplateAcR2007 : ViewStyleTemplateRecord
{
    public ViewStyleTemplateAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "ViewStyleTemplateAcR2007";

    protected override bool StringsCentralized => true;
}
