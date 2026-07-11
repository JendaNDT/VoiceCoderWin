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
        private readonly ProjectSpecification _project;
        private readonly Action _onZmena;

        private ListBox lstStories;
        private RichTextBox rtbDetail;
        private Label lblStatus;
        private Button btnAiStories;
        private Button btnExportMd;
        private Button btnExportCsv;
        private CancellationTokenSource _cts = null;

        public UserStoriesForm(List<UserStory> stories, string apiKey, string model, ProjectSpecification project, Action onZmena)
        {
            _stories = stories;
            _apiKey = apiKey;
            _model = model;
            _project = project;
            _onZmena = onZmena;

            Text = "Uživatelské příběhy (User Stories)";
            Size = new Size(850, 580);
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowIcon = false;
            Font = DesignSystem.Body;
            BackColor = DesignSystem.SvetlePozadi;
            ForeColor = DesignSystem.Navy;

            PostavUI();
            NaplnStories();
        }

        private void PostavUI()
        {
            // 1. Hlavička
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = DesignSystem.Navy
            };
            var lblTitle = new Label
            {
                Text = "💡 Uživatelské příběhy (User Stories / Backlog)",
                Font = DesignSystem.HeaderLarge,
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
                ForeColor = DesignSystem.SedaText,
                Location = new Point(16, 18),
                AutoSize = true,
                Font = DesignSystem.Body
            };

            btnAiStories = new Button
            {
                Text = "🤖 Generovat přes Gemini",
                Height = 32,
                AutoSize = true,
                BackColor = DesignSystem.Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = DesignSystem.BodyBold
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
                ForeColor = DesignSystem.Navy,
                Cursor = Cursors.Hand,
                Font = DesignSystem.Body
            };
            btnExportMd.FlatAppearance.BorderColor = DesignSystem.Teal;
            btnExportMd.Click += BtnExportMd_Click;

            btnExportCsv = new Button
            {
                Text = "⬇ Export CSV (Jira/Trello)…",
                Height = 32,
                Width = 175,
                FlatStyle = FlatStyle.Flat,
                ForeColor = DesignSystem.Navy,
                Cursor = Cursors.Hand,
                Font = DesignSystem.Body
            };
            btnExportCsv.FlatAppearance.BorderColor = DesignSystem.Teal;
            btnExportCsv.Click += BtnExportCsv_Click;

            var btnZavrit = new Button
            {
                Text = "Zavřít",
                Height = 32,
                Width = 80,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = DesignSystem.Body
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
                BackColor = DesignSystem.SvetlePozadi
            };

            lstStories = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = DesignSystem.Body,
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
                Font = DesignSystem.Body,
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
                string prioritaTag = s.Priority == "Vysoká" ? "🔴 " : (s.Priority == "Střední" ? "🟡 " : "🟢 ");
                lstStories.Items.Add($"{prioritaTag}{s.Id}: {s.Title}");
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

            // Vykreslíme detail s využitím static fontů z DesignSystem
            rtbDetail.SelectionFont = DesignSystem.HeaderMedium;
            rtbDetail.SelectionColor = DesignSystem.Navy;
            rtbDetail.AppendText($"{s.Id}: {s.Title}\n\n");

            rtbDetail.SelectionFont = DesignSystem.BodyBold;
            rtbDetail.SelectionColor = DesignSystem.SedaText;
            rtbDetail.AppendText("Priorita: ");
            rtbDetail.SelectionFont = DesignSystem.BodyBold;
            rtbDetail.SelectionColor = s.Priority == "Vysoká" ? DesignSystem.Cervena : (s.Priority == "Střední" ? DesignSystem.Oranzova : DesignSystem.Zelena);
            rtbDetail.AppendText($"{s.Priority}\n\n");

            rtbDetail.SelectionFont = DesignSystem.CardHeader;
            rtbDetail.SelectionColor = DesignSystem.Navy;
            rtbDetail.AppendText("Uživatelský příběh (User Story)\n");
            
            rtbDetail.SelectionFont = DesignSystem.BodyItalic;
            rtbDetail.SelectionColor = Color.FromArgb(50, 50, 50);
            rtbDetail.AppendText($"> {s.Description}\n\n");

            rtbDetail.SelectionFont = DesignSystem.CardHeader;
            rtbDetail.SelectionColor = DesignSystem.Navy;
            rtbDetail.AppendText("Akceptační kritéria (Acceptance Criteria)\n");

            rtbDetail.SelectionFont = DesignSystem.Body;
            rtbDetail.SelectionColor = Color.Black;
            foreach (var k in s.Criteria)
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
                var noveStories = await GeminiService.GenerateUserStoriesAsync(_apiKey, _model, _project, _cts.Token);
                if (this.IsDisposed || !this.Created) return;
                _stories.Clear();
                _stories.AddRange(noveStories);

                // Zaznamenáme akci do logu
                _project.ChangeLog.Add(new DecisionLogEntry
                {
                    Timestamp = DateTime.Now,
                    Action = "User Stories",
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
                FileName = "user_stories_" + MainForm.BezpecnyNazevSouboru(_project.Name, "projekt") + ".md"
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
                FileName = "user_stories_" + MainForm.BezpecnyNazevSouboru(_project.Name, "projekt") + ".csv"
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
                string sum = EscapeCsv(s.Title);
                
                var descBuilder = new StringBuilder();
                descBuilder.AppendLine(s.Description);
                descBuilder.AppendLine();
                descBuilder.AppendLine("Akceptační kritéria:");
                foreach (var k in s.Criteria)
                {
                    descBuilder.AppendLine($"- {k}");
                }
                string desc = EscapeCsv(descBuilder.ToString());
                string prio = EscapeCsv(s.Priority);

                sb.AppendLine($"{id},{sum},{desc},{prio}");
            }
            File.WriteAllText(soubor, sb.ToString(), Encoding.UTF8);
        }

        private void ExportujMarkdown(string soubor)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# User Stories pro projekt: {_project.Name}");
            sb.AppendLine($"Datum vygenerování: {DateTime.Now:d. M. yyyy}");
            sb.AppendLine();
            foreach (var s in _stories)
            {
                sb.AppendLine($"## {s.Id}: {s.Title}");
                sb.AppendLine($"**Priorita:** {s.Priority}");
                sb.AppendLine();
                sb.AppendLine($"> {s.Description}");
                sb.AppendLine();
                sb.AppendLine("### Akceptační kritéria:");
                foreach (var k in s.Criteria)
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
