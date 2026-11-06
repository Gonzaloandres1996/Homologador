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
    private bool _grmPersistenceKnown;
    private bool _hasSimulatedPending;
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
        var perfilBadge = string.IsNullOrWhiteSpace(user.Perfil) ? "Sin perfil Conversor" : $"Perfiles: {user.Perfil}";
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
        _currentContext = null;
        _fullTransformColumns = [];
        _pendingRecords.Clear();
        _grmPersistenceKnown = false;
        _hasSimulatedPending = false;
        _gridExecution.Rows.Clear();
        _btnGenerar.Enabled = false;
        _btnActualizar.Enabled = false;

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
            var tipoProceso = TipoProceso(report.TargetArea);
            var registros = await _api.GetPersistenciasAsync(tipoProceso);
            _grmPersistenceKnown = true;
            foreach (var rec in registros.Where(r => r.ReportId == report.Id))
            {
                var displayLote = tipoProceso == 3 && !string.IsNullOrWhiteSpace(rec.CodigoPallet)
                    ? $"{rec.Lote} | {rec.CodigoPallet}"
                    : rec.Lote;
                var rowIdx = _gridExecution.Rows.Add(
                    rec.FechaHoraCreacionArchivo?.ToString("dd-MM-yyyy") ?? "-",
                    displayLote, rec.Productor ?? "-",
                    FormatKilos(tipoProceso == 1 ? rec.Kg : null,
                        tipoProceso == 3 ? rec.Calibre : null), "Cargado");
                _gridExecution.Rows[rowIdx].Tag = rec;
            }
            _lblStatusText.Text = $"Persistencia GRM cargada: {_gridExecution.Rows.Count} registro(s) del reporte.";
            _lblStatusPending.Text = $"Cargados: {_gridExecution.Rows.Count}";
            _btnActualizar.Enabled = true;
        }
        catch (Exception ex)
        {
            _lblStatusText.Text = $"Error cargando datos: {ex.Message}";
            MessageBox.Show($"Error al cargar el contexto o la persistencia del reporte:\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            var usedSimulation = false;
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
                    $"¿Desea simular 3 lotes para validar el pipeline ({ctx.Rules.Count} columnas)? Se mostrarán como 'Pendiente', sin habilitar Generar ni registrar en GRM PROD.",
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
                usedSimulation = true;
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

            var tipoProceso = TipoProceso(ctx.Report.TargetArea);
            _grmPersistenceKnown = false;
            var persistedRecords = await _api.GetPersistenciasAsync(tipoProceso);
            _grmPersistenceKnown = true;
            var persistedKeys = GrmKeys(persistedRecords, tipoProceso, _currentReport.Id);
            ConfirmGrmRows(persistedKeys);

            // 3. Compare against GRM and against pending rows already in this session.
            var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            existingKeys.UnionWith(persistedKeys);

            foreach (DataGridViewRow row in _gridExecution.Rows)
            {
                if (row.Tag is PendingRecordItem pendingItem)
                {
                    existingKeys.Add(pendingItem.Metadata.PrimaryKey.Trim());
                }
                else if (row.Tag is GrmRecepcionRecord grmRecord)
                {
                    var key = GrmKey(grmRecord, tipoProceso, _currentReport.Id);
                    if (key is not null) existingKeys.Add(key);
                }
            }

            var newPendingItems = new List<PendingRecordItem>();
            for (int i = 0; i < fullTransform.Rows.Count; i++)
            {
                var tRow = fullTransform.Rows[i];
                var sRow = i < rawRows.Count ? rawRows[i] : null;
                var meta = RowIdentityExtractor.ExtractRowMetadata(ctx.Report.TargetArea, ctx.Rules, tRow, sRow);
                if (tipoProceso == 3 && !TrySplitProductionKey(meta.PrimaryKey, out _, out _))
                    throw new InvalidOperationException(
                        $"La fila de Producción {i + 1} no contiene LoteProceso y CodigoPallet válidos.");

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
                _btnGenerar.Enabled = _pendingRecords.Count > 0 && _grmPersistenceKnown
                    && (!_hasSimulatedPending || _isAdmin);
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

            if (usedSimulation)
                _hasSimulatedPending = true;

            _btnGenerar.Enabled = _grmPersistenceKnown && (!_hasSimulatedPending || _isAdmin);
            _lblStatusText.Text = _hasSimulatedPending && !_isAdmin
                ? $"Simulación: {newPendingItems.Count} pendiente(s). Generar está bloqueado para proteger GRM PROD."
                : $"Actualización completa: {newPendingItems.Count} nuevo(s) registro(s) pendiente(s) listado(s) arriba de la tabla.";
            _lblStatusPending.Text = $"Pendientes: {_pendingRecords.Count}  |  Cargados: {_gridExecution.Rows.Count - _pendingRecords.Count}";
        }
        catch (Exception ex)
        {
            _lblStatusText.Text = $"Error al actualizar: {ex.Message}";
            MessageBox.Show($"Error ejecutando la actualización:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnGenerar.Enabled = false;
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

        if (!_isAdmin && (!_grmPersistenceKnown || _hasSimulatedPending))
        {
            var reason = _hasSimulatedPending
                ? "Los pendientes incluyen datos simulados y no se pueden registrar en GRM PROD."
                : "No se pudo confirmar la persistencia GRM. Actualice el reporte antes de generar.";
            MessageBox.Show(reason, "Generación bloqueada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            var tipoProceso = TipoProceso(report.TargetArea);
            if (tipoProceso == 3 && pendingMetadata.Any(row =>
                    !TrySplitProductionKey(row.PrimaryKey, out _, out _)))
                throw new InvalidOperationException(
                    "Producción requiere LoteProceso y CodigoPallet válidos antes de generar.");

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
                var createdAt = DateTime.UtcNow;
                try
                {
                    GrmRecepcionRegistrationResponse response;
                    if (tipoProceso == 1)
                        response = await _api.RegisterRecepcionAsync(
                            pendingMetadata.Select(row => new GrmRecepcionRegistrationItem(
                                row.PrimaryKey, report.Id, createdAt, row.Productor, row.Kg)).ToArray());
                    else if (tipoProceso == 2)
                        response = await _api.RegisterCalidadAsync(
                            pendingMetadata.Select(row => new GrmCalidadRegistrationItem(
                                row.PrimaryKey, report.Id, createdAt, row.Productor)).ToArray());
                    else
                        response = await _api.RegisterProduccionAsync(
                            pendingMetadata.Select(row =>
                            {
                                TrySplitProductionKey(row.PrimaryKey, out var lote, out var pallet);
                                return new GrmProduccionRegistrationItem(lote, report.Id, pallet,
                                    createdAt, row.Productor, row.Calibre);
                            }).ToArray());

                    HashSet<string> confirmed;
                    if (tipoProceso == 3)
                    {
                        if (response.InsertadosRegistros is null || response.ExistentesRegistros is null)
                            throw new InvalidOperationException("GRM devolvió un resultado de Producción incompleto.");
                        confirmed = response.InsertadosRegistros.Concat(response.ExistentesRegistros)
                            .Where(identity => identity.ReportId == report.Id)
                            .Select(identity => $"{identity.Lote}|{identity.CodigoPallet}")
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    }
                    else
                    {
                        if (response.Insertados is null || response.Existentes is null)
                            throw new InvalidOperationException("GRM devolvió un resultado incompleto.");
                        confirmed = response.Insertados.Concat(response.Existentes)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    }
                    ConfirmGrmRows(confirmed);
                    _lblStatusText.Text = $"Archivo generado en '{targetFilePath}'. GRM confirmó {confirmed.Count} lote(s); pendientes: {_pendingRecords.Count}.";
                    MessageBox.Show(_pendingRecords.Count == 0
                        ? $"Archivo generado en '{targetFilePath}' y registros confirmados por GRM."
                        : $"Archivo generado en '{targetFilePath}'. Algunos lotes siguen pendientes; revise la persistencia GRM.",
                        "Resultado de generación", MessageBoxButtons.OK,
                        _pendingRecords.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
                catch (Exception postError)
                {
                    try
                    {
                        var persisted = await _api.GetPersistenciasAsync(tipoProceso);
                        _grmPersistenceKnown = true;
                        ConfirmGrmRows(GrmKeys(persisted, tipoProceso, report.Id));
                        _lblStatusText.Text = $"No se confirmó el POST: {postError.Message}. Pendientes: {_pendingRecords.Count}.";
                    }
                    catch (Exception getError)
                    {
                        _grmPersistenceKnown = false;
                        _btnGenerar.Enabled = false;
                        _lblStatusText.Text = $"No se pudo confirmar GRM: {getError.Message}";
                    }
                    MessageBox.Show("El resultado del registro GRM fue incierto. Se consultó nuevamente cuando fue posible; revise los pendientes antes de reintentar.",
                        "Registro no confirmado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            _lblStatusText.Text = $"Error generando archivo: {ex.Message}";
            MessageBox.Show($"Error durante la generación del archivo:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnGenerar.Enabled = _pendingRecords.Count > 0 && _grmPersistenceKnown
                && (!_hasSimulatedPending || _isAdmin);
        }
    }

    private void ConfirmGrmRows(HashSet<string> confirmed)
    {
        foreach (DataGridViewRow row in _gridExecution.Rows)
        {
            if (row.Tag is PendingRecordItem item && confirmed.Contains(item.Metadata.PrimaryKey))
                row.Cells["_colEstado"].Value = "Cargado";
        }
        _pendingRecords.RemoveAll(item => confirmed.Contains(item.Metadata.PrimaryKey));
        if (_pendingRecords.Count == 0)
            _hasSimulatedPending = false;
        _btnGenerar.Enabled = _pendingRecords.Count > 0 && _grmPersistenceKnown && !_hasSimulatedPending;
        _lblStatusPending.Text = $"Pendientes: {_pendingRecords.Count}  |  Cargados: {_gridExecution.Rows.Count - _pendingRecords.Count}";
    }

    private static int TipoProceso(string area) =>
        area.StartsWith("Recepc", StringComparison.OrdinalIgnoreCase) ? 1 :
        area.StartsWith("Calidad", StringComparison.OrdinalIgnoreCase) ? 2 :
        area.StartsWith("Producc", StringComparison.OrdinalIgnoreCase) ? 3 :
        throw new InvalidOperationException($"Área de reporte no reconocida: {area}");

    private static HashSet<string> GrmKeys(
        IEnumerable<GrmRecepcionRecord> records, int tipoProceso, int reportId) =>
        records.Select(record => GrmKey(record, tipoProceso, reportId))
            .OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string? GrmKey(GrmRecepcionRecord record, int tipoProceso, int reportId)
    {
        if (string.IsNullOrWhiteSpace(record.Lote)) return null;
        if (tipoProceso != 3) return record.Lote.Trim();
        if (record.ReportId != reportId || string.IsNullOrWhiteSpace(record.CodigoPallet)
            || record.Lote.Contains('|') || record.CodigoPallet.Contains('|')) return null;
        return $"{record.Lote.Trim()}|{record.CodigoPallet.Trim()}";
    }

    private static bool TrySplitProductionKey(string? key, out string lote, out string pallet)
    {
        lote = pallet = string.Empty;
        if (string.IsNullOrWhiteSpace(key)) return false;
        var parts = key.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0])
            || string.IsNullOrWhiteSpace(parts[1])) return false;
        lote = parts[0];
        pallet = parts[1];
        return true;
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
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar la administración desde GRM:\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
