namespace DwgParser.Core.Objects.Records;

public sealed class MLeaderStyleCapture : RecordIdentity, IMLeaderStyle
{
    public bool Captured;
    public string FailReason = "";
    public long FailBit = -1;
    public long BodyStartBit;
    public long StreamStartBit;
    public string Desc = "";
    public string TextDefault = "";

    public bool UpCaptured;
    public long UpHeadEndBit;
    public long UpDescStartBit;
    public long UpDescEndBit;
    public long UpTextDefStartBit;
    public long UpTextDefEndBit;
    public long UpEedStartBit;
    public long UpEedEndBit;
    public long UpXdicBit;
    public bool MLeaderStyleR2000ToR2010;

    public bool DownCaptured;
    public long DownHeadEndBit;
    public long DownFieldsStartBit;
    public long DownAttachStartBit;
    public long DownStreamStartBit;
    public string DownDesc = "";
    public string DownTextDefault = "";

    public long DownDescInsertBit;
    public long DownTextDefInsertBit;
    public List<(long Start, long End)> DownCmcSpans = new();

    public MLeaderStyleValues? Values;

    public bool ValuesCommitted;

    public MLeaderStyleHandleRefs? HandleRefs;

    public bool HandleStreamCommitted;

    public int HandlePadBits;

    public int HandlePadValue;

    public sealed class MLeaderStyleValues
    {
        public int ClassVersion;
        public int ContentType;
        public int MleaderOrder;
        public int LeaderOrder;
        public int MaxPoints;
        public double FirstSegAngle;
        public double SecondSegAngle;
        public int LeaderType;
        public (int Idx, uint Rgb, byte Flag) LeaderLineColor;
        public int LeaderLineWeight;
        public bool EnableLanding;
        public double LandingGap;
        public bool EnableDogleg;
        public double DoglegLength;
        public double ArrowHeadSize;
        public int AttachLeft;
        public int AttachRight;
        public int TextAngleType;
        public int TextAlignType;
        public (int Idx, uint Rgb, byte Flag) TextColor;
        public double TextHeight;
        public bool EnableTextFrame;
        public bool TextAlwaysLeft;
        public double AlignSpace;
        public (int Idx, uint Rgb, byte Flag) BlockColor;
        public double BlockScaleX;
        public double BlockScaleY;
        public double BlockScaleZ;
        public bool UseBlockScale;
        public double BlockRotation;
        public bool UseBlockRotation;
        public int BlockConnection;
        public double Scale;
        public bool IsChanged;
        public bool IsAnnotative;
        public double BreakSize;
        public string Description = "";
        public string TextDefault = "";
        public int AttachDir;
        public int AttachTop;
        public int AttachBottom;
        public bool TextExtended;
    }

    bool IMLeaderStyle.Captured => Captured;
    bool IMLeaderStyle.DownCaptured => DownCaptured;
    string IMLeaderStyle.Description => Desc;
    string IMLeaderStyle.TextDefault => TextDefault;
    MLeaderStyleCapture.MLeaderStyleValues? IMLeaderStyle.Values => Values;
}
