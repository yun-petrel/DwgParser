namespace DwgParser.Core.Format;

public enum OpStatus
{
    Applied,
    Rejected,
    Unapplied,
}

public sealed record OpOutcome(int Index, string OpName, ulong Handle, OpStatus Status,
    bool Verified, string? Reason);

public sealed record EditCounts(int EntitiesBefore, int EntitiesAfter, int ObjectsBefore,
    int ObjectsAfter, int WalkErrors);

public sealed record ReferenceCoverage(int DecodedRecords, int UndecodedRecords, bool RefsComplete);

public sealed record EditReport(string SourceVersion, long SourceBytes, string ProductPath,
    long ProductBytes, bool Written, EditCounts Counts, string FingerprintBefore,
    string FingerprintAfter, ReferenceCoverage Coverage, IReadOnlyList<OpOutcome> Outcomes);

public sealed record EditCandidate(ulong Handle, string Kind, string Text,
    IReadOnlyList<string> Ops);

public sealed record InspectResult(string Version, int DecodedRecords, int UndecodedRecords,
    IReadOnlyList<EditCandidate> Candidates);
