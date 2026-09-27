using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects.Records;

public abstract partial class LayerRecord
{
    private void CommitColorFields(short colorIndex, uint trueColor, string colorName, string colorBookName)
        => Cap.CommitColor(colorIndex, trueColor, colorName, colorBookName);

    private bool CommitNameAndPublish(string name)
    {
        bool named = !string.IsNullOrEmpty(name);
        Cap.CommitName(named ? name : "");
        if (named) Result.LayerNames[Handle] = name;
        return named;
    }

    private void CommitLayerColorIfLanded(bool named, short colorIndex, uint trueColor)
    {
        if (!named) return;
        if (!UsesCmcColor)
        {
            bool colorLanded = _cursor != null && _cursor.BitPosition == Rec.HandleStreamStartBit;
            if (colorLanded && colorIndex is >= 0 and <= 256)
            {
                Result.LayerColors[Handle] = colorIndex;
                Cap.CommitLayerColor(colorIndex);
            }
            return;
        }
        uint payload = trueColor & 0xFFFFFF;
        int method = (int)((trueColor >> 24) & 0xFF);
        if (method == AciPalette.CmcMethodTrueColor && payload is not 0 and not 0xFFFFFF)
        {
            int aci = AciPalette.FromCmcTrueColor(trueColor);
            if (aci is > 0 and < AciPalette.Count)
            {
                Result.LayerColors[Handle] = (short)aci;
                Cap.CommitLayerColor((short)aci);
            }
            return;
        }
        if (method == AciPalette.CmcMethodByAci && payload is >= 1 and <= 255)
        {
            Result.LayerColors[Handle] = (short)payload;
            Cap.CommitLayerColor((short)payload);
        }
    }
}
