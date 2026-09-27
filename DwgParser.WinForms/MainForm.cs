using DwgParser.Core;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;

namespace DwgParser.WinForms;

internal sealed partial class MainForm : Form
{
    private const int MaxListRows = 20000;

    private readonly DwgCanvas _canvas = new();
    private readonly ListView _entityList = new();
    private readonly DataGridView _propGrid = new();
    private readonly Label _propHeader = new();
    private readonly ToolStripStatusLabel _statusFile = new();
    private readonly ToolStripStatusLabel _statusCursor = new();
    private readonly ToolStripStatusLabel _statusView = new();

    private DwgScene? _scene;
    private List<PropRow> _rows = new();
    private bool _syncingList;

    public MainForm(string? initialPath)
    {
        BuildUi();
        WireEvents();
        _statusFile.Text = "未载入图纸";
        Shown += (_, _) =>
        {
            ApplySplitterDefaults();
            if (initialPath != null) OpenFile(initialPath);
        };
    }

    private void ApplySplitterDefaults()
    {
        try
        {
            _splitOuter.Panel1MinSize = 180;
            _splitInner.Panel2MinSize = 240;
            _splitOuter.SplitterDistance = 280;
            _splitInner.SplitterDistance = Math.Max(_splitInner.Panel1MinSize,
                                                     _splitInner.Width - 340);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
        }
    }

    private void WireEvents()
    {
        _canvas.SelectionChanged += (_, _) => OnSelectionChanged();
        _canvas.CursorMoved += (_, p) => _statusCursor.Text =
            string.Format("X = {0:0.####}   Y = {1:0.####}", p.X, p.Y);
        _canvas.ViewChanged += (_, _) => _statusView.Text = _canvas.View.Describe();

        _entityList.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingList || _scene == null) return;
            int i = _entityList.SelectedIndices.Count > 0 ? _entityList.SelectedIndices[0] : -1;
            _canvas.SelectEntity(i >= 0 && i < _scene.AllEntities.Count ? _scene.AllEntities[i] : null);
        };


        _propGrid.CellBeginEdit += PropGrid_CellBeginEdit;
        _propGrid.CellEndEdit += PropGrid_CellEndEdit;
        _propGrid.DataError += (_, e) => e.ThrowException = false;

        DragEnter += (_, e) =>
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files
                && File.Exists(files[0]))
                OpenFile(files[0]);
        };
    }


    private void PromptOpen()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "打开 DWG",
            Filter = "DWG 图纸 (*.dwg)|*.dwg|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) OpenFile(dlg.FileName);
    }

    private void OpenFile(string path)
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            var scene = DwgScene.Load(path);
            _scene = scene;
            _canvas.SetScene(scene);
            RefreshEntityList();
            RebuildLayerBar();
            RefreshProperties();
            RefreshStatus();
            Text = $"DwgParser 查看器 — {scene.DisplayName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "打开失败：\n" + ex.Message, "错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }


    private void RefreshEntityList()
    {
        _syncingList = true;
        try
        {
            _entityList.BeginUpdate();
            _entityList.Items.Clear();
            if (_scene != null)
            {
                var all = _scene.AllEntities;
                int n = Math.Min(all.Count, MaxListRows);
                var items = new ListViewItem[n];
                for (int i = 0; i < n; i++)
                {
                    var e = all[i];
                    items[i] = new ListViewItem(new[]
                    {
                        $"0x{e.Handle:X6}",
                        e.Kind.ToString(),
                        _scene.LayerNameOf(e.LayerHandle),
                        e.ColorIndex.ToString()
                    });
                }
                _entityList.Items.AddRange(items);
            }
        }
        finally
        {
            _entityList.EndUpdate();
            _syncingList = false;
        }
        SyncListSelection();
    }

    private void RefreshProperties()
    {
        _propGrid.Rows.Clear();
        var e = _canvas.Selected;
        if (_scene == null || e == null)
        {
            _rows = new List<PropRow>();
            _propHeader.Text = "未选中实体（在画布上单击，或从左侧列表选择）";
            return;
        }

        _rows = EntityInspector.Build(e, _scene);
        _propHeader.Text = $"{e.Kind}  句柄 0x{e.Handle:X}  —— 改值后视图立即重绘";
        foreach (var row in _rows)
        {
            int idx = _propGrid.Rows.Add(row.Name, row.Value);
            var cell = _propGrid.Rows[idx].Cells[1];
            if (row.IsReadOnly)
            {
                cell.ReadOnly = true;
                cell.Style.BackColor = SystemColors.Control;
                cell.Style.ForeColor = SystemColors.GrayText;
            }
            else if (row.Kind is PropKind.Choice or PropKind.Boolean)
            {
                cell.ReadOnly = true;
                cell.ToolTipText = "可选：" + string.Join(" / ", row.Choices);
                cell.Style.BackColor = Color.FromArgb(245, 250, 255);
            }
        }
    }

    private void RefreshStatus()
    {
        if (_scene == null)
        {
            _statusFile.Text = "未载入图纸";
            return;
        }
        int hidden = 0;
        foreach (var layer in _scene.Layers)
            if (_scene.IsLayerHidden(layer.Handle)) hidden++;

        string filter = _activeLayer is { } only ? $"  只看图层「{_scene.LayerNameOf(only)}」" : "";
        string diag = _scene.Document.Diagnostics.Count > 0
            ? $"  诊断 {_scene.Document.Diagnostics.Count} 条" : "";
        _statusFile.Text = $"{Exporters.Describe(_scene)}  实体 {_scene.VisibleEntities.Count}/{_scene.AllEntities.Count}"
            + (hidden > 0 ? $"  隐藏图层 {hidden}" : "") + filter + diag;
        _statusView.Text = _canvas.View.Describe();
    }

    private void OnSelectionChanged()
    {
        SyncListSelection();
        RefreshProperties();
    }

    private void SyncListSelection()
    {
        if (_syncingList || _scene == null) return;
        var sel = _canvas.Selected;
        _syncingList = true;
        try
        {
            _entityList.SelectedItems.Clear();
            if (sel == null) return;
            int idx = _scene.IndexOfEntity(sel);
            if (idx >= 0 && idx < _entityList.Items.Count)
            {
                _entityList.Items[idx].Selected = true;
                _entityList.Items[idx].EnsureVisible();
            }
        }
        finally
        {
            _syncingList = false;
        }
    }


    private void PropGrid_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
    {
        if (e.ColumnIndex != 1 || e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
        var row = _rows[e.RowIndex];
        if (row.Kind is not (PropKind.Choice or PropKind.Boolean)) return;

        e.Cancel = true;
        BeginInvoke(() => ShowChoiceMenu(e.RowIndex, row));
    }

    private void ShowChoiceMenu(int rowIndex, PropRow row)
    {
        if (row.Choices.Length == 0) return;
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        foreach (var choice in row.Choices)
        {
            var item = new ToolStripMenuItem(choice) { Checked = choice == row.Value };
            item.Click += (_, _) => ApplyEdit(rowIndex, row, choice);
            menu.Items.Add(item);
        }
        var cell = _propGrid.Rows[rowIndex].Cells[1];
        var rect = _propGrid.GetCellDisplayRectangle(1, rowIndex, true);
        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(_propGrid, rect.Left, rect.Bottom);
    }

    private void PropGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.ColumnIndex != 1 || e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
        var row = _rows[e.RowIndex];
        if (row.IsReadOnly) return;
        string text = _propGrid.Rows[e.RowIndex].Cells[1].Value?.ToString() ?? "";
        if (text == row.Value) return;
        ApplyEdit(e.RowIndex, row, text);
    }

    private void ApplyEdit(int rowIndex, PropRow row, string text)
    {
        if (_scene == null || row.Apply is null) return;
        var entity = _canvas.Selected;
        if (entity == null) return;

        string? error;
        try
        {
            error = row.Apply(text);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        if (error != null)
        {
            MessageBox.Show(this, $"「{row.Name}」改值失败：\n{error}", "无效输入",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        RefreshProperties();
        UpdateListRow(entity);
        if (error == null)
        {
            _canvas.InvalidateRender();
            RefreshStatus();
        }
    }

    private void UpdateListRow(DwgEntity entity)
    {
        if (_scene == null) return;
        int idx = _scene.IndexOfEntity(entity);
        if (idx < 0 || idx >= _entityList.Items.Count) return;
        var item = _entityList.Items[idx];
        if (item.SubItems.Count < 4) return;
        item.SubItems[1].Text = entity.Kind.ToString();
        item.SubItems[2].Text = _scene.LayerNameOf(entity.LayerHandle);
        item.SubItems[3].Text = entity.ColorIndex.ToString();
    }


    private void SetDark(bool dark)
    {
        if (_canvas.DarkBackground == dark) return;
        _canvas.DarkBackground = dark;
        _miDark.Checked = dark;
        _btnDark.Checked = dark;
    }

    private void PromptExport(string kind)
    {
        if (_scene == null) { WarnNoScene(); return; }
        using var dlg = new SaveFileDialog
        {
            Title = "导出 " + kind.ToUpperInvariant(),
            Filter = kind switch
            {
                "png" => "PNG 图片 (*.png)|*.png",
                "svg" => "SVG 矢量图 (*.svg)|*.svg",
                _ => "JSON (*.json)|*.json"
            },
            FileName = Path.GetFileNameWithoutExtension(_scene.DisplayName) + "." + kind
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        Cursor = Cursors.WaitCursor;
        try
        {
            switch (kind)
            {
                case "png":
                    Exporters.SavePng(_scene, dlg.FileName, 2048, 2048,
                                      _canvas.Surface.LineThickness, _canvas.DarkBackground);
                    break;
                case "svg":
                    Exporters.SaveSvg(_scene, dlg.FileName, _canvas.DarkBackground);
                    break;
                default:
                    Exporters.SaveJson(_scene, dlg.FileName);
                    break;
            }
            MessageBox.Show(this, "已导出：\n" + dlg.FileName, "导出完成",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：\n" + ex.Message, "错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void PromptSaveAsDwg()
    {
        if (_scene == null) { WarnNoScene(); return; }

        var answer = MessageBox.Show(this,
            "另存为 DWG 走的是本仓写侧**帧级位切片搬运**（同版本重建对象流 / 跨版本按类型手术）。\n\n"
            + "属性面板里**只有属性块（ATTRIB）的「文本 / 标记」两行会写进这个文件**"
            + "（走 DwgAttributeEditor 的值级改写）；其余改动（颜色、图层、几何、普通文字…）"
            + "**不会**落盘 —— 写侧不是通用的「模型 → 字节」重编码。\n\n是否继续？",
            "另存为 DWG", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
        if (answer != DialogResult.OK) return;

        using var versionDlg = new VersionPickDialog(_scene.Document.Version);
        if (versionDlg.ShowDialog(this) != DialogResult.OK) return;

        using var dlg = new SaveFileDialog
        {
            Title = "另存为 DWG",
            Filter = "DWG 图纸 (*.dwg)|*.dwg",
            FileName = Path.GetFileNameWithoutExtension(_scene.DisplayName)
                       + "_" + versionDlg.Selected.GetVersionName() + ".dwg"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        Cursor = Cursors.WaitCursor;
        try
        {
            Exporters.SaveDwg(_scene, dlg.FileName, versionDlg.Selected);
            var unapplied = DwgAttributeEditor.UnappliedEdits;
            string tail = unapplied.Count > 0
                ? $"\n\n注意：{unapplied.Count} 枚属性改动**未能写进产物**（句柄 "
                  + string.Join(", ", unapplied.Select(h => $"0x{h:X}"))
                  + "）—— 该帧的形制不在本手实证面内。"
                : "";
            MessageBox.Show(this, "已保存：\n" + dlg.FileName + tail, "另存为 DWG",
                            MessageBoxButtons.OK,
                            unapplied.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            DwgAttributeEditor.ClearEdits();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败：\n" + ex.Message, "错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void WarnNoScene()
        => MessageBox.Show(this, "请先打开一张 DWG。", "提示",
                           MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void ShowAbout()
        => MessageBox.Show(this,
            "DwgParser 查看器\n\n"
            + "自研 DWG 解析库的桌面外壳：打开 → 渲染显示 → 点选实体 → 改属性（即时重绘）→ 导出。\n"
            + "解析用 DwgParser.Core，渲染用 DwgParser.Rendering（与 CLI 的 PNG/SVG 同一条链）。\n\n"
            + "属性改动只作用于内存模型，不回写 DWG 字节（写侧是帧级搬运，非模型重编码）。",
            "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
}
