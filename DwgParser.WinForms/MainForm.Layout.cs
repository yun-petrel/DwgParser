namespace DwgParser.WinForms;

internal sealed partial class MainForm
{
    private MenuStrip _menu = null!;
    private ToolStrip _toolbar = null!;
    private StatusStrip _status = null!;
    private ToolStripMenuItem _miDark = null!;
    private ToolStripButton _btnDark = null!;
    private ToolStripComboBox _cbThickness = null!;
    private SplitContainer _splitOuter = null!;
    private SplitContainer _splitInner = null!;
    private Panel _propPanel = null!;
    private Panel _layerBar = null!;
    private Button _btnAllLayers = null!;
    private FlowLayoutPanel _layerFlow = null!;
    private readonly ToolTip _layerTip = new();

    private void BuildUi()
    {
        SuspendLayout();

        Text = "DwgParser 查看器";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1360, 860);
        MinimumSize = new Size(900, 600);
        AllowDrop = true;
        KeyPreview = true;

        _canvas.Dock = DockStyle.Fill;

        _entityList.Dock = DockStyle.Fill;
        _entityList.View = View.Details;
        _entityList.FullRowSelect = true;
        _entityList.MultiSelect = false;
        _entityList.HideSelection = false;
        _entityList.Columns.Add("句柄", 90);
        _entityList.Columns.Add("类型", 110);
        _entityList.Columns.Add("图层", 160);
        _entityList.Columns.Add("色", 40);

        _propHeader.Dock = DockStyle.Top;
        _propHeader.Height = 30;
        _propHeader.TextAlign = ContentAlignment.MiddleLeft;
        _propHeader.Padding = new Padding(8, 0, 0, 0);
        _propHeader.Text = "未选中实体";

        _propGrid.Dock = DockStyle.Fill;
        _propGrid.AllowUserToAddRows = false;
        _propGrid.AllowUserToDeleteRows = false;
        _propGrid.AllowUserToResizeRows = false;
        _propGrid.RowHeadersVisible = false;
        _propGrid.EditMode = DataGridViewEditMode.EditOnEnter;
        _propGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _propGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _propGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "name",
            HeaderText = "属性",
            ReadOnly = true,
            FillWeight = 40
        });
        _propGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "value",
            HeaderText = "值",
            FillWeight = 60
        });

        _propPanel = new Panel { Dock = DockStyle.Fill };
        _propPanel.Controls.Add(_propGrid);
        _propPanel.Controls.Add(_propHeader);

        BuildLayerBar();

        _splitInner = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel2
        };
        _splitInner.Panel1.Controls.Add(_canvas);
        _splitInner.Panel1.Controls.Add(_layerBar);
        _splitInner.Panel2.Controls.Add(_propPanel);

        _splitOuter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1
        };
        _splitOuter.Panel1.Controls.Add(_entityList);
        _splitOuter.Panel2.Controls.Add(_splitInner);

        BuildMenu();
        BuildToolbar();
        BuildStatus();

        Controls.Add(_splitOuter);
        Controls.Add(_status);
        Controls.Add(_toolbar);
        Controls.Add(_menu);
        MainMenuStrip = _menu;

        ResumeLayout(true);
    }

    private void BuildMenu()
    {
        _menu = new MenuStrip();

        var file = new ToolStripMenuItem("文件(&F)");
        file.DropDownItems.Add(Item("打开图纸…", Keys.Control | Keys.O, (_, _) => PromptOpen()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("导出 PNG…", Keys.None, (_, _) => PromptExport("png")));
        file.DropDownItems.Add(Item("导出 SVG…", Keys.None, (_, _) => PromptExport("svg")));
        file.DropDownItems.Add(Item("导出 JSON…", Keys.None, (_, _) => PromptExport("json")));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("另存为 DWG…（属性块文本/标记会落盘，其余改动仅本会话）", Keys.None,
            (_, _) => PromptSaveAsDwg()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("退出", Keys.Alt | Keys.F4, (_, _) => Close()));

        var view = new ToolStripMenuItem("视图(&V)");
        view.DropDownItems.Add(Item("适应窗口", Keys.Control | Keys.D0, (_, _) => _canvas.FitToDrawing()));
        view.DropDownItems.Add(Item("放大", Keys.Control | Keys.Oemplus, (_, _) => _canvas.ZoomBy(1.25)));
        view.DropDownItems.Add(Item("缩小", Keys.Control | Keys.OemMinus, (_, _) => _canvas.ZoomBy(1 / 1.25)));
        view.DropDownItems.Add(Item("缩放到选中实体", Keys.Control | Keys.D1, (_, _) => _canvas.ZoomToSelection()));
        view.DropDownItems.Add(new ToolStripSeparator());
        _miDark = new ToolStripMenuItem("深底（黑底白线）") { CheckOnClick = true };
        _miDark.CheckedChanged += (_, _) => SetDark(_miDark.Checked);
        view.DropDownItems.Add(_miDark);

        var help = new ToolStripMenuItem("帮助(&H)");
        help.DropDownItems.Add(Item("关于", Keys.None, (_, _) => ShowAbout()));

        _menu.Items.Add(file);
        _menu.Items.Add(view);
        _menu.Items.Add(help);
    }

    private void BuildToolbar()
    {
        _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };

        _toolbar.Items.Add(ToolButton("打开", (_, _) => PromptOpen()));
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(ToolButton("适应窗口", (_, _) => _canvas.FitToDrawing()));
        _toolbar.Items.Add(ToolButton("缩小", (_, _) => _canvas.ZoomBy(1 / 1.25)));
        _toolbar.Items.Add(ToolButton("放大", (_, _) => _canvas.ZoomBy(1.25)));
        _toolbar.Items.Add(ToolButton("缩放到选中", (_, _) => _canvas.ZoomToSelection()));
        _toolbar.Items.Add(new ToolStripSeparator());

        _btnDark = new ToolStripButton("深底") { CheckOnClick = true, DisplayStyle = ToolStripItemDisplayStyle.Text };
        _btnDark.CheckedChanged += (_, _) => SetDark(_btnDark.Checked);
        _toolbar.Items.Add(_btnDark);

        _toolbar.Items.Add(new ToolStripLabel("线宽"));
        _cbThickness = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 56 };
        _cbThickness.Items.AddRange(new object[] { "1", "2", "3", "4" });
        _cbThickness.SelectedIndex = 0;
        _cbThickness.SelectedIndexChanged += (_, _) =>
        {
            if (_cbThickness.SelectedIndex < 0) return;
            _canvas.Surface.LineThickness = _cbThickness.SelectedIndex + 1;
            _canvas.InvalidateRender();
        };
        _toolbar.Items.Add(_cbThickness);
    }

    private void BuildStatus()
    {
        _status = new StatusStrip();
        _statusFile.Spring = true;
        _statusFile.TextAlign = ContentAlignment.MiddleLeft;
        _statusCursor.AutoSize = true;
        _statusView.AutoSize = true;
        _status.Items.Add(_statusFile);
        _status.Items.Add(_statusCursor);
        _status.Items.Add(_statusView);
    }

    private static ToolStripMenuItem Item(string text, Keys shortcut, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text);
        if (shortcut != Keys.None) item.ShortcutKeys = shortcut;
        item.Click += onClick;
        return item;
    }

    private static ToolStripButton ToolButton(string text, EventHandler onClick)
    {
        var b = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        b.Click += onClick;
        return b;
    }
}
