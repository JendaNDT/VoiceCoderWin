using System;
using System.Drawing;
using System.Windows.Forms;
using CodePlanner.Core;

namespace CodePlanner
{
    public class SettingsForm : Form
    {
        private static readonly Color Navy = Color.FromArgb(16, 35, 63);
        private static readonly Color Teal = Color.FromArgb(23, 176, 160);
        private static readonly Color TealSvetla = Color.FromArgb(224, 244, 241);
        private static readonly Color SvetlePozadi = Color.FromArgb(246, 248, 250);
        private static readonly Color SedaText = Color.FromArgb(105, 105, 105);

        private readonly GeminiNastaveni _nastaveni;
        private TextBox txtApiKey;
        private ComboBox cbModel;
        private CheckBox chkZobrazitKlic;
        private LinkLabel lnkGetApiKey;
        private Button btnUlozit;
        private Button btnStorno;
        private Button btnTest;

        public SettingsForm()
        {
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            _nastaveni = GeminiNastaveni.Nacti();

            ClientSize = new Size(500, 275);
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Nastavení Gemini API";
            Font = new Font("Segoe UI", 9.75f);
            BackColor = SvetlePozadi;
            ForeColor = Navy;

            PostavUI();
            NactiHodnoty();

            this.FormClosing += (s, e) =>
            {
                this.Font?.Dispose();
                txtApiKey?.Font?.Dispose();
                cbModel?.Font?.Dispose();
                chkZobrazitKlic?.Font?.Dispose();
                lnkGetApiKey?.Font?.Dispose();
                btnUlozit?.Font?.Dispose();
                btnStorno?.Font?.Dispose();
                btnTest?.Font?.Dispose();
            };
        }

        private void PostavUI()
        {
            var pnlMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(16),
                BackColor = Color.Transparent
            };
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // API key label
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // API key textbox
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Checkbox + LinkLabel flow
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Model label
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Model combobox
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Spacer/Buttons

            var lblApiKey = new Label
            {
                Text = "API Klíč pro Gemini (lze nastavit i přes proměnnou GEMINI_API_KEY):",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.75f),
                Margin = new Padding(0, 0, 0, 4)
            };

            txtApiKey = new TextBox
            {
                Dock = DockStyle.Fill,
                UseSystemPasswordChar = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            var pnlKeyHelper = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 12),
                Height = 24
            };

            chkZobrazitKlic = new CheckBox
            {
                Text = "Zobrazit klíč",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = SedaText,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 12, 0)
            };
            chkZobrazitKlic.CheckedChanged += (s, e) =>
            {
                txtApiKey.UseSystemPasswordChar = !chkZobrazitKlic.Checked;
            };

            lnkGetApiKey = new LinkLabel
            {
                Text = "Získat API klíč v Google AI Studio",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Underline),
                LinkColor = Teal,
                ActiveLinkColor = Color.FromArgb(19, 150, 137),
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 3, 0, 0)
            };
            lnkGetApiKey.LinkClicked += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://aistudio.google.com/") { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Odkaz nelze otevřít: " + ex.Message, "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            pnlKeyHelper.Controls.Add(chkZobrazitKlic);
            pnlKeyHelper.Controls.Add(lnkGetApiKey);

            var lblModel = new Label
            {
                Text = "Model Gemini:",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.75f),
                Margin = new Padding(0, 0, 0, 4)
            };

            cbModel = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 0, 20)
            };
            cbModel.Items.Add("gemini-2.5-flash");
            cbModel.Items.Add("gemini-2.5-pro");
            cbModel.Items.Add("gemini-2.0-flash");
            cbModel.Items.Add("gemini-1.5-flash");
            cbModel.Items.Add("gemini-1.5-pro");

            var pnlTlacitka = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Height = 36,
                Margin = new Padding(0)
            };

            btnStorno = new Button
            {
                Text = "Storno",
                DialogResult = DialogResult.Cancel,
                Size = new Size(90, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Navy,
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnStorno.FlatAppearance.BorderColor = Teal;
            btnStorno.FlatAppearance.MouseOverBackColor = TealSvetla;

            btnUlozit = new Button
            {
                Text = "Uložit",
                Size = new Size(90, 30),
                BackColor = Teal,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnUlozit.FlatAppearance.BorderSize = 0;
            btnUlozit.FlatAppearance.MouseOverBackColor = Color.FromArgb(19, 150, 137);
            btnUlozit.Click += BtnUlozit_Click;

            btnTest = new Button
            {
                Text = "🧪 Test připojení",
                Size = new Size(130, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Navy,
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            btnTest.FlatAppearance.BorderColor = Color.Silver;
            btnTest.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 238, 242);
            btnTest.Click += BtnTest_Click;

            pnlTlacitka.Controls.Add(btnStorno);
            pnlTlacitka.Controls.Add(btnUlozit);
            pnlTlacitka.Controls.Add(btnTest);

            pnlMain.Controls.Add(lblApiKey, 0, 0);
            pnlMain.Controls.Add(txtApiKey, 0, 1);
            pnlMain.Controls.Add(pnlKeyHelper, 0, 2);
            pnlMain.Controls.Add(lblModel, 0, 3);
            pnlMain.Controls.Add(cbModel, 0, 4);

            var wrapper = new Panel { Dock = DockStyle.Fill };
            wrapper.Controls.Add(pnlTlacitka); // Bottom dock first
            wrapper.Controls.Add(pnlMain);      // Fill dock second

            Controls.Add(wrapper);

            AcceptButton = btnUlozit;
            CancelButton = btnStorno;
        }

        private void NactiHodnoty()
        {
            txtApiKey.Text = _nastaveni.GeminiApiKey;
            
            int idx = cbModel.Items.IndexOf(_nastaveni.GeminiModel);
            if (idx >= 0)
            {
                cbModel.SelectedIndex = idx;
            }
            else
            {
                cbModel.Items.Add(_nastaveni.GeminiModel);
                cbModel.SelectedIndex = cbModel.Items.Count - 1;
            }
        }

        private void BtnUlozit_Click(object sender, EventArgs e)
        {
            _nastaveni.GeminiApiKey = txtApiKey.Text.Trim();
            _nastaveni.GeminiModel = cbModel.SelectedItem.ToString();

            try
            {
                _nastaveni.Uloz();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Chyba při ukládání nastavení:\n\n" + ex.Message,
                    "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnTest_Click(object sender, EventArgs e)
        {
            string testKey = txtApiKey.Text.Trim();
            string testModel = cbModel.SelectedItem?.ToString() ?? "gemini-2.5-flash";

            if (string.IsNullOrWhiteSpace(testKey))
            {
                MessageBox.Show(this, "Pro testování zadejte platný API klíč.", "Test připojení", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnTest.Enabled = false;
            btnTest.Text = "🧪 Testuji...";
            Cursor = Cursors.WaitCursor;

            try
            {
                await GeminiService.TestPripojeniAsync(testKey, testModel);
                MessageBox.Show(this, "Připojení k Gemini API bylo úspěšně ověřeno. Váš API klíč je platný!", "Test úspěšný", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Test připojení selhal:\n\n" + ex.Message, "Chyba připojení", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTest.Enabled = true;
                btnTest.Text = "🧪 Test připojení";
                Cursor = Cursors.Default;
            }
        }
    }
}
