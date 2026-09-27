namespace DwgParser.Core.Objects.Records;

internal sealed class ViewStyleTemplateAcPreR2007 : ViewStyleTemplateRecord
{
    public ViewStyleTemplateAcPreR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "ViewStyleTemplateAcPreR2007";

    protected override bool StringsCentralized => false;
}
