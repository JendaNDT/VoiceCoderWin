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
        private Button btnUlozit;
        private Button btnStorno;

        public SettingsForm()
        {
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            _nastaveni = GeminiNastaveni.Nacti();

            ClientSize = new Size(460, 240);
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
        }

        private void PostavUI()
        {
            var pnlMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(16),
                BackColor = Color.Transparent
            };
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // API key label
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // API key textbox + checkbox flow
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Model label
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Model combobox
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Buttons flow

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

            chkZobrazitKlic = new CheckBox
            {
                Text = "Zobrazit klíč",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = SedaText,
                Margin = new Padding(0, 0, 0, 12),
                Cursor = Cursors.Hand
            };
            chkZobrazitKlic.CheckedChanged += (s, e) =>
            {
                txtApiKey.UseSystemPasswordChar = !chkZobrazitKlic.Checked;
            };

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
                Margin = new Padding(0)
            };
            btnUlozit.FlatAppearance.BorderSize = 0;
            btnUlozit.FlatAppearance.MouseOverBackColor = Color.FromArgb(19, 150, 137);
            btnUlozit.Click += BtnUlozit_Click;

            pnlTlacitka.Controls.Add(btnStorno);
            pnlTlacitka.Controls.Add(btnUlozit);

            pnlMain.Controls.Add(lblApiKey, 0, 0);
            pnlMain.Controls.Add(txtApiKey, 0, 1);
            pnlMain.Controls.Add(chkZobrazitKlic, 0, 2);
            pnlMain.Controls.Add(lblModel, 0, 3);
            pnlMain.Controls.Add(cbModel, 0, 4);

            var wrapper = new Panel { Dock = DockStyle.Fill };
            wrapper.Controls.Add(pnlMain);
            wrapper.Controls.Add(pnlTlacitka);

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
                // Pokud model v seznamu chybí, přidáme jej jako volbu
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
    }
}
