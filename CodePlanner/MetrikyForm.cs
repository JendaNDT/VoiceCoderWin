using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using CodePlanner.Core;

namespace CodePlanner
{
    public class MetrikyForm : Form
    {
        private readonly ProjectMetrics _metrics;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly ProjectSpecification _project;
        private readonly Action _onZmena;

        private Label lblCasOdhaduVal;
        private Label lblKomplexitaVal;
        private Label lblRozpocetVal;
        private Label lblSlozeniVal;
        private RichTextBox rtbRozbor;
        private RichTextBox rtbRizika;
        private Label lblStatus;
        private Button btnAiMetriky;
        private Button btnCopy;
        private CancellationTokenSource _cts = null;

        public MetrikyForm(ProjectMetrics metrics, string apiKey, string model, ProjectSpecification project, Action onZmena)
        {
            _metrics = metrics;
            _apiKey = apiKey;
            _model = model;
            _project = project;
            _onZmena = onZmena;

            Text = "Metriky a odhad projektu";
            Size = new Size(820, 580);
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(600, 400);
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = true;
            Font = DesignSystem.Body;
            BackColor = DesignSystem.SvetlePozadi;
            ForeColor = DesignSystem.Navy;

            // Esc closes the form
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content split
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Bottom action bar

            // Header panel (Navy)
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DesignSystem.Navy
            };
            var lblTitle = new Label
            {
                Text = "📊 AI Odhad a projektové metriky",
                ForeColor = Color.White,
                Font = DesignSystem.HeaderMedium,
                Dock = DockStyle.Left,
                Width = 400,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            pnlHeader.Controls.Add(lblTitle);

            // Split Container
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 320,
                BorderStyle = BorderStyle.None
            };

            // LEVÝ PANEL: vizuální vizitka a hlavní metriky
            var pnlLeft = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(245, 247, 250)
            };

            float scale = 1.0f;
            int cardHeight = (int)(65 * scale);
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Panel VytvorCard(string label, out Label valLabel)
            {
                var card = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(0, 0, 0, 8),
                    Padding = new Padding(8)
                };

                var lblName = new Label
                {
                    Text = label,
                    ForeColor = DesignSystem.SedaText,
                    Font = DesignSystem.SmallBold,
                    Dock = DockStyle.Top,
                    Height = (int)(18 * scale)
                };

                valLabel = new Label
                {
                    ForeColor = DesignSystem.Navy,
                    Font = DesignSystem.BodyBold,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                card.Controls.Add(valLabel);
                card.Controls.Add(lblName);
                return card;
            }

            pnlLeft.Controls.Add(VytvorCard("ODHADOVANÁ DOBA VÝVOJE", out lblCasOdhaduVal), 0, 0);
            pnlLeft.Controls.Add(VytvorCard("SLOŽITOST PROJEKTU", out lblKomplexitaVal), 0, 1);
            pnlLeft.Controls.Add(VytvorCard("DOPORUČENÝ ROZPOČET", out lblRozpocetVal), 0, 2);
            pnlLeft.Controls.Add(VytvorCard("SLOŽENÍ TÝMU", out lblSlozeniVal), 0, 3);

            split.Panel1.Controls.Add(pnlLeft);

            // PRAVÝ PANEL: technický rozbor a rizika
            var tabContent = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = DesignSystem.Body
            };

            var tabRozbor = new TabPage("🔧 Technický rozbor a architektura");
            rtbRozbor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = DesignSystem.Body,
                Padding = new Padding(8)
            };
            tabRozbor.Controls.Add(rtbRozbor);

            var tabRizika = new TabPage("⚠ Odhadovaná rizika projektu");
            rtbRizika = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = DesignSystem.Body,
                Padding = new Padding(8)
            };
            tabRizika.Controls.Add(rtbRizika);

            tabContent.TabPages.Add(tabRozbor);
            tabContent.TabPages.Add(tabRizika);

            split.Panel2.Controls.Add(tabContent);

            // Bottom bar
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(235, 238, 242),
                Padding = new Padding(6)
            };

            lblStatus = new Label
            {
                Dock = DockStyle.Left,
                Width = 350,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = DesignSystem.SedaText,
                Font = DesignSystem.BodyItalic
            };

            btnAiMetriky = new Button
            {
                Text = "🤖 Spočítat odhad přes AI",
                Width = 200,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = DesignSystem.Navy,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = DesignSystem.BodyBold
            };
            btnAiMetriky.FlatAppearance.BorderSize = 0;
            btnAiMetriky.Click += BtnAiMetriky_Click;

            btnCopy = new Button
            {
                Text = "📋 Kopírovat text",
                Width = 140,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = DesignSystem.Navy,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0),
                Font = DesignSystem.Body
            };
            btnCopy.FlatAppearance.BorderColor = Color.Silver;
            btnCopy.Click += BtnCopy_Click;

            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(btnCopy);
            pnlBottom.Controls.Add(btnAiMetriky);

            mainLayout.Controls.Add(pnlHeader, 0, 0);
            mainLayout.Controls.Add(split, 0, 1);
            mainLayout.Controls.Add(pnlBottom, 0, 2);

            Controls.Add(mainLayout);

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                btnAiMetriky.Enabled = false;
                btnAiMetriky.Text = "🤖 Spočítat odhad (chybí API klíč)";
            }

            NaplnMetriky();
        }

        private void NaplnMetriky()
        {
            if (_metrics == null || _metrics.CalculationTimestamp == default)
            {
                lblCasOdhaduVal.Text = "Zatím nespočítáno";
                lblKomplexitaVal.Text = "Zatím nespočítáno";
                lblRozpocetVal.Text = "Zatím nespočítáno";
                lblSlozeniVal.Text = "Zatím nespočítáno";
                rtbRozbor.Text = "Klikněte na tlačítko 'Spočítat odhad přes AI' pro asynchronní analýzu specifikace a backlogu pomocí Gemini API.";
                rtbRizika.Text = "Seznam rizik bude vygenerován spolu s odhadem.";
                lblStatus.Text = "Odhad dosud nebyl vygenerován.";
                btnCopy.Enabled = false;
                return;
            }

            btnCopy.Enabled = true;
            lblCasOdhaduVal.Text = $"{_metrics.TimeEstimateMin} až {_metrics.TimeEstimateMax}";
            lblKomplexitaVal.Text = _metrics.Complexity;
            
            // Barevné odlišení komplexity
            if (_metrics.Complexity.Contains("Vysoká"))
                lblKomplexitaVal.ForeColor = DesignSystem.Cervena;
            else if (_metrics.Complexity.Contains("Střední"))
                lblKomplexitaVal.ForeColor = DesignSystem.Oranzova;
            else
                lblKomplexitaVal.ForeColor = DesignSystem.Zelena;

            lblRozpocetVal.Text = _metrics.RecommendedBudget;
            lblSlozeniVal.Text = _metrics.TeamComposition;

            rtbRozbor.Text = _metrics.TechnicalAnalysis;
            
            rtbRizika.Clear();
            if (_metrics.MetricRisks != null && _metrics.MetricRisks.Count > 0)
            {
                foreach (var r in _metrics.MetricRisks)
                {
                    rtbRizika.AppendText($"• {r}\n\n");
                }
            }
            else
            {
                rtbRizika.Text = "Nebyly definovány žádné specifické hrozby.";
            }

            lblStatus.Text = $"Odhad aktualizován: {_metrics.CalculationTimestamp:d. M. yyyy v H:mm}";
        }

        private async void BtnAiMetriky_Click(object sender, EventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                return;
            }

            Cursor = Cursors.WaitCursor;
            btnAiMetriky.Text = "❌ Zrušit odhad";
            btnAiMetriky.Enabled = true;
            lblStatus.Text = "Počítám odhad pomocí Gemini API...";
            _cts = new CancellationTokenSource();

            try
            {
                var noveMetriky = await GeminiService.GenerateMetricsAsync(_apiKey, _model, _project, _cts.Token);
                if (this.IsDisposed || !this.Created) return;
                
                _metrics.TimeEstimateMin = noveMetriky.TimeEstimateMin;
                _metrics.TimeEstimateMax = noveMetriky.TimeEstimateMax;
                _metrics.Complexity = noveMetriky.Complexity;
                _metrics.RecommendedBudget = noveMetriky.RecommendedBudget;
                _metrics.TeamComposition = noveMetriky.TeamComposition;
                _metrics.TechnicalAnalysis = noveMetriky.TechnicalAnalysis;
                _metrics.MetricRisks = noveMetriky.MetricRisks;
                _metrics.CalculationTimestamp = DateTime.Now;

                _project.ChangeLog.Add(new DecisionLogEntry
                {
                    Timestamp = DateTime.Now,
                    Action = "Projektové metriky",
                    Detail = $"AI odhad pracnosti: {_metrics.TimeEstimateMin} až {_metrics.TimeEstimateMax} (komplexita: {_metrics.Complexity})."
                });

                _onZmena();
                NaplnMetriky();
                MessageBox.Show(this, "AI analýza a odhad projektu byly úspěšně dokončeny.", "Odhad hotov", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (this.IsDisposed || !this.Created) return;
                if (ex is OperationCanceledException || ex.InnerException is OperationCanceledException)
                {
                    lblStatus.Text = "Výpočet zrušen uživatelem.";
                    return;
                }
                MessageBox.Show(this, "Výpočet odhadu selhal:\n\n" + ex.Message, "Chyba AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Výpočet selhal.";
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                if (!this.IsDisposed && this.Created)
                {
                    btnAiMetriky.Enabled = !string.IsNullOrWhiteSpace(_apiKey);
                    btnAiMetriky.Text = "🤖 Spočítat odhad přes AI";
                    Cursor = Cursors.Default;
                }
            }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== METRIKY A ODHAD PROJEKTU ===");
            sb.AppendLine($"Projekt: {_project.Name}");
            sb.AppendLine($"Odhad času: {_metrics.TimeEstimateMin} až {_metrics.TimeEstimateMax}");
            sb.AppendLine($"Složitost: {_metrics.Complexity}");
            sb.AppendLine($"Doporučený rozpočet: {_metrics.RecommendedBudget}");
            sb.AppendLine($"Doporučené složení týmu: {_metrics.TeamComposition}");
            sb.AppendLine();
            sb.AppendLine("--- Technický rozbor ---");
            sb.AppendLine(_metrics.TechnicalAnalysis);
            sb.AppendLine();
            sb.AppendLine("--- Odhadovaná rizika ---");
            foreach (var r in _metrics.MetricRisks)
            {
                sb.AppendLine($"- {r}");
            }

            try
            {
                Clipboard.SetText(sb.ToString());
                MessageBox.Show(this, "Celý text odhadu byl zkopírován do schránky.", "Zkopírováno", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Nepodařilo se kopírovat do schránky:\n\n" + ex.Message, "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
