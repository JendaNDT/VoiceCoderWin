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
        private readonly List<Nalez> _offlineNalezy;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly SpecProjekt _projekt;
        private ListView lvNalezy;
        private Button btnAiCheck;
        private Label lblStatus;
        private CancellationTokenSource _cts = null;

        public NalezyForm(List<Nalez> offlineNalezy, string apiKey, string model, SpecProjekt projekt)
        {
            _offlineNalezy = offlineNalezy;
            _apiKey = apiKey;
            _model = model;
            _projekt = projekt;

            Text = "Kontrola konzistence specifikace";
            Size = new Size(750, 480);
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(500, 350);
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9.5f);

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
                Font = new Font("Segoe UI", 9.25f)
            };
            lvNalezy.Columns.Add("Typ", 110);
            lvNalezy.Columns.Add("Problém / Téma", 180);
            lvNalezy.Columns.Add("Detail / Návrh řešení", 410);

            lblStatus = new Label
            {
                Text = "Kontrola porovnává klíčová slova. Pro sémantické posouzení spusť hloubkovou AI analýzu.",
                Dock = DockStyle.Fill,
                ForeColor = Color.DimGray,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
            };

            btnAiCheck = new Button
            {
                Text = "🧠 Spustit hloubkovou AI analýzu",
                Height = 32,
                AutoSize = true,
                BackColor = Color.FromArgb(16, 35, 63), // Navy
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9.5f)
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
                Cursor = Cursors.Hand
            };
            btnZavrit.FlatAppearance.BorderColor = Color.Silver;
            btnZavrit.Click += (s, e) => Close();

            // Zpřístupníme klávesu ESC pro zavření dialogu
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

            NaplnNalezy(offlineNalezy, isAi: false);

            this.FormClosing += (s, e) =>
            {
                this.Font?.Dispose();
                lvNalezy?.Font?.Dispose();
                lblStatus?.Font?.Dispose();
                btnAiCheck?.Font?.Dispose();
            };
        }

        private void NaplnNalezy(List<Nalez> list, bool isAi)
        {
            lvNalezy.BeginUpdate();
            if (!isAi) lvNalezy.Items.Clear();

            foreach (var n in list)
            {
                var it = new ListViewItem(n.Zavaznost == Zavaznost.Rozpor ? "❗ ROZPOR" : "⚠ Varování");
                it.ForeColor = n.Zavaznost == Zavaznost.Rozpor ? Color.FromArgb(155, 28, 28) : Color.FromArgb(180, 110, 0);
                
                if (isAi)
                {
                    it.Text = n.Zavaznost == Zavaznost.Rozpor ? "🧠 ROZPOR (AI)" : "🧠 Varování (AI)";
                }

                it.SubItems.Add(n.Titulek);
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
            lblStatus.ForeColor = Color.FromArgb(16, 35, 63);

            // Vyčistíme staré nálezy a naplníme seznam pouze výchozími offline nálezy před novou AI kontrolou
            NaplnNalezy(_offlineNalezy, isAi: false);
            _cts = new CancellationTokenSource();

            try
            {
                var aiNalezy = await GeminiService.AnalyzujKonzistenciAsync(_apiKey, _model, _projekt, _cts.Token);
                if (this.IsDisposed || !this.Created) return;
                
                if (aiNalezy.Count == 0)
                {
                    lblStatus.Text = "Gemini AI nenašla žádné další logické rozpory ani bezpečnostní díry. Skvělá práce!";
                    lblStatus.ForeColor = Color.Green;
                }
                else
                {
                    NaplnNalezy(aiNalezy, isAi: true);
                    lblStatus.Text = $"Analýza dokončena. Nalezeno {aiNalezy.Count} nových AI podnětů.";
                    lblStatus.ForeColor = Color.Green;
                }
            }
            catch (Exception ex)
            {
                if (this.IsDisposed || !this.Created) return;
                if (ex is OperationCanceledException || ex.InnerException is OperationCanceledException)
                {
                    lblStatus.Text = "Analýza zrušena uživatelem.";
                    lblStatus.ForeColor = Color.FromArgb(16, 35, 63);
                    return;
                }
                MessageBox.Show(this, "AI analýza selhala:\n\n" + ex.Message, "Chyba AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Během AI analýzy došlo k chybě.";
                lblStatus.ForeColor = Color.Red;
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
