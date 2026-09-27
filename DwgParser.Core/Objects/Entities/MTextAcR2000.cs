namespace DwgParser.Core.Objects;

internal sealed class MTextAcR2000 : MTextEntity
{
    protected override bool HasRectHeight => false;
    protected override bool HasLineSpacing => true;
    protected override bool HasBackgroundFill => false;
    protected override bool HasAnnotativeData => false;
}
