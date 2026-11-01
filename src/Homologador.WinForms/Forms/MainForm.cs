using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using Homologador.Core.DTOs;
using Homologador.Core.Services;
using Homologador.WinForms.ApiClient;
using Homologador.WinForms.Services;
using Homologador.WinForms.Theme;

namespace Homologador.WinForms.Forms;

public partial class MainForm : Form
{
    private readonly HomologadorApiClient? _api;
    private readonly bool _isAdmin;
    private readonly CultureInfo _cultureCl = new("es-CL");

    private readonly ExternalQueryExecutor _queryExecutor = new();
    private readonly TransformationEngine _transformationEngine = new();
    private readonly FileGeneratorService _fileGenerator = new();
    private readonly SqlConnectionTester _connectionTester = new();

    private IReadOnlyList<ReportSummaryDto> _availableReports = [];
    private ReportSummaryDto? _currentReport;
    private ExecutionContextDto? _currentContext;

    // Registros en memoria para la pantalla de ejecución
    private readonly List<PendingRecordItem> _pendingRecords = [];
    private IReadOnlyList<string> _fullTransformColumns = [];

    // Admin caches
    private IReadOnlyList<AdminPipelineDto> _cachedPipelines = [];
    private IReadOnlyList<LookupTableDto> _cachedLookups = [];

    public bool SwitchUserRequested { get; private set; }

    public MainForm()
    {
        InitializeComponent();
        ApplyCustomStyling();
    }

    public MainForm(HomologadorApiClient api) : this()
    {
        _api = api;
        var user = _api.CurrentUser!;
        _isAdmin = string.Equals(user.Rol, "Admin", StringComparison.OrdinalIgnoreCase);

        Text = $"Homologador carga masiva DDC — {user.DisplayName} ({user.Rol})";
        var perfilBadge = string.IsNullOrWhiteSpace(user.Perfil) ? "Administrador" : $"Perfil: {user.Perfil}";
        _lblUserInfo.Text = $"Usuario: {user.DisplayName} ({user.Email})   |   {perfilBadge}";

        if (_isAdmin)
        {
            _btnAdminToggle.Visible = true;
            _pnlAdminNotice.Visible = true;
        }

        Shown += async (_, _) => await InitializeDataAsync();
    }

    private void ApplyCustomStyling()
    {
        try
        {
            var icon = DdcTheme.LoadBrandIcon("ddc-isotipo.ico");
            if (icon != null) Icon = icon;
        }
        catch { }

        try
        {
            var logo = DdcTheme.LoadBrandImage("ddc-logo-white.png");
            if (logo != null) _picHeaderLogo.Image = logo;
        }
        catch { }

        // Top banner paint
        _topBanner.Paint += (_, e) => DdcTheme.DrawHeaderBanner(e.Graphics, _topBanner.ClientRectangle);

        // Hover buttons
        _btnActualizar.MouseEnter += (_, _) => _btnActualizar.BackColor = Color.FromArgb(8, 63, 81);
        _btnActualizar.MouseLeave += (_, _) => _btnActualizar.BackColor = Color.FromArgb(13, 92, 117);

        _btnBrowseOutputDir.MouseEnter += (_, _) => _btnBrowseOutputDir.BackColor = Color.FromArgb(8, 63, 81);
        _btnBrowseOutputDir.MouseLeave += (_, _) => _btnBrowseOutputDir.BackColor = Color.FromArgb(17, 92, 119);

        _btnGenerar.MouseEnter += (_, _) => _btnGenerar.BackColor = Color.FromArgb(199, 89, 20);
        _btnGenerar.MouseLeave += (_, _) => _btnGenerar.BackColor = Color.FromArgb(230, 110, 37);

        _btnBackToMenu.MouseEnter += (_, _) => _btnBackToMenu.BackColor = Color.FromArgb(8, 63, 81);
        _btnBackToMenu.MouseLeave += (_, _) => _btnBackToMenu.BackColor = Color.FromArgb(17, 92, 119);

        _btnAdminToggle.MouseEnter += (_, _) => _btnAdminToggle.BackColor = Color.FromArgb(23, 37, 84);
        _btnAdminToggle.MouseLeave += (_, _) => _btnAdminToggle.BackColor = Color.FromArgb(30, 58, 138);

        _btnSwitchUser.MouseEnter += (_, _) => _btnSwitchUser.BackColor = Color.FromArgb(51, 65, 85);
        _btnSwitchUser.MouseLeave += (_, _) => _btnSwitchUser.BackColor = Color.FromArgb(15, 23, 42);

        // Button clicks
        _btnBackToMenu.Click += OnBtnBackToMenuClick;
        _btnAdminToggle.Click += OnBtnAdminToggleClick;
        _btnSwitchUser.Click += OnBtnSwitchUserClick;
        _btnActualizar.Click += OnBtnActualizarClick;
        _btnGenerar.Click += OnBtnGenerarClick;

        _gridExecution.CellFormatting += OnGridExecutionCellFormatting;

        // Admin events
        _btnBrowseOutputDir.Click += OnBtnBrowseOutputDirClick;
        _btnResetOutputDir.Click += OnBtnResetOutputDirClick;
        _btnOpenOutputDir.Click += OnBtnOpenOutputDirClick;
        _btnResetPwd.Click += async (_, _) => await ResetSelectedUserPasswordAsync();
        _btnTestVpn.Click += async (_, _) => await TestSelectedDataSourceConnectionAsync();
        _gridAdminPipelines.SelectionChanged += (_, _) => UpdateSelectedPipelineRulesGrid();
        _gridAdminLookups.SelectionChanged += (_, _) => UpdateSelectedLookupEntriesGrid();

        // Admin styling
        DdcTheme.StyleGrid(_gridAdminUsers);
        DdcTheme.StyleGrid(_gridAdminReports);
        DdcTheme.StyleGrid(_gridAdminPipelines);
        DdcTheme.StyleGrid(_gridAdminRules);
        DdcTheme.StyleGrid(_gridAdminLookups);
        DdcTheme.StyleGrid(_gridAdminLookupEntries);
        DdcTheme.StyleGrid(_gridAdminDataSources);
        DdcTheme.StyleGrid(_gridAdminQueries);
    }

    private async Task InitializeDataAsync()
    {
        if (_api == null) return;

        try
        {
            _availableReports = await _api.GetMyReportsAsync();
            BuildReportCards();

            if (_isAdmin)
            {
                await LoadAdminGridsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error inicializando datos desde la API:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BuildReportCards()
    {
        _flowReportCards.SuspendLayout();
        _flowReportCards.Controls.Clear();

        if (_availableReports.Count == 0)
        {
            var lblEmpty = new Label
            {
                Text = "No tiene planillas o reportes asignados para su perfil.",
                Font = new Font("Segoe UI", 11F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Margin = new Padding(20)
            };
            _flowReportCards.Controls.Add(lblEmpty);
            _flowReportCards.ResumeLayout();
            return;
        }

        foreach (var r in _availableReports)
        {
            var card = CreateReportCard(r);
            _flowReportCards.Controls.Add(card);
        }

        _flowReportCards.ResumeLayout();
    }

    private Panel CreateReportCard(ReportSummaryDto report)
    {
        var card = new Panel
        {
            Width = 360,
            Height = 185,
            BackColor = Color.White,
            Margin = new Padding(14),
            Cursor = Cursors.Hand
        };

        var accentColor = report.TargetArea.StartsWith("Calidad", StringComparison.OrdinalIgnoreCase)
            ? Color.FromArgb(17, 92, 119) // Dark Teal
            : report.TargetArea.StartsWith("Producc", StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(30, 64, 175) // Royal Blue
                : Color.FromArgb(13, 148, 136); // Teal

        // Franja de acento superior
        var pnlAccent = new Panel
        {
            Dock = DockStyle.Top,
            Height = 5,
            BackColor = accentColor
        };
        card.Controls.Add(pnlAccent);

        var lblArea = new Label
        {
            Text = $"ÁREA: {report.TargetArea.ToUpperInvariant()}",
            Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
            ForeColor = accentColor,
            Location = new Point(16, 16),
            AutoSize = true
        };
        card.Controls.Add(lblArea);

        var lblTitle = new Label
        {
            Text = report.Name,
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(17, 24, 39),
            Location = new Point(14, 38),
            Size = new Size(330, 44),
            AutoEllipsis = true
        };
        card.Controls.Add(lblTitle);

        var lblDetails = new Label
        {
            Text = $"Reglas SAP: {report.RuleCount}  |  Formato: {report.OutputFormat}\nQuery: {report.QueryName}",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(16, 88),
            Size = new Size(330, 36)
        };
        card.Controls.Add(lblDetails);

        var btnOpen = new Button
        {
            Text = "Abrir Reporte →",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = accentColor,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(190, 136),
            Size = new Size(150, 34),
            Cursor = Cursors.Hand
        };
        btnOpen.FlatAppearance.BorderSize = 0;
        card.Controls.Add(btnOpen);

        // Border painting
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1.2f);
            var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
            e.Graphics.DrawRectangle(pen, rect);
        };

        // Click handlers
        EventHandler onClick = async (_, _) => await OpenReportExecutionAsync(report);
        card.Click += onClick;
        lblArea.Click += onClick;
        lblTitle.Click += onClick;
        lblDetails.Click += onClick;
        btnOpen.Click += onClick;

        // Hover effect
        card.MouseEnter += (_, _) => card.BackColor = Color.FromArgb(248, 250, 252);
        card.MouseLeave += (_, _) => card.BackColor = Color.White;

        return card;
    }

    private async Task OpenReportExecutionAsync(ReportSummaryDto report)
    {
        if (_api == null) return;

        _currentReport = report;
        _pendingRecords.Clear();
        _gridExecution.Rows.Clear();
        _btnGenerar.Enabled = false;

        _pnlReportMenu.Visible = false;
        _pnlAdmin.Visible = false;
        _pnlReportExecution.Visible = true;
        _btnBackToMenu.Visible = true;

        _lblExecReportTitle.Text = report.Name;
        var outDir = AppConfigService.GetOutputDirectory();
        _lblExecSubtitle.Text = $"Área: {report.TargetArea}   |   Destino: {outDir}   |   Formato: {report.OutputFormat}";
        _lblExecAdminBadge.Visible = _isAdmin;

        try
        {
            _lblStatusText.Text = "Cargando contexto del reporte y persistencia...";
            Application.DoEvents();

            _currentContext = await _api.GetExecutionContextAsync(report.Id);
            var persistedRows = await _api.GetRecordsByReportAsync(report.Id);

            foreach (var rec in persistedRows)
            {
                var kilosStr = FormatKilos(rec.Kg, rec.Calibre);
                var isProduccion = string.Equals(report.TargetArea, "Producción", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(report.TargetArea, "Produccion", StringComparison.OrdinalIgnoreCase);
                var displayLote = isProduccion && !string.IsNullOrWhiteSpace(rec.CodigoOrLote)
                    ? $"{rec.LoteOrProceso} | {rec.CodigoOrLote}"
                    : rec.LoteOrProceso;

                var rowIdx = _gridExecution.Rows.Add(
                    rec.FechaHoraCreacionArchivo.ToString("dd-MM-yyyy"),
                    displayLote,
                    rec.Productor ?? "-",
                    kilosStr,
                    "Cargado"
                );
                _gridExecution.Rows[rowIdx].Tag = rec;
            }

            _lblStatusText.Text = $"Persistencia cargada: {persistedRows.Count} registro(s) en base de datos.";
            _lblStatusPending.Text = $"Cargados: {persistedRows.Count}";
        }
        catch (Exception ex)
        {
            _lblStatusText.Text = $"Error cargando datos: {ex.Message}";
            MessageBox.Show($"Error al cargar la persistencia del reporte:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnBtnActualizarClick(object? sender, EventArgs e)
    {
        if (_api == null || _currentContext == null || _currentReport == null) return;

        _btnActualizar.Enabled = false;
        _lblStatusText.Text = "Conectando al servidor del packing externo y consultando datos...";
        Application.DoEvents();

        try
        {
            var ctx = _currentContext;

            // 1. Extraer desde el SQL Server del packing
            var sqlParams = new Dictionary<string, (string SqlType, object? Value)>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in ctx.Query.Parameters)
            {
                sqlParams[p.Name] = (p.SqlType, p.DefaultValue);
            }

            List<Dictionary<string, object?>> rawRows;
            try
            {
                var extract = await _queryExecutor.ExecuteExtractAsync(
                    ctx.DataSource,
                    ctx.Query.CommandType,
                    ctx.Query.CommandText,
                    sqlParams);
                rawRows = extract.Rows.ToList();
            }
            catch (Exception sqlEx)
            {
                var ask = MessageBox.Show(
                    $"No se pudo conectar al servidor del packing externo '{ctx.DataSource.Server}' (verifique VPN activa).\n\n" +
                    $"Detalle: {sqlEx.Message}\n\n" +
                    $"¿Desea simular la extracción con 3 lotes de prueba para validar el pipeline ({ctx.Rules.Count} columnas), " +
                    $"visualizarlos como 'Pendiente' y poder generar el Excel?",
                    "Sin Conexión VPN — Simulación Disponible",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (ask != DialogResult.Yes)
                {
                    _lblStatusText.Text = "Operación cancelada (sin conexión VPN).";
                    return;
                }

                rawRows = RowIdentityExtractor.BuildOfflineDemoRows(
                    ctx.Report.TargetArea,
                    ctx.Rules,
                    startIndex: _gridExecution.Rows.Count + 1);
            }

            if (rawRows.Count == 0)
            {
                MessageBox.Show("La consulta en el packing externo no devolvió registros.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _lblStatusText.Text = "La consulta no devolvió registros.";
                return;
            }

            // 2. Transformar filas con TransformationEngine
            _lblStatusText.Text = $"Transformando {rawRows.Count} fila(s) con {ctx.Rules.Count} reglas SAP...";
            Application.DoEvents();

            var fullTransform = _transformationEngine.Transform(ctx.Rules, ctx.Lookups, rawRows);
            _fullTransformColumns = fullTransform.Columns;

            // 3. Obtener claves primarias ya presentes en la grilla y en el contexto de ejecución
            var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ctx.AlreadyGeneratedKeys != null)
            {
                foreach (var k in ctx.AlreadyGeneratedKeys)
                {
                    if (!string.IsNullOrWhiteSpace(k))
                    {
                        existingKeys.Add(k.Trim());
                    }
                }
            }

            foreach (DataGridViewRow row in _gridExecution.Rows)
            {
                if (row.Tag is PendingRecordItem pendingItem)
                {
                    existingKeys.Add(pendingItem.Metadata.PrimaryKey.Trim());
                }
                else if (row.Tag is RecordGridRowDto rec)
                {
                    if (!string.IsNullOrWhiteSpace(rec.CodigoOrLote) && !string.IsNullOrWhiteSpace(rec.LoteOrProceso))
                    {
                        existingKeys.Add($"{rec.LoteOrProceso}|{rec.CodigoOrLote}".Trim());
                    }
                    if (!string.IsNullOrWhiteSpace(rec.CodigoOrLote))
                    {
                        existingKeys.Add(rec.CodigoOrLote.Trim());
                    }
                    if (!string.IsNullOrWhiteSpace(rec.LoteOrProceso))
                    {
                        existingKeys.Add(rec.LoteOrProceso.Trim());
                    }
                }
                else if (row.Cells["_colLote"].Value is string loteVal && !string.IsNullOrWhiteSpace(loteVal))
                {
                    existingKeys.Add(loteVal.Trim());
                }
            }

            var newPendingItems = new List<PendingRecordItem>();
            for (int i = 0; i < fullTransform.Rows.Count; i++)
            {
                var tRow = fullTransform.Rows[i];
                var sRow = i < rawRows.Count ? rawRows[i] : null;
                var meta = RowIdentityExtractor.ExtractRowMetadata(ctx.Report.TargetArea, ctx.Rules, tRow, sRow);

                if (string.IsNullOrEmpty(meta.PrimaryKey) || existingKeys.Contains(meta.PrimaryKey))
                {
                    continue; // Ya existe en la persistencia o en la lista
                }

                existingKeys.Add(meta.PrimaryKey);
                newPendingItems.Add(new PendingRecordItem(
                    Fecha: DateTime.Now.ToString("dd-MM-yyyy"),
                    Lote: meta.PrimaryKey,
                    Productor: meta.Productor ?? "-",
                    Kilos: FormatKilos(meta.Kg, meta.Calibre),
                    TransformedRow: tRow,
                    Metadata: meta
                ));
            }

            if (newPendingItems.Count == 0)
            {
                MessageBox.Show(
                    "No se encontraron registros nuevos.\nTodos los lotes obtenidos ya se encuentran cargados en la base de datos.",
                    "Sin Nuevos Registros",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _lblStatusText.Text = "Todos los lotes obtenidos ya se encuentran cargados.";
                return;
            }

            // 4. Insertar sobre los últimos registros los nuevos con estatus "Pendiente"
            for (int i = newPendingItems.Count - 1; i >= 0; i--)
            {
                var item = newPendingItems[i];
                _pendingRecords.Insert(0, item);

                _gridExecution.Rows.Insert(0,
                    item.Fecha,
                    item.Lote,
                    item.Productor,
                    item.Kilos,
                    "Pendiente"
                );
                _gridExecution.Rows[0].Tag = item;
            }

            _btnGenerar.Enabled = true;
            _lblStatusText.Text = $"Actualización completa: {newPendingItems.Count} nuevo(s) registro(s) pendiente(s) listado(s) arriba de la tabla.";
            _lblStatusPending.Text = $"Pendientes: {_pendingRecords.Count}  |  Cargados: {_gridExecution.Rows.Count - _pendingRecords.Count}";
        }
        catch (Exception ex)
        {
            _lblStatusText.Text = $"Error al actualizar: {ex.Message}";
            MessageBox.Show($"Error ejecutando la actualización:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnActualizar.Enabled = true;
        }
    }

    private async void OnBtnGenerarClick(object? sender, EventArgs e)
    {
        if (_api == null || _currentContext == null || _currentReport == null) return;

        if (_pendingRecords.Count == 0)
        {
            MessageBox.Show("No hay registros pendientes para generar el archivo Excel.", "Sin Pendientes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _btnGenerar.Enabled = false;
        var sw = Stopwatch.StartNew();

        try
        {
            var ctx = _currentContext;
            var report = _currentReport;

            // Extraer las filas transformadas de los pendientes
            var pendingTransformedRows = _pendingRecords.Select(p => p.TransformedRow).ToList();
            var pendingMetadata = _pendingRecords.Select(p => p.Metadata).ToList();

            // Construir archivo Excel
            var generatedFile = _fileGenerator.GenerateFile(
                _fullTransformColumns,
                pendingTransformedRows,
                report.OutputFormat,
                report.OutputSheetName,
                report.OutputFilePattern);

            // Guardar directamente en la ruta configurada (por defecto Descargas)
            var outputDir = AppConfigService.GetOutputDirectory();
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var targetFilePath = Path.Combine(outputDir, generatedFile.FileName);
            _lblStatusText.Text = $"Guardando archivo '{generatedFile.FileName}' en '{outputDir}'...";
            Application.DoEvents();

            await File.WriteAllBytesAsync(targetFilePath, generatedFile.FileBytes);
            sw.Stop();

            if (_isAdmin)
            {
                // MODO ADMINISTRADOR: Ejecución de prueba
                // No se persiste en base de datos ni se cambia ningún estatus
                _lblStatusText.Text = $"Modo prueba: Archivo generado en '{targetFilePath}' (sin alterar persistencia).";
                MessageBox.Show(
                    $"Archivo generado exitosamente en modo prueba:\n{targetFilePath}\n\n" +
                    $"• Filas exportadas: {pendingTransformedRows.Count}\n\n" +
                    "NOTA (Perfil Administrador): Esta ejecución es de prueba. No se modificó ningún estatus ni se guardaron registros en la persistencia.",
                    "Generación Exitosa (Modo Prueba)",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                // MODO OPERADOR: Guarda en persistencia con estatus "Cargado"
                _lblStatusText.Text = $"Registrando {pendingMetadata.Count} lote(s) en la persistencia...";
                Application.DoEvents();

                var createdAt = DateTime.UtcNow;
                var markResp = await _api.MarkGeneratedAsync(new MarkGeneratedRequest(
                    ReportId: report.Id,
                    TargetArea: report.TargetArea,
                    RowsExtracted: pendingMetadata.Count,
                    RowsOutput: pendingMetadata.Count,
                    DurationSeconds: Math.Round((decimal)sw.Elapsed.TotalSeconds, 3),
                    FechaHoraCreacionArchivo: createdAt,
                    GeneratedRows: pendingMetadata
                ));

                // Actualizar estatus en la grilla de "Pendiente" a "Cargado"
                foreach (DataGridViewRow row in _gridExecution.Rows)
                {
                    if (string.Equals(row.Cells["_colEstado"].Value as string, "Pendiente", StringComparison.OrdinalIgnoreCase))
                    {
                        row.Cells["_colEstado"].Value = "Cargado";
                    }
                }

                _pendingRecords.Clear();
                _btnGenerar.Enabled = false;

                _lblStatusText.Text = $"Archivo generado en '{targetFilePath}' y {markResp.InsertedCount} lote(s) guardados en persistencia con estado 'Cargado'.";
                _lblStatusPending.Text = $"Pendientes: 0  |  Cargados: {_gridExecution.Rows.Count}";

                MessageBox.Show(
                    $"Archivo generado exitosamente:\n{targetFilePath}\n\n" +
                    $"• Filas exportadas: {pendingTransformedRows.Count}\n" +
                    $"• Lotes registrados en persistencia: {markResp.InsertedCount}\n" +
                    "• Estado actualizado a: 'Cargado'",
                    "Generación Exitosa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _lblStatusText.Text = $"Error generando archivo: {ex.Message}";
            MessageBox.Show($"Error durante la generación del archivo:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnGenerar.Enabled = true;
        }
    }

    private void OnGridExecutionCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        var colName = _gridExecution.Columns[e.ColumnIndex].Name;

        if (colName == "_colEstado" && e.Value is string estadoVal)
        {
            if (string.Equals(estadoVal, "Pendiente", StringComparison.OrdinalIgnoreCase))
            {
                e.CellStyle.ForeColor = Color.FromArgb(194, 65, 12); // Naranja oscuro / Amber
                e.CellStyle.Font = new Font(_gridExecution.Font, FontStyle.Bold);
            }
            else if (string.Equals(estadoVal, "Cargado", StringComparison.OrdinalIgnoreCase))
            {
                e.CellStyle.ForeColor = Color.FromArgb(17, 92, 119); // Dark Teal
                e.CellStyle.Font = new Font(_gridExecution.Font, FontStyle.Bold);
            }
        }
        else if (colName == "_colKilos")
        {
            e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }

    private void OnBtnBackToMenuClick(object? sender, EventArgs e)
    {
        _pnlReportExecution.Visible = false;
        _pnlAdmin.Visible = false;
        _pnlReportMenu.Visible = true;
        _btnBackToMenu.Visible = false;
        _currentReport = null;
    }

    private void OnBtnAdminToggleClick(object? sender, EventArgs e)
    {
        if (!_isAdmin) return;

        if (_pnlAdmin.Visible)
        {
            _pnlAdmin.Visible = false;
            _pnlReportMenu.Visible = true;
            _btnBackToMenu.Visible = false;
        }
        else
        {
            _pnlReportMenu.Visible = false;
            _pnlReportExecution.Visible = false;
            _pnlAdmin.Visible = true;
            _btnBackToMenu.Visible = true;
        }
    }

    private void OnBtnSwitchUserClick(object? sender, EventArgs e)
    {
        SwitchUserRequested = true;
        Close();
    }

    private string FormatKilos(decimal? kg, string? calibre)
    {
        if (kg.HasValue)
        {
            return kg.Value.ToString("N0", _cultureCl);
        }
        if (!string.IsNullOrWhiteSpace(calibre))
        {
            return calibre;
        }
        return "-";
    }

    private static string NormalizeAreaTableSuffix(string targetArea)
    {
        if (targetArea.StartsWith("Calidad", StringComparison.OrdinalIgnoreCase)) return "Calidad";
        if (targetArea.StartsWith("Producc", StringComparison.OrdinalIgnoreCase)) return "Produccion";
        return "Recepcion";
    }

    // ==========================================
    // MÉTODOS DE ADMINISTRACIÓN (SOLO ADMIN)
    // ==========================================

    private async Task LoadAdminGridsAsync()
    {
        if (_api == null) return;
        try
        {
            var users = await _api.GetAdminUsersAsync();
            var reports = await _api.GetAdminReportsAsync();
            _cachedPipelines = await _api.GetAdminPipelinesAsync();
            _cachedLookups = await _api.GetAdminLookupsAsync();
            var dataSources = await _api.GetAdminDataSourcesAsync();
            var queries = await _api.GetAdminQueriesAsync();

            _gridAdminUsers.DataSource = users.Select(u => new
            {
                u.Id,
                u.Codigo,
                u.Email,
                u.DisplayName,
                u.Rol,
                u.Perfil,
                Activo = u.IsActive ? "Sí" : "No",
                ReportesAsignados = u.AssignedReportsSummary
            }).ToList();

            _gridAdminReports.DataSource = reports.Select(r => new
            {
                r.Id,
                r.Codigo,
                r.Name,
                r.TargetArea,
                r.QueryName,
                r.PipelineName,
                r.OutputFormat
            }).ToList();

            _gridAdminPipelines.DataSource = _cachedPipelines.Select(p => new
            {
                p.Id,
                p.Codigo,
                p.Name,
                Reglas = p.RuleCount,
                p.Description
            }).ToList();

            _gridAdminLookups.DataSource = _cachedLookups.Select(l => new
            {
                l.Id,
                l.Codigo,
                l.Name,
                Entradas = l.Entries.Count,
                l.Description
            }).ToList();

            _gridAdminDataSources.DataSource = dataSources.Select(d => new
            {
                d.Id,
                d.Codigo,
                d.Name,
                d.Server,
                d.Database,
                d.AuthMode,
                d.ConnectionTimeout,
                Activo = d.IsActive ? "Sí" : "No"
            }).ToList();

            _gridAdminQueries.DataSource = queries.Select(q => new
            {
                q.Id,
                q.Codigo,
                q.Name,
                q.DataSourceName,
                TipoComando = q.CommandTypeText,
                Parametros = q.ParameterCount
            }).ToList();

            _txtOutputDir.Text = AppConfigService.GetOutputDirectory();
        }
        catch { }
    }

    private void UpdateSelectedPipelineRulesGrid()
    {
        if (_gridAdminPipelines.CurrentRow?.DataBoundItem == null) return;
        dynamic item = _gridAdminPipelines.CurrentRow.DataBoundItem;
        int pipelineId = item.Id;

        var pipe = _cachedPipelines.FirstOrDefault(p => p.Id == pipelineId);
        if (pipe != null)
        {
            _gridAdminRules.DataSource = pipe.Rules.Select(r => new
            {
                r.SortOrder,
                r.TargetColumn,
                r.RuleType,
                r.SourceColumn,
                r.FixedValue,
                r.DateFormat,
                EsRequerido = r.IsRequired ? "Sí" : "No",
                EsIdentidad = r.IsIdentity ? "Sí" : "No"
            }).ToList();
        }
    }

    private void UpdateSelectedLookupEntriesGrid()
    {
        if (_gridAdminLookups.CurrentRow?.DataBoundItem == null) return;
        dynamic item = _gridAdminLookups.CurrentRow.DataBoundItem;
        int lookupId = item.Id;

        var lk = _cachedLookups.FirstOrDefault(l => l.Id == lookupId);
        if (lk != null)
        {
            _gridAdminLookupEntries.DataSource = lk.Entries.Select(e => new
            {
                e.KeyValue,
                e.MappedValue,
                e.ExtraField1
            }).ToList();
        }
    }

    private async Task ResetSelectedUserPasswordAsync()
    {
        if (_api == null || _gridAdminUsers.CurrentRow?.DataBoundItem == null) return;
        dynamic item = _gridAdminUsers.CurrentRow.DataBoundItem;
        int userId = item.Id;
        string email = item.Email;

        var confirm = MessageBox.Show($"¿Desea restablecer la contraseña de '{email}' a 'admin123.'?", "Restablecer", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        try
        {
            await _api.ResetPasswordAsync(userId, "admin123.");
            MessageBox.Show("Contraseña restablecida exitosamente a 'admin123.'.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error restableciendo contraseña: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task TestSelectedDataSourceConnectionAsync()
    {
        if (_gridAdminDataSources.CurrentRow?.DataBoundItem == null || _api == null) return;
        dynamic item = _gridAdminDataSources.CurrentRow.DataBoundItem;
        int dsId = item.Id;

        try
        {
            var ds = (await _api.GetAdminDataSourcesAsync()).FirstOrDefault(d => d.Id == dsId);
            if (ds == null) return;

            var res = await _connectionTester.TestAsync(ds.Server, ds.Database, 0, null, null, ds.ConnectionTimeout);

            if (res.IsSuccess)
            {
                MessageBox.Show($"Conexión SQL exitosa a '{ds.Server}' ({ds.Database}) en {res.LatencyMs} ms.", "Conexión OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Fallo al conectar a '{ds.Server}':\n{res.ErrorMessage}", "Error Conexión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error probando conexión: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnBtnBrowseOutputDirClick(object? sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Seleccione la carpeta donde se generarán los archivos Excel:",
            UseDescriptionForTitle = true,
            InitialDirectory = AppConfigService.GetOutputDirectory(),
            ShowNewFolderButton = true
        };

        if (fbd.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
        {
            AppConfigService.SetOutputDirectory(fbd.SelectedPath);
            _txtOutputDir.Text = fbd.SelectedPath;
            MessageBox.Show(
                $"Ruta de generación configurada exitosamente:\n{fbd.SelectedPath}\n\nLos archivos generados se guardarán directamente en este directorio.",
                "Ruta Configurada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void OnBtnResetOutputDirClick(object? sender, EventArgs e)
    {
        AppConfigService.ResetToDefault();
        var defaultDir = AppConfigService.GetDefaultOutputDirectory();
        _txtOutputDir.Text = defaultDir;
        MessageBox.Show(
            $"Ruta restablecida al valor por defecto (Descargas):\n{defaultDir}",
            "Ruta Restablecida",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnBtnOpenOutputDirClick(object? sender, EventArgs e)
    {
        var dir = AppConfigService.GetOutputDirectory();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo abrir la carpeta:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private record PendingRecordItem(
        string Fecha,
        string Lote,
        string Productor,
        string Kilos,
        Dictionary<string, object?> TransformedRow,
        GeneratedRowItemDto Metadata
    );
}
