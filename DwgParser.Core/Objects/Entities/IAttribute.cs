namespace DwgParser.Core.Objects;

public interface IAttribute : IEntity
{
    string Tag { get; }

    string Prompt { get; }

    byte AttFlags { get; }

    short AttFieldLength { get; }

    int AttIsLockedInBlock { get; }

    int AttKeepDuplicateRecords { get; }

    int AttMTextType { get; }
}
