using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal static class AttributeTailReader
{
    public static void Read(BitReader br, DwgEntity e, bool withPrompt, bool hasLockPosition,
        bool hasKeepDuplicate, bool hasLockedInBlock, bool hasMtextType, DwgObjectRecord? record)
    {
        long tagStart = br.BitPosition;
        string tag = br.ReadTV();
        record?.CaptureString(tagStart, br.BitPosition, tag);
        long lockedAnchor = br.BitPosition;
        int locked = -1;
        if (hasLockedInBlock) locked = br.ReadRC();
        long mtextAnchor = br.BitPosition;
        int mtextType = -1;
        if (hasMtextType) mtextType = br.ReadRC();
        short fieldLen = br.ReadBS();
        byte flags = br.ReadRC();
        if (hasLockPosition) br.ReadB();
        long keepAnchor = br.BitPosition;
        int keepDup = -1;
        if (hasKeepDuplicate) keepDup = br.ReadRC();
        string prompt = "";
        if (withPrompt)
        {
            long promptStart = br.BitPosition;
            prompt = br.ReadTV();
            record?.CaptureString(promptStart, br.BitPosition, prompt);
        }

        e.Tag = tag;
        e.Prompt = prompt;
        e.AttFlags = flags;
        e.AttFieldLength = fieldLen;
        e.AttIsLockedInBlock = locked;
        e.AttMTextType = mtextType;
        e.AttKeepDuplicateRecords = keepDup;
        if (record != null)
        {
            record.AttTailLockedBit = lockedAnchor;
            record.AttTailMTextBit = mtextAnchor;
            record.AttTailKeepDuplicateBit = keepAnchor;
            record.AttRoundTripLockedInBlock = locked;
            record.AttRoundTripMTextType = mtextType;
            record.AttRoundTripKeepDuplicateRecords = keepDup;
        }
    }
}
