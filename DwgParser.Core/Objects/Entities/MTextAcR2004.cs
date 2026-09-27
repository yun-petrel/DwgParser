namespace DwgParser.Core.Objects;

internal sealed class MTextAcR2004 : MTextEntity
{
    protected override bool HasRectHeight => false;
    protected override bool HasLineSpacing => true;
    protected override bool HasBackgroundFill => true;
    protected override bool HasAnnotativeData => false;
}
