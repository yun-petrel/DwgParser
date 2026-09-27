namespace DwgParser.Core.Objects;

internal sealed class MTextAcR2018Plus : MTextEntity
{
    protected override bool HasRectHeight => true;
    protected override bool HasLineSpacing => true;
    protected override bool HasBackgroundFill => true;
    protected override bool HasAnnotativeData => true;
}
