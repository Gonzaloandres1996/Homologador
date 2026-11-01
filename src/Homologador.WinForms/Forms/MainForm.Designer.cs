namespace Homologador.WinForms.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // Top Banner
    private System.Windows.Forms.Panel _topBanner;
    private System.Windows.Forms.PictureBox _picHeaderLogo;
    private System.Windows.Forms.Label _lblBrandTitle;
    private System.Windows.Forms.Label _lblUserInfo;
    private System.Windows.Forms.Button _btnBackToMenu;
    private System.Windows.Forms.Button _btnAdminToggle;
    private System.Windows.Forms.Button _btnSwitchUser;

    // Main Content
    private System.Windows.Forms.Panel _pnlMainContent;

    // View 1: Menú con cajas de Reportes
    private System.Windows.Forms.Panel _pnlReportMenu;
    private System.Windows.Forms.Panel _pnlMenuHeader;
    private System.Windows.Forms.Label _lblMenuTitle;
    private System.Windows.Forms.Label _lblMenuSubtitle;
    private System.Windows.Forms.Panel _pnlAdminNotice;
    private System.Windows.Forms.Label _lblAdminNotice;
    private System.Windows.Forms.FlowLayoutPanel _flowReportCards;

    // View 2: Mesa de Ejecución del Reporte
    private System.Windows.Forms.Panel _pnlReportExecution;
    private System.Windows.Forms.Panel _pnlExecHeader;
    private System.Windows.Forms.Label _lblExecReportTitle;
    private System.Windows.Forms.Label _lblExecSubtitle;
    private System.Windows.Forms.Label _lblExecAdminBadge;
    private System.Windows.Forms.Panel _pnlExecButtons;
    private System.Windows.Forms.Button _btnActualizar;
    private System.Windows.Forms.Button _btnGenerar;
    private System.Windows.Forms.Panel _pnlTableContainer;
    private System.Windows.Forms.DataGridView _gridExecution;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colFecha;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colLote;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colProductor;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colKilos;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colEstado;
    private System.Windows.Forms.StatusStrip _statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel _lblStatusText;
    private System.Windows.Forms.ToolStripStatusLabel _lblStatusPending;

    // View 3: Panel de Administración (solo Admin)
    private System.Windows.Forms.Panel _pnlAdmin;
    private System.Windows.Forms.TabControl _adminSubTabs;
    private System.Windows.Forms.TabPage _tabAdminUsers;
    private System.Windows.Forms.SplitContainer _splitUsers;
    private System.Windows.Forms.GroupBox _grpUsers;
    private System.Windows.Forms.FlowLayoutPanel _pnlUserActions;
    private System.Windows.Forms.Button _btnResetPwd;
    private System.Windows.Forms.DataGridView _gridAdminUsers;
    private System.Windows.Forms.GroupBox _grpReports;
    private System.Windows.Forms.DataGridView _gridAdminReports;

    private System.Windows.Forms.TabPage _tabAdminPipelines;
    private System.Windows.Forms.SplitContainer _splitPipelines;
    private System.Windows.Forms.GroupBox _grpPipelines;
    private System.Windows.Forms.DataGridView _gridAdminPipelines;
    private System.Windows.Forms.GroupBox _grpRules;
    private System.Windows.Forms.DataGridView _gridAdminRules;

    private System.Windows.Forms.TabPage _tabAdminLookups;
    private System.Windows.Forms.SplitContainer _splitLookups;
    private System.Windows.Forms.GroupBox _grpLookups;
    private System.Windows.Forms.DataGridView _gridAdminLookups;
    private System.Windows.Forms.GroupBox _grpLookupEntries;
    private System.Windows.Forms.DataGridView _gridAdminLookupEntries;

    private System.Windows.Forms.TabPage _tabAdminSql;
    private System.Windows.Forms.SplitContainer _splitSql;
    private System.Windows.Forms.GroupBox _grpDataSources;
    private System.Windows.Forms.FlowLayoutPanel _pnlDsActions;
    private System.Windows.Forms.Button _btnTestVpn;
    private System.Windows.Forms.DataGridView _gridAdminDataSources;
    private System.Windows.Forms.GroupBox _grpQueries;
    private System.Windows.Forms.DataGridView _gridAdminQueries;

    private System.Windows.Forms.TabPage _tabAdminConfig;
    private System.Windows.Forms.GroupBox _grpOutputDirConfig;
    private System.Windows.Forms.Label _lblOutputDirDesc;
    private System.Windows.Forms.Label _lblOutputDirPath;
    private System.Windows.Forms.TextBox _txtOutputDir;
    private System.Windows.Forms.Button _btnBrowseOutputDir;
    private System.Windows.Forms.Button _btnResetOutputDir;
    private System.Windows.Forms.Button _btnOpenOutputDir;
    private System.Windows.Forms.Label _lblOutputDirNote;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        System.Windows.Forms.DataGridViewCellStyle dgvHeaderStyle = new System.Windows.Forms.DataGridViewCellStyle();
        System.Windows.Forms.DataGridViewCellStyle dgvDefaultStyle = new System.Windows.Forms.DataGridViewCellStyle();
        System.Windows.Forms.DataGridViewCellStyle dgvAlternatingStyle = new System.Windows.Forms.DataGridViewCellStyle();

        _topBanner = new Panel();
        _picHeaderLogo = new PictureBox();
        _lblBrandTitle = new Label();
        _lblUserInfo = new Label();
        _btnBackToMenu = new Button();
        _btnAdminToggle = new Button();
        _btnSwitchUser = new Button();

        _pnlMainContent = new Panel();

        // Screen 2
        _pnlReportMenu = new Panel();
        _pnlMenuHeader = new Panel();
        _lblMenuTitle = new Label();
        _lblMenuSubtitle = new Label();
        _pnlAdminNotice = new Panel();
        _lblAdminNotice = new Label();
        _flowReportCards = new FlowLayoutPanel();

        // Screen 3
        _pnlReportExecution = new Panel();
        _pnlExecHeader = new Panel();
        _lblExecReportTitle = new Label();
        _lblExecSubtitle = new Label();
        _lblExecAdminBadge = new Label();
        _pnlExecButtons = new Panel();
        _btnActualizar = new Button();
        _btnGenerar = new Button();
        _pnlTableContainer = new Panel();
        _gridExecution = new DataGridView();
        _colFecha = new DataGridViewTextBoxColumn();
        _colLote = new DataGridViewTextBoxColumn();
        _colProductor = new DataGridViewTextBoxColumn();
        _colKilos = new DataGridViewTextBoxColumn();
        _colEstado = new DataGridViewTextBoxColumn();
        _statusStrip = new StatusStrip();
        _lblStatusText = new ToolStripStatusLabel();
        _lblStatusPending = new ToolStripStatusLabel();

        // Admin Panel
        _pnlAdmin = new Panel();
        _adminSubTabs = new TabControl();
        _tabAdminUsers = new TabPage();
        _splitUsers = new SplitContainer();
        _grpUsers = new GroupBox();
        _gridAdminUsers = new DataGridView();
        _pnlUserActions = new FlowLayoutPanel();
        _btnResetPwd = new Button();
        _grpReports = new GroupBox();
        _gridAdminReports = new DataGridView();
        _tabAdminPipelines = new TabPage();
        _splitPipelines = new SplitContainer();
        _grpPipelines = new GroupBox();
        _gridAdminPipelines = new DataGridView();
        _grpRules = new GroupBox();
        _gridAdminRules = new DataGridView();
        _tabAdminLookups = new TabPage();
        _splitLookups = new SplitContainer();
        _grpLookups = new GroupBox();
        _gridAdminLookups = new DataGridView();
        _grpLookupEntries = new GroupBox();
        _gridAdminLookupEntries = new DataGridView();
        _tabAdminSql = new TabPage();
        _splitSql = new SplitContainer();
        _grpDataSources = new GroupBox();
        _gridAdminDataSources = new DataGridView();
        _pnlDsActions = new FlowLayoutPanel();
        _btnTestVpn = new Button();
        _grpQueries = new GroupBox();
        _gridAdminQueries = new DataGridView();
        _tabAdminConfig = new TabPage();
        _grpOutputDirConfig = new GroupBox();
        _lblOutputDirDesc = new Label();
        _lblOutputDirPath = new Label();
        _txtOutputDir = new TextBox();
        _btnBrowseOutputDir = new Button();
        _btnResetOutputDir = new Button();
        _btnOpenOutputDir = new Button();
        _lblOutputDirNote = new Label();

        _topBanner.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_picHeaderLogo).BeginInit();
        _pnlMainContent.SuspendLayout();
        _pnlReportMenu.SuspendLayout();
        _pnlMenuHeader.SuspendLayout();
        _pnlAdminNotice.SuspendLayout();
        _pnlReportExecution.SuspendLayout();
        _pnlExecHeader.SuspendLayout();
        _pnlExecButtons.SuspendLayout();
        _pnlTableContainer.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_gridExecution).BeginInit();
        _statusStrip.SuspendLayout();

        _pnlAdmin.SuspendLayout();
        _adminSubTabs.SuspendLayout();
        _tabAdminUsers.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_splitUsers).BeginInit();
        _splitUsers.Panel1.SuspendLayout();
        _splitUsers.Panel2.SuspendLayout();
        _splitUsers.SuspendLayout();
        _grpUsers.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_gridAdminUsers).BeginInit();
        _pnlUserActions.SuspendLayout();
        _grpReports.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_gridAdminReports).BeginInit();

        _tabAdminPipelines.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_splitPipelines).BeginInit();
        _splitPipelines.Panel1.SuspendLayout();
        _splitPipelines.Panel2.SuspendLayout();
        _splitPipelines.SuspendLayout();
        _grpPipelines.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_gridAdminPipelines).BeginInit();
        _grpRules.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_gridAdminRules).BeginInit();

        _tabAdminLookups.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_splitLookups).BeginInit();
        _splitLookups.Panel1.SuspendLayout();
        _splitLookups.Panel2.SuspendLayout();
        _splitLookups.SuspendLayout();
        _grpLookups.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_gridAdminLookups).BeginInit();
        _grpLookupEntries.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_gridAdminLookupEntries).BeginInit();

        _tabAdminSql.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_splitSql).BeginInit();
        _splitSql.Panel1.SuspendLayout();
        _splitSql.Panel2.SuspendLayout();
        _splitSql.SuspendLayout();
        _grpDataSources.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_gridAdminDataSources).BeginInit();
        _pnlDsActions.SuspendLayout();
        _grpQueries.SuspendLayout();
        _tabAdminConfig.SuspendLayout();
        _grpOutputDirConfig.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_gridAdminQueries).BeginInit();

        SuspendLayout();

        // 
        // _topBanner
        // 
        _topBanner.BackColor = System.Drawing.Color.FromArgb(10, 24, 60);
        _topBanner.Controls.Add(_picHeaderLogo);
        _topBanner.Controls.Add(_lblBrandTitle);
        _topBanner.Controls.Add(_lblUserInfo);
        _topBanner.Controls.Add(_btnBackToMenu);
        _topBanner.Controls.Add(_btnAdminToggle);
        _topBanner.Controls.Add(_btnSwitchUser);
        _topBanner.Dock = System.Windows.Forms.DockStyle.Top;
        _topBanner.Location = new System.Drawing.Point(0, 0);
        _topBanner.Name = "_topBanner";
        _topBanner.Size = new System.Drawing.Size(1260, 54);
        _topBanner.TabIndex = 0;
        // 
        // _picHeaderLogo
        // 
        _picHeaderLogo.Location = new System.Drawing.Point(12, 10);
        _picHeaderLogo.Name = "_picHeaderLogo";
        _picHeaderLogo.Size = new System.Drawing.Size(34, 34);
        _picHeaderLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        _picHeaderLogo.TabIndex = 0;
        _picHeaderLogo.TabStop = false;
        // 
        // _lblBrandTitle
        // 
        _lblBrandTitle.AutoSize = true;
        _lblBrandTitle.Font = new System.Drawing.Font("Segoe UI", 11.5F, System.Drawing.FontStyle.Bold);
        _lblBrandTitle.ForeColor = System.Drawing.Color.White;
        _lblBrandTitle.Location = new System.Drawing.Point(52, 16);
        _lblBrandTitle.Name = "_lblBrandTitle";
        _lblBrandTitle.Size = new System.Drawing.Size(262, 21);
        _lblBrandTitle.TabIndex = 1;
        _lblBrandTitle.Text = "Homologador carga masiva DDC";
        // 
        // _lblUserInfo
        // 
        _lblUserInfo.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        _lblUserInfo.AutoSize = true;
        _lblUserInfo.Font = new System.Drawing.Font("Segoe UI", 9.25F);
        _lblUserInfo.ForeColor = System.Drawing.Color.FromArgb(203, 213, 225);
        _lblUserInfo.Location = new System.Drawing.Point(340, 19);
        _lblUserInfo.Name = "_lblUserInfo";
        _lblUserInfo.Size = new System.Drawing.Size(155, 17);
        _lblUserInfo.TabIndex = 2;
        _lblUserInfo.Text = "Usuario: Cargando...";
        // 
        // _btnBackToMenu
        // 
        _btnBackToMenu.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        _btnBackToMenu.BackColor = System.Drawing.Color.FromArgb(17, 92, 119);
        _btnBackToMenu.FlatAppearance.BorderSize = 0;
        _btnBackToMenu.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnBackToMenu.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        _btnBackToMenu.ForeColor = System.Drawing.Color.White;
        _btnBackToMenu.Location = new System.Drawing.Point(860, 11);
        _btnBackToMenu.Name = "_btnBackToMenu";
        _btnBackToMenu.Size = new System.Drawing.Size(140, 32);
        _btnBackToMenu.TabIndex = 3;
        _btnBackToMenu.Text = "← Volver a Reportes";
        _btnBackToMenu.UseVisualStyleBackColor = false;
        _btnBackToMenu.Visible = false;
        // 
        // _btnAdminToggle
        // 
        _btnAdminToggle.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        _btnAdminToggle.BackColor = System.Drawing.Color.FromArgb(30, 58, 138);
        _btnAdminToggle.FlatAppearance.BorderSize = 0;
        _btnAdminToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnAdminToggle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        _btnAdminToggle.ForeColor = System.Drawing.Color.White;
        _btnAdminToggle.Location = new System.Drawing.Point(1008, 11);
        _btnAdminToggle.Name = "_btnAdminToggle";
        _btnAdminToggle.Size = new System.Drawing.Size(124, 32);
        _btnAdminToggle.TabIndex = 4;
        _btnAdminToggle.Text = "⚙ Administración";
        _btnAdminToggle.UseVisualStyleBackColor = false;
        _btnAdminToggle.Visible = false;
        // 
        // _btnSwitchUser
        // 
        _btnSwitchUser.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        _btnSwitchUser.BackColor = System.Drawing.Color.FromArgb(15, 23, 42);
        _btnSwitchUser.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(71, 85, 105);
        _btnSwitchUser.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnSwitchUser.Font = new System.Drawing.Font("Segoe UI", 9F);
        _btnSwitchUser.ForeColor = System.Drawing.Color.FromArgb(226, 232, 240);
        _btnSwitchUser.Location = new System.Drawing.Point(1140, 11);
        _btnSwitchUser.Name = "_btnSwitchUser";
        _btnSwitchUser.Size = new System.Drawing.Size(108, 32);
        _btnSwitchUser.TabIndex = 5;
        _btnSwitchUser.Text = "Cerrar sesión";
        _btnSwitchUser.UseVisualStyleBackColor = false;
        // 
        // _pnlMainContent
        // 
        _pnlMainContent.BackColor = System.Drawing.Color.FromArgb(243, 244, 246);
        _pnlMainContent.Controls.Add(_pnlReportMenu);
        _pnlMainContent.Controls.Add(_pnlReportExecution);
        _pnlMainContent.Controls.Add(_pnlAdmin);
        _pnlMainContent.Dock = System.Windows.Forms.DockStyle.Fill;
        _pnlMainContent.Location = new System.Drawing.Point(0, 54);
        _pnlMainContent.Name = "_pnlMainContent";
        _pnlMainContent.Size = new System.Drawing.Size(1260, 686);
        _pnlMainContent.TabIndex = 1;

        // 
        // ==========================================
        // View 1: Menú con cajas para seleccionar el Reporte
        // ==========================================
        // 
        _pnlReportMenu.Controls.Add(_flowReportCards);
        _pnlReportMenu.Controls.Add(_pnlMenuHeader);
        _pnlReportMenu.Dock = System.Windows.Forms.DockStyle.Fill;
        _pnlReportMenu.Location = new System.Drawing.Point(0, 0);
        _pnlReportMenu.Name = "_pnlReportMenu";
        _pnlReportMenu.Size = new System.Drawing.Size(1260, 686);
        _pnlReportMenu.TabIndex = 0;
        // 
        // _pnlMenuHeader
        // 
        _pnlMenuHeader.BackColor = System.Drawing.Color.White;
        _pnlMenuHeader.Controls.Add(_lblMenuTitle);
        _pnlMenuHeader.Controls.Add(_lblMenuSubtitle);
        _pnlMenuHeader.Controls.Add(_pnlAdminNotice);
        _pnlMenuHeader.Dock = System.Windows.Forms.DockStyle.Top;
        _pnlMenuHeader.Location = new System.Drawing.Point(0, 0);
        _pnlMenuHeader.Name = "_pnlMenuHeader";
        _pnlMenuHeader.Padding = new System.Windows.Forms.Padding(24, 16, 24, 12);
        _pnlMenuHeader.Size = new System.Drawing.Size(1260, 95);
        _pnlMenuHeader.TabIndex = 0;
        // 
        // _lblMenuTitle
        // 
        _lblMenuTitle.AutoSize = true;
        _lblMenuTitle.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
        _lblMenuTitle.ForeColor = System.Drawing.Color.FromArgb(17, 24, 39);
        _lblMenuTitle.Location = new System.Drawing.Point(22, 14);
        _lblMenuTitle.Name = "_lblMenuTitle";
        _lblMenuTitle.Size = new System.Drawing.Size(325, 30);
        _lblMenuTitle.TabIndex = 0;
        _lblMenuTitle.Text = "Planillas y Reportes de Carga";
        // 
        // _lblMenuSubtitle
        // 
        _lblMenuSubtitle.AutoSize = true;
        _lblMenuSubtitle.Font = new System.Drawing.Font("Segoe UI", 9.5F);
        _lblMenuSubtitle.ForeColor = System.Drawing.Color.FromArgb(75, 85, 99);
        _lblMenuSubtitle.Location = new System.Drawing.Point(24, 48);
        _lblMenuSubtitle.Name = "_lblMenuSubtitle";
        _lblMenuSubtitle.Size = new System.Drawing.Size(534, 17);
        _lblMenuSubtitle.TabIndex = 1;
        _lblMenuSubtitle.Text = "Seleccione una planilla para ingresar a la mesa de trabajo de extracción y persistencia.";
        // 
        // _pnlAdminNotice
        // 
        _pnlAdminNotice.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        _pnlAdminNotice.BackColor = System.Drawing.Color.FromArgb(254, 243, 199);
        _pnlAdminNotice.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _pnlAdminNotice.Controls.Add(_lblAdminNotice);
        _pnlAdminNotice.Location = new System.Drawing.Point(620, 14);
        _pnlAdminNotice.Name = "_pnlAdminNotice";
        _pnlAdminNotice.Padding = new System.Windows.Forms.Padding(10, 6, 10, 6);
        _pnlAdminNotice.Size = new System.Drawing.Size(616, 68);
        _pnlAdminNotice.TabIndex = 2;
        _pnlAdminNotice.Visible = false;
        // 
        // _lblAdminNotice
        // 
        _lblAdminNotice.Dock = System.Windows.Forms.DockStyle.Fill;
        _lblAdminNotice.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Regular);
        _lblAdminNotice.ForeColor = System.Drawing.Color.FromArgb(146, 64, 14);
        _lblAdminNotice.Location = new System.Drawing.Point(10, 6);
        _lblAdminNotice.Name = "_lblAdminNotice";
        _lblAdminNotice.Size = new System.Drawing.Size(594, 54);
        _lblAdminNotice.TabIndex = 0;
        _lblAdminNotice.Text = "MODO ADMINISTRADOR (Ejecución de Prueba):\r\nTiene habilitadas todas las planillas. Al ejecutar y generar el archivo Excel, su perfil NO cambiará ningún estatus ni guardará registros en la persistencia (prueba de ejecución exclusivamente).";
        // 
        // _flowReportCards
        // 
        _flowReportCards.AutoScroll = true;
        _flowReportCards.BackColor = System.Drawing.Color.FromArgb(243, 244, 246);
        _flowReportCards.Dock = System.Windows.Forms.DockStyle.Fill;
        _flowReportCards.Location = new System.Drawing.Point(0, 95);
        _flowReportCards.Name = "_flowReportCards";
        _flowReportCards.Padding = new System.Windows.Forms.Padding(24);
        _flowReportCards.Size = new System.Drawing.Size(1260, 591);
        _flowReportCards.TabIndex = 1;

        // 
        // ==========================================
        // View 2: Menú de Ejecución del Reporte (Persistencia + Botones Actualizar / Generar a la derecha)
        // ==========================================
        // 
        _pnlReportExecution.Controls.Add(_pnlTableContainer);
        _pnlReportExecution.Controls.Add(_pnlExecHeader);
        _pnlReportExecution.Controls.Add(_statusStrip);
        _pnlReportExecution.Dock = System.Windows.Forms.DockStyle.Fill;
        _pnlReportExecution.Location = new System.Drawing.Point(0, 0);
        _pnlReportExecution.Name = "_pnlReportExecution";
        _pnlReportExecution.Size = new System.Drawing.Size(1260, 686);
        _pnlReportExecution.TabIndex = 1;
        _pnlReportExecution.Visible = false;
        // 
        // _pnlExecHeader
        // 
        _pnlExecHeader.BackColor = System.Drawing.Color.White;
        _pnlExecHeader.Controls.Add(_lblExecReportTitle);
        _pnlExecHeader.Controls.Add(_lblExecSubtitle);
        _pnlExecHeader.Controls.Add(_lblExecAdminBadge);
        _pnlExecHeader.Controls.Add(_pnlExecButtons);
        _pnlExecHeader.Dock = System.Windows.Forms.DockStyle.Top;
        _pnlExecHeader.Location = new System.Drawing.Point(0, 0);
        _pnlExecHeader.Name = "_pnlExecHeader";
        _pnlExecHeader.Padding = new System.Windows.Forms.Padding(20, 10, 20, 10);
        _pnlExecHeader.Size = new System.Drawing.Size(1260, 68);
        _pnlExecHeader.TabIndex = 0;
        // 
        // _lblExecReportTitle
        // 
        _lblExecReportTitle.AutoSize = true;
        _lblExecReportTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
        _lblExecReportTitle.ForeColor = System.Drawing.Color.FromArgb(17, 24, 39);
        _lblExecReportTitle.Location = new System.Drawing.Point(18, 10);
        _lblExecReportTitle.Name = "_lblExecReportTitle";
        _lblExecReportTitle.Size = new System.Drawing.Size(176, 25);
        _lblExecReportTitle.TabIndex = 0;
        _lblExecReportTitle.Text = "Reporte Ejecución";
        // 
        // _lblExecSubtitle
        // 
        _lblExecSubtitle.AutoSize = true;
        _lblExecSubtitle.Font = new System.Drawing.Font("Segoe UI", 9F);
        _lblExecSubtitle.ForeColor = System.Drawing.Color.FromArgb(75, 85, 99);
        _lblExecSubtitle.Location = new System.Drawing.Point(20, 39);
        _lblExecSubtitle.Name = "_lblExecSubtitle";
        _lblExecSubtitle.Size = new System.Drawing.Size(262, 15);
        _lblExecSubtitle.TabIndex = 1;
        _lblExecSubtitle.Text = "Persistencia de lotes cargados para este reporte.";
        // 
        // _lblExecAdminBadge
        // 
        _lblExecAdminBadge.AutoSize = true;
        _lblExecAdminBadge.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        _lblExecAdminBadge.ForeColor = System.Drawing.Color.FromArgb(194, 65, 12);
        _lblExecAdminBadge.Location = new System.Drawing.Point(380, 16);
        _lblExecAdminBadge.Name = "_lblExecAdminBadge";
        _lblExecAdminBadge.Size = new System.Drawing.Size(260, 15);
        _lblExecAdminBadge.TabIndex = 2;
        _lblExecAdminBadge.Text = "• MODO PRUEBA (Sin persistencia de cambios)";
        _lblExecAdminBadge.Visible = false;
        // 
        // _pnlExecButtons
        // 
        _pnlExecButtons.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        _pnlExecButtons.Controls.Add(_btnActualizar);
        _pnlExecButtons.Controls.Add(_btnGenerar);
        _pnlExecButtons.Location = new System.Drawing.Point(1020, 14);
        _pnlExecButtons.Name = "_pnlExecButtons";
        _pnlExecButtons.Size = new System.Drawing.Size(224, 40);
        _pnlExecButtons.TabIndex = 3;
        // 
        // _btnActualizar
        // 
        _btnActualizar.BackColor = System.Drawing.Color.FromArgb(13, 92, 117);
        _btnActualizar.Cursor = System.Windows.Forms.Cursors.Hand;
        _btnActualizar.FlatAppearance.BorderSize = 0;
        _btnActualizar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnActualizar.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
        _btnActualizar.ForeColor = System.Drawing.Color.White;
        _btnActualizar.Location = new System.Drawing.Point(4, 2);
        _btnActualizar.Name = "_btnActualizar";
        _btnActualizar.Size = new System.Drawing.Size(102, 34);
        _btnActualizar.TabIndex = 0;
        _btnActualizar.Text = "Actualizar";
        _btnActualizar.UseVisualStyleBackColor = false;
        // 
        // _btnGenerar
        // 
        _btnGenerar.BackColor = System.Drawing.Color.FromArgb(230, 110, 37);
        _btnGenerar.Cursor = System.Windows.Forms.Cursors.Hand;
        _btnGenerar.FlatAppearance.BorderSize = 0;
        _btnGenerar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnGenerar.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
        _btnGenerar.ForeColor = System.Drawing.Color.White;
        _btnGenerar.Location = new System.Drawing.Point(114, 2);
        _btnGenerar.Name = "_btnGenerar";
        _btnGenerar.Size = new System.Drawing.Size(102, 34);
        _btnGenerar.TabIndex = 1;
        _btnGenerar.Text = "Generar";
        _btnGenerar.UseVisualStyleBackColor = false;
        // 
        // _pnlTableContainer
        // 
        _pnlTableContainer.BackColor = System.Drawing.Color.FromArgb(243, 244, 246);
        _pnlTableContainer.Controls.Add(_gridExecution);
        _pnlTableContainer.Dock = System.Windows.Forms.DockStyle.Fill;
        _pnlTableContainer.Location = new System.Drawing.Point(0, 68);
        _pnlTableContainer.Name = "_pnlTableContainer";
        _pnlTableContainer.Padding = new System.Windows.Forms.Padding(20, 12, 20, 12);
        _pnlTableContainer.Size = new System.Drawing.Size(1260, 594);
        _pnlTableContainer.TabIndex = 1;
        // 
        // _gridExecution
        // 
        _gridExecution.AllowUserToAddRows = false;
        _gridExecution.AllowUserToDeleteRows = false;
        _gridExecution.AllowUserToResizeRows = false;
        _gridExecution.BackgroundColor = System.Drawing.Color.FromArgb(223, 227, 232);
        _gridExecution.BorderStyle = System.Windows.Forms.BorderStyle.None;
        _gridExecution.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.Single;
        _gridExecution.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;

        dgvHeaderStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
        dgvHeaderStyle.BackColor = System.Drawing.Color.FromArgb(17, 92, 119);
        dgvHeaderStyle.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
        dgvHeaderStyle.ForeColor = System.Drawing.Color.White;
        dgvHeaderStyle.Padding = new System.Windows.Forms.Padding(6, 4, 6, 4);
        dgvHeaderStyle.SelectionBackColor = System.Drawing.Color.FromArgb(17, 92, 119);
        dgvHeaderStyle.SelectionForeColor = System.Drawing.Color.White;
        dgvHeaderStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
        _gridExecution.ColumnHeadersDefaultCellStyle = dgvHeaderStyle;
        _gridExecution.ColumnHeadersHeight = 36;
        _gridExecution.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        _gridExecution.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            _colFecha,
            _colLote,
            _colProductor,
            _colKilos,
            _colEstado
        });

        dgvDefaultStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
        dgvDefaultStyle.BackColor = System.Drawing.Color.FromArgb(203, 210, 217);
        dgvDefaultStyle.Font = new System.Drawing.Font("Segoe UI", 9.5F);
        dgvDefaultStyle.ForeColor = System.Drawing.Color.FromArgb(20, 24, 35);
        dgvDefaultStyle.Padding = new System.Windows.Forms.Padding(6, 3, 6, 3);
        dgvDefaultStyle.SelectionBackColor = System.Drawing.Color.FromArgb(186, 215, 233);
        dgvDefaultStyle.SelectionForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
        _gridExecution.DefaultCellStyle = dgvDefaultStyle;

        dgvAlternatingStyle.BackColor = System.Drawing.Color.FromArgb(223, 227, 232);
        dgvAlternatingStyle.ForeColor = System.Drawing.Color.FromArgb(20, 24, 35);
        dgvAlternatingStyle.SelectionBackColor = System.Drawing.Color.FromArgb(186, 215, 233);
        dgvAlternatingStyle.SelectionForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
        _gridExecution.AlternatingRowsDefaultCellStyle = dgvAlternatingStyle;

        _gridExecution.Dock = System.Windows.Forms.DockStyle.Fill;
        _gridExecution.EnableHeadersVisualStyles = false;
        _gridExecution.GridColor = System.Drawing.Color.White;
        _gridExecution.Location = new System.Drawing.Point(20, 12);
        _gridExecution.MultiSelect = false;
        _gridExecution.Name = "_gridExecution";
        _gridExecution.ReadOnly = true;
        _gridExecution.RowHeadersVisible = false;
        _gridExecution.RowTemplate.Height = 32;
        _gridExecution.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        _gridExecution.Size = new System.Drawing.Size(1220, 570);
        _gridExecution.TabIndex = 0;
        // 
        // _colFecha
        // 
        _colFecha.HeaderText = "Fecha";
        _colFecha.Name = "_colFecha";
        _colFecha.ReadOnly = true;
        _colFecha.Width = 130;
        // 
        // _colLote
        // 
        _colLote.HeaderText = "Lote";
        _colLote.Name = "_colLote";
        _colLote.ReadOnly = true;
        _colLote.Width = 140;
        // 
        // _colProductor
        // 
        _colProductor.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
        _colProductor.HeaderText = "Productor";
        _colProductor.Name = "_colProductor";
        _colProductor.ReadOnly = true;
        // 
        // _colKilos
        // 
        _colKilos.HeaderText = "Kilos";
        _colKilos.Name = "_colKilos";
        _colKilos.ReadOnly = true;
        _colKilos.Width = 120;
        // 
        // _colEstado
        // 
        _colEstado.HeaderText = "Estado";
        _colEstado.Name = "_colEstado";
        _colEstado.ReadOnly = true;
        _colEstado.Width = 120;
        // 
        // _statusStrip
        // 
        _statusStrip.BackColor = System.Drawing.Color.White;
        _statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            _lblStatusText,
            _lblStatusPending
        });
        _statusStrip.Location = new System.Drawing.Point(0, 662);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Size = new System.Drawing.Size(1260, 24);
        _statusStrip.TabIndex = 2;
        // 
        // _lblStatusText
        // 
        _lblStatusText.Font = new System.Drawing.Font("Segoe UI", 9F);
        _lblStatusText.ForeColor = System.Drawing.Color.FromArgb(75, 85, 99);
        _lblStatusText.Name = "_lblStatusText";
        _lblStatusText.Size = new System.Drawing.Size(1205, 19);
        _lblStatusText.Spring = true;
        _lblStatusText.Text = "Listo.";
        _lblStatusText.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // 
        // _lblStatusPending
        // 
        _lblStatusPending.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        _lblStatusPending.ForeColor = System.Drawing.Color.FromArgb(17, 92, 119);
        _lblStatusPending.Name = "_lblStatusPending";
        _lblStatusPending.Size = new System.Drawing.Size(40, 19);
        _lblStatusPending.Text = "Cargados: 0";
        // 
        // ==========================================
        // View 3: Panel de Administración (solo Admin)
        // ==========================================
        // 
        _pnlAdmin.Controls.Add(_adminSubTabs);
        _pnlAdmin.Dock = System.Windows.Forms.DockStyle.Fill;
        _pnlAdmin.Location = new System.Drawing.Point(0, 0);
        _pnlAdmin.Name = "_pnlAdmin";
        _pnlAdmin.Padding = new System.Windows.Forms.Padding(12);
        _pnlAdmin.Size = new System.Drawing.Size(1260, 686);
        _pnlAdmin.TabIndex = 2;
        _pnlAdmin.Visible = false;
        // 
        // _adminSubTabs
        // 
        _adminSubTabs.Controls.Add(_tabAdminUsers);
        _adminSubTabs.Controls.Add(_tabAdminPipelines);
        _adminSubTabs.Controls.Add(_tabAdminLookups);
        _adminSubTabs.Controls.Add(_tabAdminSql);
        _adminSubTabs.Controls.Add(_tabAdminConfig);
        _adminSubTabs.Dock = System.Windows.Forms.DockStyle.Fill;
        _adminSubTabs.Location = new System.Drawing.Point(12, 12);
        _adminSubTabs.Name = "_adminSubTabs";
        _adminSubTabs.SelectedIndex = 0;
        _adminSubTabs.Size = new System.Drawing.Size(1236, 662);
        _adminSubTabs.TabIndex = 0;
        // 
        // _tabAdminUsers
        // 
        _tabAdminUsers.Controls.Add(_splitUsers);
        _tabAdminUsers.Location = new System.Drawing.Point(4, 24);
        _tabAdminUsers.Name = "_tabAdminUsers";
        _tabAdminUsers.Padding = new System.Windows.Forms.Padding(6);
        _tabAdminUsers.Size = new System.Drawing.Size(1228, 634);
        _tabAdminUsers.TabIndex = 0;
        _tabAdminUsers.Text = "Usuarios y Reportes Asignados";
        _tabAdminUsers.UseVisualStyleBackColor = true;
        // 
        // _splitUsers
        // 
        _splitUsers.Dock = System.Windows.Forms.DockStyle.Fill;
        _splitUsers.Location = new System.Drawing.Point(6, 6);
        _splitUsers.Name = "_splitUsers";
        _splitUsers.Orientation = System.Windows.Forms.Orientation.Horizontal;
        // 
        // _splitUsers.Panel1
        // 
        _splitUsers.Panel1.Controls.Add(_grpUsers);
        // 
        // _splitUsers.Panel2
        // 
        _splitUsers.Panel2.Controls.Add(_grpReports);
        _splitUsers.Size = new System.Drawing.Size(1216, 622);
        _splitUsers.SplitterDistance = 330;
        _splitUsers.TabIndex = 0;
        // 
        // _grpUsers
        // 
        _grpUsers.Controls.Add(_gridAdminUsers);
        _grpUsers.Controls.Add(_pnlUserActions);
        _grpUsers.Dock = System.Windows.Forms.DockStyle.Fill;
        _grpUsers.Location = new System.Drawing.Point(0, 0);
        _grpUsers.Name = "_grpUsers";
        _grpUsers.Padding = new System.Windows.Forms.Padding(6);
        _grpUsers.Size = new System.Drawing.Size(1216, 330);
        _grpUsers.TabIndex = 0;
        _grpUsers.TabStop = false;
        _grpUsers.Text = "Usuarios del Sistema (dbo.Users)";
        // 
        // _gridAdminUsers
        // 
        _gridAdminUsers.BackgroundColor = System.Drawing.Color.White;
        _gridAdminUsers.Dock = System.Windows.Forms.DockStyle.Fill;
        _gridAdminUsers.Location = new System.Drawing.Point(6, 22);
        _gridAdminUsers.Name = "_gridAdminUsers";
        _gridAdminUsers.Size = new System.Drawing.Size(1204, 264);
        _gridAdminUsers.TabIndex = 0;
        // 
        // _pnlUserActions
        // 
        _pnlUserActions.Controls.Add(_btnResetPwd);
        _pnlUserActions.Dock = System.Windows.Forms.DockStyle.Bottom;
        _pnlUserActions.Location = new System.Drawing.Point(6, 286);
        _pnlUserActions.Name = "_pnlUserActions";
        _pnlUserActions.Padding = new System.Windows.Forms.Padding(4);
        _pnlUserActions.Size = new System.Drawing.Size(1204, 38);
        _pnlUserActions.TabIndex = 1;
        // 
        // _btnResetPwd
        // 
        _btnResetPwd.Location = new System.Drawing.Point(7, 7);
        _btnResetPwd.Name = "_btnResetPwd";
        _btnResetPwd.Size = new System.Drawing.Size(220, 26);
        _btnResetPwd.TabIndex = 0;
        _btnResetPwd.Text = "Restablecer Contraseña Usuario";
        _btnResetPwd.UseVisualStyleBackColor = true;
        // 
        // _grpReports
        // 
        _grpReports.Controls.Add(_gridAdminReports);
        _grpReports.Dock = System.Windows.Forms.DockStyle.Fill;
        _grpReports.Location = new System.Drawing.Point(0, 0);
        _grpReports.Name = "_grpReports";
        _grpReports.Padding = new System.Windows.Forms.Padding(6);
        _grpReports.Size = new System.Drawing.Size(1216, 288);
        _grpReports.TabIndex = 0;
        _grpReports.TabStop = false;
        _grpReports.Text = "Reportes y Permisos (dbo.Reports & dbo.UserReports)";
        // 
        // _gridAdminReports
        // 
        _gridAdminReports.BackgroundColor = System.Drawing.Color.White;
        _gridAdminReports.Dock = System.Windows.Forms.DockStyle.Fill;
        _gridAdminReports.Location = new System.Drawing.Point(6, 22);
        _gridAdminReports.Name = "_gridAdminReports";
        _gridAdminReports.Size = new System.Drawing.Size(1204, 260);
        _gridAdminReports.TabIndex = 0;
        // 
        // _tabAdminPipelines
        // 
        _tabAdminPipelines.Controls.Add(_splitPipelines);
        _tabAdminPipelines.Location = new System.Drawing.Point(4, 24);
        _tabAdminPipelines.Name = "_tabAdminPipelines";
        _tabAdminPipelines.Padding = new System.Windows.Forms.Padding(6);
        _tabAdminPipelines.Size = new System.Drawing.Size(1228, 634);
        _tabAdminPipelines.TabIndex = 1;
        _tabAdminPipelines.Text = "Pipelines y Reglas";
        _tabAdminPipelines.UseVisualStyleBackColor = true;
        // 
        // _splitPipelines
        // 
        _splitPipelines.Dock = System.Windows.Forms.DockStyle.Fill;
        _splitPipelines.Location = new System.Drawing.Point(6, 6);
        _splitPipelines.Name = "_splitPipelines";
        _splitPipelines.Orientation = System.Windows.Forms.Orientation.Horizontal;
        _splitPipelines.Panel1.Controls.Add(_grpPipelines);
        _splitPipelines.Panel2.Controls.Add(_grpRules);
        _splitPipelines.Size = new System.Drawing.Size(1216, 622);
        _splitPipelines.SplitterDistance = 240;
        _splitPipelines.TabIndex = 0;
        // 
        // _grpPipelines
        // 
        _grpPipelines.Controls.Add(_gridAdminPipelines);
        _grpPipelines.Dock = System.Windows.Forms.DockStyle.Fill;
        _grpPipelines.Location = new System.Drawing.Point(0, 0);
        _grpPipelines.Name = "_grpPipelines";
        _grpPipelines.Padding = new System.Windows.Forms.Padding(6);
        _grpPipelines.Size = new System.Drawing.Size(1216, 240);
        _grpPipelines.TabIndex = 0;
        _grpPipelines.TabStop = false;
        _grpPipelines.Text = "Pipelines (dbo.MappingPipelines)";
        // 
        // _gridAdminPipelines
        // 
        _gridAdminPipelines.BackgroundColor = System.Drawing.Color.White;
        _gridAdminPipelines.Dock = System.Windows.Forms.DockStyle.Fill;
        _gridAdminPipelines.Location = new System.Drawing.Point(6, 22);
        _gridAdminPipelines.Name = "_gridAdminPipelines";
        _gridAdminPipelines.Size = new System.Drawing.Size(1204, 212);
        _gridAdminPipelines.TabIndex = 0;
        // 
        // _grpRules
        // 
        _grpRules.Controls.Add(_gridAdminRules);
        _grpRules.Dock = System.Windows.Forms.DockStyle.Fill;
        _grpRules.Location = new System.Drawing.Point(0, 0);
        _grpRules.Name = "_grpRules";
        _grpRules.Padding = new System.Windows.Forms.Padding(6);
        _grpRules.Size = new System.Drawing.Size(1216, 378);
        _grpRules.TabIndex = 0;
        _grpRules.TabStop = false;
        _grpRules.Text = "Reglas del Pipeline Seleccionado (dbo.MappingRules)";
        // 
        // _gridAdminRules
        // 
        _gridAdminRules.BackgroundColor = System.Drawing.Color.White;
        _gridAdminRules.Dock = System.Windows.Forms.DockStyle.Fill;
        _gridAdminRules.Location = new System.Drawing.Point(6, 22);
        _gridAdminRules.Name = "_gridAdminRules";
        _gridAdminRules.Size = new System.Drawing.Size(1204, 350);
        _gridAdminRules.TabIndex = 0;
        // 
        // _tabAdminLookups
        // 
        _tabAdminLookups.Controls.Add(_splitLookups);
        _tabAdminLookups.Location = new System.Drawing.Point(4, 24);
        _tabAdminLookups.Name = "_tabAdminLookups";
        _tabAdminLookups.Padding = new System.Windows.Forms.Padding(6);
        _tabAdminLookups.Size = new System.Drawing.Size(1228, 634);
        _tabAdminLookups.TabIndex = 2;
        _tabAdminLookups.Text = "Tablas Lookup";
        _tabAdminLookups.UseVisualStyleBackColor = true;
        // 
        // _splitLookups
        // 
        _splitLookups.Dock = System.Windows.Forms.DockStyle.Fill;
        _splitLookups.Location = new System.Drawing.Point(6, 6);
        _splitLookups.Name = "_splitLookups";
        _splitLookups.Orientation = System.Windows.Forms.Orientation.Horizontal;
        _splitLookups.Panel1.Controls.Add(_grpLookups);
        _splitLookups.Panel2.Controls.Add(_grpLookupEntries);
        _splitLookups.Size = new System.Drawing.Size(1216, 622);
        _splitLookups.SplitterDistance = 240;
        _splitLookups.TabIndex = 0;
        // 
        // _grpLookups
        // 
        _grpLookups.Controls.Add(_gridAdminLookups);
        _grpLookups.Dock = System.Windows.Forms.DockStyle.Fill;
        _grpLookups.Location = new System.Drawing.Point(0, 0);
        _grpLookups.Name = "_grpLookups";
        _grpLookups.Padding = new System.Windows.Forms.Padding(6);
        _grpLookups.Size = new System.Drawing.Size(1216, 240);
        _grpLookups.TabIndex = 0;
        _grpLookups.TabStop = false;
        _grpLookups.Text = "Catálogo de Lookups (dbo.LookupTables)";
        // 
        // _gridAdminLookups
        // 
        _gridAdminLookups.BackgroundColor = System.Drawing.Color.White;
        _gridAdminLookups.Dock = System.Windows.Forms.DockStyle.Fill;
        _gridAdminLookups.Location = new System.Drawing.Point(6, 22);
        _gridAdminLookups.Name = "_gridAdminLookups";
        _gridAdminLookups.Size = new System.Drawing.Size(1204, 212);
        _gridAdminLookups.TabIndex = 0;
        // 
        // _grpLookupEntries
        // 
        _grpLookupEntries.Controls.Add(_gridAdminLookupEntries);
        _grpLookupEntries.Dock = System.Windows.Forms.DockStyle.Fill;
        _grpLookupEntries.Location = new System.Drawing.Point(0, 0);
        _grpLookupEntries.Name = "_grpLookupEntries";
        _grpLookupEntries.Padding = new System.Windows.Forms.Padding(6);
        _grpLookupEntries.Size = new System.Drawing.Size(1216, 378);
        _grpLookupEntries.TabIndex = 0;
        _grpLookupEntries.TabStop = false;
        _grpLookupEntries.Text = "Equivalencias del Lookup Seleccionado (dbo.LookupEntries)";
        // 
        // _gridAdminLookupEntries
        // 
        _gridAdminLookupEntries.BackgroundColor = System.Drawing.Color.White;
        _gridAdminLookupEntries.Dock = System.Windows.Forms.DockStyle.Fill;
        _gridAdminLookupEntries.Location = new System.Drawing.Point(6, 22);
        _gridAdminLookupEntries.Name = "_gridAdminLookupEntries";
        _gridAdminLookupEntries.Size = new System.Drawing.Size(1204, 350);
        _gridAdminLookupEntries.TabIndex = 0;
        // 
        // _tabAdminSql
        // 
        _tabAdminSql.Controls.Add(_splitSql);
        _tabAdminSql.Location = new System.Drawing.Point(4, 24);
        _tabAdminSql.Name = "_tabAdminSql";
        _tabAdminSql.Padding = new System.Windows.Forms.Padding(6);
        _tabAdminSql.Size = new System.Drawing.Size(1228, 634);
        _tabAdminSql.TabIndex = 3;
        _tabAdminSql.Text = "Orígenes de Datos y Consultas SQL";
        _tabAdminSql.UseVisualStyleBackColor = true;
        // 
        // _splitSql
        // 
        _splitSql.Dock = System.Windows.Forms.DockStyle.Fill;
        _splitSql.Location = new System.Drawing.Point(6, 6);
        _splitSql.Name = "_splitSql";
        _splitSql.Orientation = System.Windows.Forms.Orientation.Horizontal;
        _splitSql.Panel1.Controls.Add(_grpDataSources);
        _splitSql.Panel2.Controls.Add(_grpQueries);
        _splitSql.Size = new System.Drawing.Size(1216, 622);
        _splitSql.SplitterDistance = 240;
        _splitSql.TabIndex = 0;
        // 
        // _grpDataSources
        // 
        _grpDataSources.Controls.Add(_gridAdminDataSources);
        _grpDataSources.Controls.Add(_pnlDsActions);
        _grpDataSources.Dock = System.Windows.Forms.DockStyle.Fill;
        _grpDataSources.Location = new System.Drawing.Point(0, 0);
        _grpDataSources.Name = "_grpDataSources";
        _grpDataSources.Padding = new System.Windows.Forms.Padding(6);
        _grpDataSources.Size = new System.Drawing.Size(1216, 240);
        _grpDataSources.TabIndex = 0;
        _grpDataSources.TabStop = false;
        _grpDataSources.Text = "Servidores de Packings Externos (dbo.DataSources)";
        // 
        // _gridAdminDataSources
        // 
        _gridAdminDataSources.BackgroundColor = System.Drawing.Color.White;
        _gridAdminDataSources.Dock = System.Windows.Forms.DockStyle.Fill;
        _gridAdminDataSources.Location = new System.Drawing.Point(6, 22);
        _gridAdminDataSources.Name = "_gridAdminDataSources";
        _gridAdminDataSources.Size = new System.Drawing.Size(1204, 174);
        _gridAdminDataSources.TabIndex = 0;
        // 
        // _pnlDsActions
        // 
        _pnlDsActions.Controls.Add(_btnTestVpn);
        _pnlDsActions.Dock = System.Windows.Forms.DockStyle.Bottom;
        _pnlDsActions.Location = new System.Drawing.Point(6, 196);
        _pnlDsActions.Name = "_pnlDsActions";
        _pnlDsActions.Padding = new System.Windows.Forms.Padding(4);
        _pnlDsActions.Size = new System.Drawing.Size(1204, 38);
        _pnlDsActions.TabIndex = 1;
        // 
        // _btnTestVpn
        // 
        _btnTestVpn.Location = new System.Drawing.Point(7, 7);
        _btnTestVpn.Name = "_btnTestVpn";
        _btnTestVpn.Size = new System.Drawing.Size(220, 26);
        _btnTestVpn.TabIndex = 0;
        _btnTestVpn.Text = "Probar Conectividad VPN / SQL";
        _btnTestVpn.UseVisualStyleBackColor = true;
        // 
        // _grpQueries
        // 
        _grpQueries.Controls.Add(_gridAdminQueries);
        _grpQueries.Dock = System.Windows.Forms.DockStyle.Fill;
        _grpQueries.Location = new System.Drawing.Point(0, 0);
        _grpQueries.Name = "_grpQueries";
        _grpQueries.Padding = new System.Windows.Forms.Padding(6);
        _grpQueries.Size = new System.Drawing.Size(1216, 378);
        _grpQueries.TabIndex = 0;
        _grpQueries.TabStop = false;
        _grpQueries.Text = "Consultas SQL de Extracción (dbo.QueryDefinitions)";
        // 
        // _gridAdminQueries
        // 
        _gridAdminQueries.BackgroundColor = System.Drawing.Color.White;
        _gridAdminQueries.Dock = System.Windows.Forms.DockStyle.Fill;
        _gridAdminQueries.Location = new System.Drawing.Point(6, 22);
        _gridAdminQueries.Name = "_gridAdminQueries";
        _gridAdminQueries.Size = new System.Drawing.Size(1204, 350);
        _gridAdminQueries.TabIndex = 0;

        // 
        // _tabAdminConfig
        // 
        _tabAdminConfig.Controls.Add(_grpOutputDirConfig);
        _tabAdminConfig.Location = new System.Drawing.Point(4, 24);
        _tabAdminConfig.Name = "_tabAdminConfig";
        _tabAdminConfig.Padding = new System.Windows.Forms.Padding(16);
        _tabAdminConfig.Size = new System.Drawing.Size(1228, 634);
        _tabAdminConfig.TabIndex = 4;
        _tabAdminConfig.Text = "Configuración General y Rutas";
        _tabAdminConfig.UseVisualStyleBackColor = true;
        // 
        // _grpOutputDirConfig
        // 
        _grpOutputDirConfig.Controls.Add(_lblOutputDirNote);
        _grpOutputDirConfig.Controls.Add(_btnOpenOutputDir);
        _grpOutputDirConfig.Controls.Add(_btnResetOutputDir);
        _grpOutputDirConfig.Controls.Add(_btnBrowseOutputDir);
        _grpOutputDirConfig.Controls.Add(_txtOutputDir);
        _grpOutputDirConfig.Controls.Add(_lblOutputDirPath);
        _grpOutputDirConfig.Controls.Add(_lblOutputDirDesc);
        _grpOutputDirConfig.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
        _grpOutputDirConfig.ForeColor = System.Drawing.Color.FromArgb(17, 24, 39);
        _grpOutputDirConfig.Location = new System.Drawing.Point(16, 16);
        _grpOutputDirConfig.Name = "_grpOutputDirConfig";
        _grpOutputDirConfig.Padding = new System.Windows.Forms.Padding(20);
        _grpOutputDirConfig.Size = new System.Drawing.Size(920, 260);
        _grpOutputDirConfig.TabIndex = 0;
        _grpOutputDirConfig.TabStop = false;
        _grpOutputDirConfig.Text = "Ruta General de Generación de Archivos";
        // 
        // _lblOutputDirDesc
        // 
        _lblOutputDirDesc.Font = new System.Drawing.Font("Segoe UI", 9.5F);
        _lblOutputDirDesc.ForeColor = System.Drawing.Color.FromArgb(75, 85, 99);
        _lblOutputDirDesc.Location = new System.Drawing.Point(20, 32);
        _lblOutputDirDesc.Name = "_lblOutputDirDesc";
        _lblOutputDirDesc.Size = new System.Drawing.Size(870, 42);
        _lblOutputDirDesc.TabIndex = 0;
        _lblOutputDirDesc.Text = "Al presionar el botón 'Generar' en cualquier planilla, los archivos Excel se guardarán directamente en esta carpeta sin solicitar ubicación ni abrir diálogos repetitivos.\r\nLa nomenclatura de cada archivo se respeta según la configuración de cada informe.";
        // 
        // _lblOutputDirPath
        // 
        _lblOutputDirPath.AutoSize = true;
        _lblOutputDirPath.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
        _lblOutputDirPath.ForeColor = System.Drawing.Color.FromArgb(30, 41, 59);
        _lblOutputDirPath.Location = new System.Drawing.Point(20, 84);
        _lblOutputDirPath.Name = "_lblOutputDirPath";
        _lblOutputDirPath.Size = new System.Drawing.Size(185, 17);
        _lblOutputDirPath.TabIndex = 1;
        _lblOutputDirPath.Text = "Carpeta configurada de destino:";
        // 
        // _txtOutputDir
        // 
        _txtOutputDir.BackColor = System.Drawing.Color.White;
        _txtOutputDir.Font = new System.Drawing.Font("Segoe UI", 10F);
        _txtOutputDir.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
        _txtOutputDir.Location = new System.Drawing.Point(23, 108);
        _txtOutputDir.Name = "_txtOutputDir";
        _txtOutputDir.ReadOnly = true;
        _txtOutputDir.Size = new System.Drawing.Size(660, 25);
        _txtOutputDir.TabIndex = 2;
        // 
        // _btnBrowseOutputDir
        // 
        _btnBrowseOutputDir.BackColor = System.Drawing.Color.FromArgb(17, 92, 119);
        _btnBrowseOutputDir.Cursor = System.Windows.Forms.Cursors.Hand;
        _btnBrowseOutputDir.FlatAppearance.BorderSize = 0;
        _btnBrowseOutputDir.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnBrowseOutputDir.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        _btnBrowseOutputDir.ForeColor = System.Drawing.Color.White;
        _btnBrowseOutputDir.Location = new System.Drawing.Point(695, 105);
        _btnBrowseOutputDir.Name = "_btnBrowseOutputDir";
        _btnBrowseOutputDir.Size = new System.Drawing.Size(180, 30);
        _btnBrowseOutputDir.TabIndex = 3;
        _btnBrowseOutputDir.Text = "Seleccionar carpeta...";
        _btnBrowseOutputDir.UseVisualStyleBackColor = false;
        // 
        // _btnResetOutputDir
        // 
        _btnResetOutputDir.BackColor = System.Drawing.Color.FromArgb(241, 245, 249);
        _btnResetOutputDir.Cursor = System.Windows.Forms.Cursors.Hand;
        _btnResetOutputDir.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
        _btnResetOutputDir.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnResetOutputDir.Font = new System.Drawing.Font("Segoe UI", 8.5F);
        _btnResetOutputDir.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
        _btnResetOutputDir.Location = new System.Drawing.Point(23, 150);
        _btnResetOutputDir.Name = "_btnResetOutputDir";
        _btnResetOutputDir.Size = new System.Drawing.Size(225, 28);
        _btnResetOutputDir.TabIndex = 4;
        _btnResetOutputDir.Text = "Restablecer a Descargas (Defecto)";
        _btnResetOutputDir.UseVisualStyleBackColor = false;
        // 
        // _btnOpenOutputDir
        // 
        _btnOpenOutputDir.BackColor = System.Drawing.Color.FromArgb(241, 245, 249);
        _btnOpenOutputDir.Cursor = System.Windows.Forms.Cursors.Hand;
        _btnOpenOutputDir.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
        _btnOpenOutputDir.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnOpenOutputDir.Font = new System.Drawing.Font("Segoe UI", 8.5F);
        _btnOpenOutputDir.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
        _btnOpenOutputDir.Location = new System.Drawing.Point(260, 150);
        _btnOpenOutputDir.Name = "_btnOpenOutputDir";
        _btnOpenOutputDir.Size = new System.Drawing.Size(195, 28);
        _btnOpenOutputDir.TabIndex = 5;
        _btnOpenOutputDir.Text = "Abrir carpeta en Explorador";
        _btnOpenOutputDir.UseVisualStyleBackColor = false;
        // 
        // _lblOutputDirNote
        // 
        _lblOutputDirNote.AutoSize = true;
        _lblOutputDirNote.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Italic);
        _lblOutputDirNote.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
        _lblOutputDirNote.Location = new System.Drawing.Point(20, 200);
        _lblOutputDirNote.Name = "_lblOutputDirNote";
        _lblOutputDirNote.Size = new System.Drawing.Size(434, 15);
        _lblOutputDirNote.TabIndex = 6;
        _lblOutputDirNote.Text = "* La carpeta por defecto es 'Descargas' del usuario actual en esta estación de trabajo.";

        // 
        // MainForm
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(1260, 740);
        Controls.Add(_pnlMainContent);
        Controls.Add(_topBanner);
        Font = new System.Drawing.Font("Segoe UI", 9F);
        MinimumSize = new System.Drawing.Size(1000, 600);
        Name = "MainForm";
        StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        Text = "Homologador carga masiva DDC";

        _topBanner.ResumeLayout(false);
        _topBanner.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_picHeaderLogo).EndInit();
        _pnlMainContent.ResumeLayout(false);
        _pnlReportMenu.ResumeLayout(false);
        _pnlMenuHeader.ResumeLayout(false);
        _pnlMenuHeader.PerformLayout();
        _pnlAdminNotice.ResumeLayout(false);
        _pnlReportExecution.ResumeLayout(false);
        _pnlReportExecution.PerformLayout();
        _pnlExecHeader.ResumeLayout(false);
        _pnlExecHeader.PerformLayout();
        _pnlExecButtons.ResumeLayout(false);
        _pnlTableContainer.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_gridExecution).EndInit();
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();

        _pnlAdmin.ResumeLayout(false);
        _adminSubTabs.ResumeLayout(false);
        _tabAdminUsers.ResumeLayout(false);
        _splitUsers.Panel1.ResumeLayout(false);
        _splitUsers.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_splitUsers).EndInit();
        _splitUsers.ResumeLayout(false);
        _grpUsers.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_gridAdminUsers).EndInit();
        _pnlUserActions.ResumeLayout(false);
        _grpReports.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_gridAdminReports).EndInit();

        _tabAdminPipelines.ResumeLayout(false);
        _splitPipelines.Panel1.ResumeLayout(false);
        _splitPipelines.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_splitPipelines).EndInit();
        _splitPipelines.ResumeLayout(false);
        _grpPipelines.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_gridAdminPipelines).EndInit();
        _grpRules.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_gridAdminRules).EndInit();

        _tabAdminLookups.ResumeLayout(false);
        _splitLookups.Panel1.ResumeLayout(false);
        _splitLookups.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_splitLookups).EndInit();
        _splitLookups.ResumeLayout(false);
        _grpLookups.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_gridAdminLookups).EndInit();
        _grpLookupEntries.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_gridAdminLookupEntries).EndInit();

        _tabAdminSql.ResumeLayout(false);
        _splitSql.Panel1.ResumeLayout(false);
        _splitSql.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_splitSql).EndInit();
        _splitSql.ResumeLayout(false);
        _grpDataSources.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_gridAdminDataSources).EndInit();
        _pnlDsActions.ResumeLayout(false);
        _grpQueries.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_gridAdminQueries).EndInit();

        _tabAdminConfig.ResumeLayout(false);
        _grpOutputDirConfig.ResumeLayout(false);
        _grpOutputDirConfig.PerformLayout();

        ResumeLayout(false);
    }

    #endregion
}