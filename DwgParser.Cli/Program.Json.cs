using DwgParser.Core;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;
using DwgParser.Core.Strings;

namespace DwgParser.Cli;

public static partial class Program
{
    internal static void WriteJsonExport(string outPath, string srcPath, DwgDocument doc,
        DwgObjectReader.WalkResult objs)
        => WriteAtomically(outPath, tmpPath => WriteJsonBody(tmpPath, srcPath, doc, objs));

    private static void WriteEntityCommon(System.Text.Json.Utf8JsonWriter w, DwgObjectRecord r,
        DwgEntity? ent, DwgVersion v, DwgObjectReader.WalkResult objs)
        => ProgramJsonExport.WriteEntityCommon(w, r, ent, v, objs);

    private static void WriteXrecord(System.Text.Json.Utf8JsonWriter w,
        DwgParser.Core.Objects.Records.XrecordCapture? xr)
        => ProgramJsonExport.WriteXrecord(w, xr);

    private static void WriteJsonBody(string path, string srcPath, DwgDocument doc,
        DwgObjectReader.WalkResult objs)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new System.Text.Json.Utf8JsonWriter(fs);

        w.WriteStartObject();
        w.WriteString("file", Path.GetFullPath(srcPath).Replace('\\', '/'));
        w.WriteString("version", doc.Header.VersionString);
        w.WriteString("versionName", doc.VersionName);
        w.WriteNumber("objectsSeen", objs.ObjectsSeen);
        w.WriteNumber("objectsDecoded", objs.ObjectsDecoded);
        w.WriteNumber("walkErrors", objs.Errors);
        w.WriteString("modelSpaceHandle", ProgramRuntime.Hex(objs.ModelSpaceHandle));
        w.WriteNumber("ltscale", objs.Ltscale);
        w.WriteNumber("celtscale", objs.Celtscale);
        w.WriteString("paperSpaceHandle", ProgramRuntime.Hex(objs.PaperSpaceHandle));

        WriteEntities(w, objs);
        WriteLayers(w, objs);
        WriteVports(w, objs);
        WriteBlocks(w, objs);
        WriteTypeCounts(w, doc, objs);
        WriteRecords(w, doc, objs);
        w.WriteEndObject();
        w.Flush();
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (System.IO.IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal static void WriteAtomically(string outPath, Action<string> writeToTemp)
    {
        string tmpPath = outPath + ".tmp";
        try
        {
            writeToTemp(tmpPath);
            File.Move(tmpPath, outPath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(tmpPath);
            throw;
        }
    }
}
