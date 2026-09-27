using DwgParser.Core.Objects;
using DwgParser.Core.Strings;

namespace DwgParser.Core.Format;

public interface IDwgFile
{

    string? FilePath { get; }

    long FileSize { get; }

    byte[] RawFile { get; }

    DwgVersion Version { get; }

    string VersionName { get; }

    FileHeader Header { get; }


    IReadOnlyList<DwgSection> Sections { get; }

    IReadOnlyList<string> Diagnostics { get; }


    DwgObjectReader.WalkResult GetObjects();

    List<ExtractedString> ExtractStrings(int maxLength = 200);

    List<ClassOccurrence> ScanObjectClasses();

    bool HasPreviewBitmap();

    byte[]? TryExtractPreview();


    void Save(string path);
}