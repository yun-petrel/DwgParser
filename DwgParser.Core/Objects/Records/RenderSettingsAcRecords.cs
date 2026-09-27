namespace DwgParser.Core.Objects.Records;


file static class Fs
{
    public static (string Label, RenderSettingKind Kind) Bl(string label) => (label, RenderSettingKind.Int32);
    public static (string Label, RenderSettingKind Kind) Bs(string label) => (label, RenderSettingKind.Int16);
    public static (string Label, RenderSettingKind Kind) Bd(string label) => (label, RenderSettingKind.Real);
    public static (string Label, RenderSettingKind Kind) B(string label) => (label, RenderSettingKind.Flag);
    public static (string Label, RenderSettingKind Kind) T(string label) => (label, RenderSettingKind.Text);
}

public abstract class MentalRayRenderSettingsRecord : RenderSettingsRecord
{
    private static readonly (string Label, RenderSettingKind Kind)[] TailFieldTable =
    {
        Fs.Bl("mr_version"), Fs.Bl("sampling1"), Fs.Bl("sampling2"), Fs.Bs("sampling_mr_filter"),
        Fs.Bd("sampling_filter1"), Fs.Bd("sampling_filter2"),
        Fs.Bd("sampling_contrast_color1"), Fs.Bd("sampling_contrast_color2"),
        Fs.Bd("sampling_contrast_color3"), Fs.Bd("sampling_contrast_color4"),
        Fs.Bs("shadow_mode"), Fs.B("shadow_maps_enabled"), Fs.B("ray_tracing_enabled"),
        Fs.Bl("ray_trace_depth1"), Fs.Bl("ray_trace_depth2"), Fs.Bl("ray_trace_depth3"),
        Fs.B("global_illumination_enabled"), Fs.Bl("gi_sample_count"), Fs.B("gi_sample_radius_enabled"),
        Fs.Bd("gi_sample_radius"), Fs.Bl("gi_photons_per_light"),
        Fs.Bl("photon_trace_depth1"), Fs.Bl("photon_trace_depth2"), Fs.Bl("photon_trace_depth3"),
        Fs.B("final_gathering_enabled"), Fs.Bl("fg_ray_count"),
        Fs.B("fg_sample_radius_state1"), Fs.B("fg_sample_radius_state2"), Fs.B("fg_sample_radius_state3"),
        Fs.Bd("fg_sample_radius1"), Fs.Bd("fg_sample_radius2"), Fs.Bd("light_luminance_scale"),
        Fs.Bs("diagnostics_mode"), Fs.Bs("diagnostics_grid_mode"), Fs.Bd("diagnostics_grid_float"),
        Fs.Bs("diagnostics_photon_mode"), Fs.Bs("diagnostics_bsp_mode"), Fs.B("export_mi_enabled"),
        Fs.T("mr_description"), Fs.Bl("tile_size"), Fs.Bs("tile_order"), Fs.Bl("memory_limit"),
        Fs.B("diagnostics_samples_mode"), Fs.Bd("energy_multiplier"),
    };

    protected MentalRayRenderSettingsRecord(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override IReadOnlyList<(string Label, RenderSettingKind Kind)> TailFields => TailFieldTable;

    public const int TailFieldCount = 44;
}

public abstract class RapidRtRenderSettingsRecord : RenderSettingsRecord
{
    private static readonly (string Label, RenderSettingKind Kind)[] TailFieldTable =
    {
        Fs.Bl("rapidrt_version"), Fs.Bl("render_target"), Fs.Bl("render_level"),
        Fs.Bl("render_time"), Fs.Bl("lighting_model"), Fs.Bl("filter_type"),
        Fs.Bd("filter_width"), Fs.Bd("filter_height"), Fs.B("has_predefined"),
    };

    protected RapidRtRenderSettingsRecord(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override IReadOnlyList<(string Label, RenderSettingKind Kind)> TailFields => TailFieldTable;

    public const int TailFieldCount = 9;
}


internal sealed class MentalRayRenderSettingsAcR2000 : MentalRayRenderSettingsRecord
{
    public MentalRayRenderSettingsAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2000";
}

internal sealed class MentalRayRenderSettingsAcR2004 : MentalRayRenderSettingsRecord
{
    public MentalRayRenderSettingsAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2004";

    protected override bool HasXdicMissing => true;
}

internal sealed class MentalRayRenderSettingsAcR2007 : MentalRayRenderSettingsRecord
{
    public MentalRayRenderSettingsAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2007";

    protected override bool HasXdicMissing => true;

    protected override bool StringsCentralized => true;
}

internal sealed class RapidRtRenderSettingsAcR2000 : RapidRtRenderSettingsRecord
{
    public RapidRtRenderSettingsAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2000";
}

internal sealed class RapidRtRenderSettingsAcR2004 : RapidRtRenderSettingsRecord
{
    public RapidRtRenderSettingsAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2004";

    protected override bool HasXdicMissing => true;
}

internal sealed class RapidRtRenderSettingsAcR2007 : RapidRtRenderSettingsRecord
{
    public RapidRtRenderSettingsAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2007";

    protected override bool HasXdicMissing => true;

    protected override bool StringsCentralized => true;
}
