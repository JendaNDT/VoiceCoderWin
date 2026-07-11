using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using CodePlanner.Core;

namespace CodePlanner
{
    public class NalezyForm : Form
    {
        private readonly List<ConsistencyFinding> _offlineFindings;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly ProjectSpecification _project;
        private ListView lvNalezy;
        private Button btnAiCheck;
        private Label lblStatus;
        private CancellationTokenSource _cts = null;

        public NalezyForm(List<ConsistencyFinding> offlineFindings, string apiKey, string model, ProjectSpecification project)
        {
            _offlineFindings = offlineFindings;
            _apiKey = apiKey;
            _model = model;
            _project = project;

            Text = "Kontrola konzistence specifikace";
            Size = new Size(750, 480);
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(500, 350);
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = DesignSystem.Body;
            BackColor = DesignSystem.SvetlePozadi;
            ForeColor = DesignSystem.Navy;

            this.Resize += (s, e) =>
            {
                int totalWidth = lvNalezy.ClientSize.Width;
                if (totalWidth > 320)
                {
                    lvNalezy.Columns[0].Width = (int)(totalWidth * 0.15);
                    lvNalezy.Columns[1].Width = (int)(totalWidth * 0.25);
                    lvNalezy.Columns[2].Width = totalWidth - lvNalezy.Columns[0].Width - lvNalezy.Columns[1].Width - 4;
                }
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // ListView
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));  // Status label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Buttons

            lvNalezy = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.FixedSingle,
                Font = DesignSystem.Body
            };
            lvNalezy.Columns.Add("Typ", 110);
            lvNalezy.Columns.Add("Problém / Téma", 180);
            lvNalezy.Columns.Add("Detail / Návrh řešení", 410);

            lblStatus = new Label
            {
                Text = "Kontrola porovnává klíčová slova. Pro sémantické posouzení spusť hloubkovou AI analýzu.",
                Dock = DockStyle.Fill,
                ForeColor = DesignSystem.SedaText,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = DesignSystem.BodyItalic
            };

            btnAiCheck = new Button
            {
                Text = "🧠 Spustit hloubkovou AI analýzu",
                Height = 32,
                AutoSize = true,
                BackColor = DesignSystem.Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = DesignSystem.BodyBold
            };
            btnAiCheck.FlatAppearance.BorderSize = 0;
            btnAiCheck.Click += BtnAiCheck_Click;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                btnAiCheck.Enabled = false;
                btnAiCheck.Text = "🧠 Spustit hloubkovou AI analýzu (chybí API klíč)";
            }

            var btnZavrit = new Button
            {
                Text = "Zavřít",
                Height = 32,
                Width = 100,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = DesignSystem.Body
            };
            btnZavrit.FlatAppearance.BorderColor = Color.Silver;
            btnZavrit.Click += (s, e) => Close();

            this.CancelButton = btnZavrit;

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            flow.Controls.Add(btnAiCheck);
            flow.Controls.Add(btnZavrit);

            layout.Controls.Add(lvNalezy, 0, 0);
            layout.Controls.Add(lblStatus, 0, 1);
            layout.Controls.Add(flow, 0, 2);

            Controls.Add(layout);

            NaplnNalezy(_offlineFindings, isAi: false);
        }

        // Small italic font getter for the help label
        private static Font DesignSystemSmallItalic => DesignSystem.BodyItalic;

        private void NaplnNalezy(List<ConsistencyFinding> list, bool isAi)
        {
            lvNalezy.BeginUpdate();
            if (!isAi) lvNalezy.Items.Clear();

            foreach (var n in list)
            {
                bool isConflict = n.Severity == Severity.Conflict;
                var it = new ListViewItem(isConflict ? "❗ ROZPOR" : "⚠ Varování");
                it.ForeColor = isConflict ? DesignSystem.Cervena : DesignSystem.Oranzova;
                
                if (isAi)
                {
                    it.Text = isConflict ? "🧠 ROZPOR (AI)" : "🧠 Varování (AI)";
                }

                it.SubItems.Add(n.Title);
                it.SubItems.Add(n.Detail);
                lvNalezy.Items.Add(it);
            }
            lvNalezy.EndUpdate();
        }

        private async void BtnAiCheck_Click(object sender, EventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                return;
            }

            btnAiCheck.Text = "❌ Zrušit analýzu";
            btnAiCheck.Enabled = true;
            lblStatus.Text = "Volám Gemini API pro hloubkovou kontrolu, chvíli strpení...";
            lblStatus.ForeColor = DesignSystem.Navy;

            // Vyčistíme staré nálezy a naplníme seznam pouze výchozími offline nálezy před novou AI kontrolou
            NaplnNalezy(_offlineFindings, isAi: false);
            _cts = new CancellationTokenSource();

            try
            {
                var aiFindings = await GeminiService.AnalyzeConsistencyAsync(_apiKey, _model, _project, _cts.Token);
                if (this.IsDisposed || !this.Created) return;
                
                if (aiFindings.Count == 0)
                {
                    lblStatus.Text = "Gemini AI nenašla žádné další logické rozpory ani bezpečnostní díry. Skvělá práce!";
                    lblStatus.ForeColor = DesignSystem.Zelena;
                }
                else
                {
                    NaplnNalezy(aiFindings, isAi: true);
                    lblStatus.Text = $"Analýza dokončena. Nalezeno {aiFindings.Count} nových AI podnětů.";
                    lblStatus.ForeColor = DesignSystem.Zelena;
                }
            }
            catch (Exception ex)
            {
                if (this.IsDisposed || !this.Created) return;
                if (ex is OperationCanceledException || ex.InnerException is OperationCanceledException)
                {
                    lblStatus.Text = "Analýza zrušena uživatelem.";
                    lblStatus.ForeColor = DesignSystem.Navy;
                    return;
                }
                MessageBox.Show(this, "AI analýza selhala:\n\n" + ex.Message, "Chyba AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Během AI analýzy došlo k chybě.";
                lblStatus.ForeColor = DesignSystem.Cervena;
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                if (!this.IsDisposed && this.Created)
                {
                    btnAiCheck.Text = "🧠 Spustit hloubkovou AI analýzu";
                    btnAiCheck.Enabled = !string.IsNullOrWhiteSpace(_apiKey);
                }
            }
        }
    }
}
