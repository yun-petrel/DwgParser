namespace DwgParser.Core.Objects;

public interface IMLeader : IEntity
{
    int VertexCount { get; }

    long ClassVersion { get; }

    System.Collections.Generic.List<MLeaderNode> Leaders { get; }

    int LeaderCount { get; }

    double ContextScale { get; }

    (double X, double Y, double Z) ContentBase { get; }

    double MTextHeight { get; }

    double CtxArrowSize { get; }

    double LandingGap { get; }

    MLeaderColor? ContextTextColor { get; }

    MLeaderColor? ContextBackgroundColor { get; }

    MLeaderColor? ContextBlockColor { get; }

    long CtxTextLeft { get; }

    long CtxTextRight { get; }

    long CtxTextAngleType { get; }

    long CtxTextAlignment { get; }

    long CtxTextTop { get; }

    long CtxTextBottom { get; }

    bool HasTextContent { get; }

    bool HasBlockContent { get; }

    string ContentType { get; }

    string ContentText { get; }

    (double X, double Y, double Z) ContentLocation { get; }

    (double X, double Y, double Z) ContentDirection { get; }

    double ContentRotation { get; }

    double ContentWidth { get; }

    double ContentHeight { get; }

    double MTextLineSpacingFactor { get; }

    long MTextLineSpacingStyle { get; }

    long MTextAlignment { get; }

    long MTextFlow { get; }

    double MTextBgScale { get; }

    long MTextBgTransparency { get; }

    bool MTextIsBgFill { get; }

    bool MTextIsBgMaskFill { get; }

    long MTextColType { get; }

    bool MTextIsHeightAuto { get; }

    double MTextColWidth { get; }

    double MTextColGutter { get; }

    bool MTextIsColFlowReversed { get; }

    bool MTextWordBreak { get; }

    bool MTextUnknownTailBit { get; }

    System.Collections.Generic.List<double> MTextColSizes { get; }

    bool IsNormalReversed { get; }

    long TopLineWeight { get; }

    MLeaderColor? TopLineColor { get; }

    MLeaderColor? TopTextColor { get; }

    MLeaderColor? TopBlockColor { get; }

    System.Collections.Generic.List<(string Text, long UiIndex, double Width)> BlockLabels { get; }

    System.Collections.Generic.List<bool> ArrowheadDefaults { get; }

    bool IsNegTextDir { get; }

    (double X, double Y, double Z) Base { get; }

    (double X, double Y, double Z) BaseDir { get; }

    (double X, double Y, double Z) BaseVert { get; }

    long OverrideFlags { get; }

    long TypeBS { get; }

    bool HasLanding { get; }

    bool HasDogleg { get; }

    double DoglegLength { get; }

    double TopArrowSize { get; }

    long StyleContent { get; }

    long TextLeft { get; }

    long TextRight { get; }

    long TextAngleType { get; }

    long TextAlignment { get; }

    double BlockRotation { get; }

    (double X, double Y, double Z) BlockScale { get; }

    long StyleAttachment { get; }

    bool IsAnnotative { get; }

    bool HasTextFrame { get; }

    double ScaleFactor { get; }

    long IpeAlignment { get; }

    long Justification { get; }

    int ArrowheadCount { get; }

    int BlockLabelCount { get; }

    long AttachDir { get; }

    long AttachTop { get; }

    long AttachBottom { get; }

    bool IsTextExtended { get; }

    ulong MLeaderStyleHandle { get; }

    ulong ContentStyleHandle { get; }
}
