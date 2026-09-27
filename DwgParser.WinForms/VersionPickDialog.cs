using DwgParser.Core;

namespace DwgParser.WinForms;

internal sealed class VersionPickDialog : Form
{
    private readonly ComboBox _combo = new();

    public VersionPickDialog(DwgVersion current)
    {
        Text = "选择目标版本";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(360, 110);

        var label = new Label
        {
            Text = "目标版本：",
            Location = new Point(14, 20),
            AutoSize = true
        };

        _combo.DropDownStyle = ComboBoxStyle.DropDownList;
        _combo.Location = new Point(90, 16);
        _combo.Width = 250;
        foreach (var v in Exporters.TargetVersions)
            _combo.Items.Add(Exporters.VersionLabel(v));

        int idx = Array.IndexOf(Exporters.TargetVersions, current);
        _combo.SelectedIndex = idx >= 0 ? idx : 0;

        var ok = new Button
        {
            Text = "确定",
            DialogResult = DialogResult.OK,
            Location = new Point(180, 62),
            Width = 76
        };
        var cancel = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(264, 62),
            Width = 76
        };

        Controls.Add(label);
        Controls.Add(_combo);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public DwgVersion Selected
        => Exporters.TargetVersions[Math.Clamp(_combo.SelectedIndex, 0, Exporters.TargetVersions.Length - 1)];
}
