namespace DwgParser.Core.Format;

public abstract record EditOp(ulong? Target);

public sealed record SetTextOp(ulong Handle, string Value) : EditOp(Handle);

public sealed record SetAttribOp(ulong Handle, string? Text, string? Tag) : EditOp(Handle);

public sealed record DeleteOp(ulong Handle) : EditOp(Handle);

public sealed record AddMTextOp(double X, double Y, string Value, double Height,
    string? Layer) : EditOp((ulong?)null);
