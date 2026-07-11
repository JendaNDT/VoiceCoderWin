using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using CodePlanner.Core;

namespace CodePlanner
{
    public class UserStoriesForm : Form
    {
        private readonly List<UserStory> _stories;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly SpecProjekt _projekt;
        private readonly Action _onZmena;

        private ListBox lstStories;
        private RichTextBox rtbDetail;
        private Label lblStatus;
        private Button btnAiStories;
        private Button btnExportMd;
        private Button btnExportCsv;
        private CancellationTokenSource _cts = null;

        // Cachované fonty k zamezení GDI leaků
        private Font _fTitle;
        private Font _fBold;
        private Font _fItalic;
        private Font _fRegular;
        private Font _fHeader105;

        public UserStoriesForm(List<UserStory> stories, string apiKey, string model, SpecProjekt projekt, Action onZmena)
        {
            _stories = stories;
            _apiKey = apiKey;
            _model = model;
            _projekt = projekt;
            _onZmena = onZmena;

            _fTitle = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
            _fBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _fItalic = new Font("Segoe UI", 10f, FontStyle.Italic);
            _fRegular = new Font("Segoe UI", 10f, FontStyle.Regular);
            _fHeader105 = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);

            Text = "Uživatelské příběhy (User Stories)";
            Size = new Size(850, 580);
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowIcon = false;
            Font = new Font("Segoe UI", 9.5f);

            PostavUI();
            NaplnStories();

            this.FormClosing += (s, e) =>
            {
                this.Font?.Dispose();
                _fTitle?.Dispose();
                _fBold?.Dispose();
                _fItalic?.Dispose();
                _fRegular?.Dispose();
                _fHeader105?.Dispose();
            };
        }

        private void PostavUI()
        {
            var navy = Color.FromArgb(16, 35, 63);
            var teal = Color.FromArgb(23, 176, 160);
            var pozadi = Color.FromArgb(245, 247, 250);

            // 1. Hlavička
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = navy
            };
            var lblTitle = new Label
            {
                Text = "💡 Uživatelské příběhy (User Stories / Backlog)",
                Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(16, 16),
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblTitle);

            // 2. Dolní lišta s tlačítky
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = Color.White
            };

            lblStatus = new Label
            {
                Text = "Načteno",
                ForeColor = Color.DimGray,
                Location = new Point(16, 18),
                AutoSize = true
            };

            btnAiStories = new Button
            {
                Text = "🤖 Generovat přes Gemini",
                Height = 32,
                AutoSize = true,
                BackColor = navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            btnAiStories.FlatAppearance.BorderSize = 0;
            btnAiStories.Click += BtnAiStories_Click;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                btnAiStories.Enabled = false;
                btnAiStories.Text = "🤖 Generovat (chybí API klíč)";
            }

            btnExportMd = new Button
            {
                Text = "⬇ Export Markdown…",
                Height = 32,
                Width = 135,
                FlatStyle = FlatStyle.Flat,
                ForeColor = navy,
                Cursor = Cursors.Hand
            };
            btnExportMd.FlatAppearance.BorderColor = teal;
            btnExportMd.Click += BtnExportMd_Click;

            btnExportCsv = new Button
            {
                Text = "⬇ Export CSV (Jira/Trello)…",
                Height = 32,
                Width = 175,
                FlatStyle = FlatStyle.Flat,
                ForeColor = navy,
                Cursor = Cursors.Hand
            };
            btnExportCsv.FlatAppearance.BorderColor = teal;
            btnExportCsv.Click += BtnExportCsv_Click;

            var btnZavrit = new Button
            {
                Text = "Zavřít",
                Height = 32,
                Width = 80,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnZavrit.FlatAppearance.BorderColor = Color.Silver;
            btnZavrit.Click += (s, e) => Close();

            // Klávesa ESC pro zavření
            this.CancelButton = btnZavrit;

            var flowButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Width = 580,
                Padding = new Padding(0, 10, 10, 0)
            };
            flowButtons.Controls.Add(btnAiStories);
            flowButtons.Controls.Add(btnExportMd);
            flowButtons.Controls.Add(btnExportCsv);
            flowButtons.Controls.Add(btnZavrit);

            pnlFooter.Controls.Add(flowButtons);
            pnlFooter.Controls.Add(lblStatus);

            // 3. Středová část – SplitContainer
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 250,
                BackColor = pozadi
            };

            lstStories = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5f),
                IntegralHeight = false
            };
            lstStories.SelectedIndexChanged += LstStories_SelectedIndexChanged;
            split.Panel1.Controls.Add(lstStories);

            rtbDetail = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 10f),
                Padding = new Padding(12)
            };
            split.Panel2.Controls.Add(rtbDetail);

            Controls.Add(split);
            Controls.Add(pnlHeader);
            Controls.Add(pnlFooter);
        }

        private void NaplnStories()
        {
            lstStories.BeginUpdate();
            lstStories.Items.Clear();
            foreach (var s in _stories)
            {
                string prioritaTag = s.Priorita == "Vysoká" ? "🔴 " : (s.Priorita == "Střední" ? "🟡 " : "🟢 ");
                lstStories.Items.Add($"{prioritaTag}{s.Id}: {s.Titulek}");
            }
            lstStories.EndUpdate();

            if (_stories.Count > 0)
            {
                lstStories.SelectedIndex = 0;
                lblStatus.Text = $"Načteno {_stories.Count} User Stories.";
                btnExportMd.Enabled = true;
                btnExportCsv.Enabled = true;
            }
            else
            {
                rtbDetail.Text = "Zatím nebyly vygenerovány žádné User Stories.\n\nKlikněte na tlačítko \"🤖 Generovat přes Gemini\" pro vytvoření agilního backlogu na základě aktuální specifikace.";
                lblStatus.Text = "Žádné User Stories k dispozici.";
                btnExportMd.Enabled = false;
                btnExportCsv.Enabled = false;
            }
        }

        private void LstStories_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = lstStories.SelectedIndex;
            if (idx < 0 || idx >= _stories.Count)
            {
                return;
            }

            var s = _stories[idx];
            rtbDetail.Clear();

            // Vykreslíme detail s využitím cachovaných fontů
            rtbDetail.SelectionFont = _fTitle;
            rtbDetail.SelectionColor = Color.FromArgb(16, 35, 63);
            rtbDetail.AppendText($"{s.Id}: {s.Titulek}\n\n");

            rtbDetail.SelectionFont = _fBold;
            rtbDetail.SelectionColor = Color.DimGray;
            rtbDetail.AppendText("Priorita: ");
            rtbDetail.SelectionFont = _fBold;
            rtbDetail.SelectionColor = s.Priorita == "Vysoká" ? Color.Red : (s.Priorita == "Střední" ? Color.DarkGoldenrod : Color.Green);
            rtbDetail.AppendText($"{s.Priorita}\n\n");

            rtbDetail.SelectionFont = _fHeader105;
            rtbDetail.SelectionColor = Color.FromArgb(16, 35, 63);
            rtbDetail.AppendText("Uživatelský příběh (User Story)\n");
            
            rtbDetail.SelectionFont = _fItalic;
            rtbDetail.SelectionColor = Color.FromArgb(50, 50, 50);
            rtbDetail.AppendText($"> {s.Popis}\n\n");

            rtbDetail.SelectionFont = _fHeader105;
            rtbDetail.SelectionColor = Color.FromArgb(16, 35, 63);
            rtbDetail.AppendText("Akceptační kritéria (Acceptance Criteria)\n");

            rtbDetail.SelectionFont = _fRegular;
            rtbDetail.SelectionColor = Color.Black;
            foreach (var k in s.Kriteria)
            {
                rtbDetail.AppendText($"• {k}\n");
            }
        }

        private async void BtnAiStories_Click(object sender, EventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                return;
            }

            Cursor = Cursors.WaitCursor;
            btnAiStories.Text = "❌ Zrušit generování";
            btnAiStories.Enabled = true;
            lblStatus.Text = "Volám Gemini API, chvíli strpení...";
            _cts = new CancellationTokenSource();

            try
            {
                var noveStories = await GeminiService.GenerujUserStoriesAsync(_apiKey, _model, _projekt, _cts.Token);
                if (this.IsDisposed || !this.Created) return;
                _stories.Clear();
                _stories.AddRange(noveStories);

                // Zaznamenáme akci do logu
                _projekt.Log.Add(new Rozhodnuti
                {
                    Cas = DateTime.Now,
                    Akce = "User Stories",
                    Detail = $"Vygenerováno {_stories.Count} uživatelských příběhů přes Gemini."
                });

                _onZmena(); // Uložíme změnu projektu (označíme dirty)
                NaplnStories();
                MessageBox.Show(this, $"Úspěšně vygenerováno {_stories.Count} User Stories.", "Generování dokončeno", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (this.IsDisposed || !this.Created) return;
                if (ex is OperationCanceledException || ex.InnerException is OperationCanceledException)
                {
                    lblStatus.Text = "Generování zrušeno uživatelem.";
                    return;
                }
                MessageBox.Show(this, "Chyba při generování User Stories:\n\n" + ex.Message, "Chyba AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Generování selhalo.";
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                if (!this.IsDisposed && this.Created)
                {
                    btnAiStories.Text = "🤖 Generovat přes Gemini";
                    btnAiStories.Enabled = !string.IsNullOrWhiteSpace(_apiKey);
                    Cursor = Cursors.Default;
                }
            }
        }

        private void BtnExportMd_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog
            {
                Title = "Export User Stories do Markdown",
                Filter = "Markdown (*.md)|*.md",
                FileName = "user_stories_" + MainForm.BezpecnyNazevSouboru(_projekt.Nazev, "projekt") + ".md"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        ExportujMarkdown(dlg.FileName);
                        MessageBox.Show(this, "Markdown export dokončen:\n\n" + dlg.FileName, "Export dokončen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Export selhal:\n\n" + ex.Message, "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnExportCsv_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog
            {
                Title = "Export User Stories do CSV",
                Filter = "CSV soubory (*.csv)|*.csv",
                FileName = "user_stories_" + MainForm.BezpecnyNazevSouboru(_projekt.Nazev, "projekt") + ".csv"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        ExportujCsv(dlg.FileName);
                        MessageBox.Show(this, "CSV export dokončen (soubor lze importovat do Jira/Trello):\n\n" + dlg.FileName, "Export dokončen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Export selhal:\n\n" + ex.Message, "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportujCsv(string soubor)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Issue ID,Summary,Description,Priority");
            foreach (var s in _stories)
            {
                string id = EscapeCsv(s.Id);
                string sum = EscapeCsv(s.Titulek);
                
                var descBuilder = new StringBuilder();
                descBuilder.AppendLine(s.Popis);
                descBuilder.AppendLine();
                descBuilder.AppendLine("Akceptační kritéria:");
                foreach (var k in s.Kriteria)
                {
                    descBuilder.AppendLine($"- {k}");
                }
                string desc = EscapeCsv(descBuilder.ToString());
                string prio = EscapeCsv(s.Priorita);

                sb.AppendLine($"{id},{sum},{desc},{prio}");
            }
            File.WriteAllText(soubor, sb.ToString(), Encoding.UTF8);
        }

        private void ExportujMarkdown(string soubor)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# User Stories pro projekt: {_projekt.Nazev}");
            sb.AppendLine($"Datum vygenerování: {DateTime.Now:d. M. yyyy}");
            sb.AppendLine();
            foreach (var s in _stories)
            {
                sb.AppendLine($"## {s.Id}: {s.Titulek}");
                sb.AppendLine($"**Priorita:** {s.Priorita}");
                sb.AppendLine();
                sb.AppendLine($"> {s.Popis}");
                sb.AppendLine();
                sb.AppendLine("### Akceptační kritéria:");
                foreach (var k in s.Kriteria)
                {
                    sb.AppendLine($"- [ ] {k}");
                }
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }
            File.WriteAllText(soubor, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string safeText = text;
            if (safeText.StartsWith("=") || safeText.StartsWith("+") || safeText.StartsWith("-") || safeText.StartsWith("@"))
            {
                safeText = "'" + safeText;
            }
            if (safeText.Contains("\"") || safeText.Contains(",") || safeText.Contains("\n") || safeText.Contains("\r"))
            {
                return "\"" + safeText.Replace("\"", "\"\"") + "\"";
            }
            return safeText;
        }
    }
}
