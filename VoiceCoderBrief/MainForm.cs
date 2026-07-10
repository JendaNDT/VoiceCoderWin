using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VoiceCoderBrief.Core;

namespace VoiceCoderBrief
{
    public class MainForm : Form
    {
        // barvy podle vizuálu návrhu (tmavě modrá + teal)
        private static readonly Color Navy = Color.FromArgb(16, 35, 63);
        private static readonly Color Teal = Color.FromArgb(23, 176, 160);
        private static readonly Color SvetlePozadi = Color.FromArgb(246, 248, 250);

        private SpecProjekt _projekt = new SpecProjekt();
        private string _cestaSouboru = null;
        private bool _dirty = false;
        private bool _nacitani = false;   // potlačí eventy při programových změnách
        private string _napadSnapshot = "";

        // ovládací prvky
        private TextBox txtNazev;
        private TextBox txtNapad;
        private ListBox lstOtazky;
        private Label lblOtazka;
        private Label lblNapoveda;
        private TextBox txtOdpoved;
        private Button btnOdpovedet;
        private Button btnPredpoklad;
        private Label lblPostup;
        private RichTextBox rtbSpec;
        private ListView lvLog;
        private ToolStripStatusLabel lblStav;

        public MainForm()
        {
            Text = "VoiceCoder Brief – demonstrátor v0.1";
            ClientSize = new Size(1280, 800);
            MinimumSize = new Size(1100, 720);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.75f);
            BackColor = SvetlePozadi;
            AutoScaleMode = AutoScaleMode.Dpi;

            var split = PostavHlavniPlochu();
            var logBox = PostavLog();
            var tool = PostavToolbar();
            var status = PostavStatusBar();

            // pořadí přidání řídí docking: později přidané se dokují dřív
            Controls.Add(split);     // Fill
            Controls.Add(logBox);    // Bottom
            Controls.Add(tool);      // Top
            Controls.Add(status);    // Bottom (pod logem)

            FormClosing += (s, e) =>
            {
                if (!PotvrdNeulozene()) e.Cancel = true;
            };

            NovyProjekt(prvniSpusteni: true);
        }

        // ---------------- stavba UI ----------------

        private ToolStrip PostavToolbar()
        {
            var tool = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(8, 4, 8, 4),
                BackColor = Color.White
            };

            ToolStripButton Tlacitko(string text, EventHandler klik)
            {
                var b = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
                b.Click += klik;
                return b;
            }

            tool.Items.Add(Tlacitko("Nový", (s, e) => { if (PotvrdNeulozene()) NovyProjekt(false); }));
            tool.Items.Add(Tlacitko("Otevřít…", (s, e) => OtevritProjekt()));
            tool.Items.Add(Tlacitko("Uložit", (s, e) => UlozitProjekt()));
            tool.Items.Add(new ToolStripSeparator());
            tool.Items.Add(Tlacitko("Export Markdown…", (s, e) => Export(true)));
            tool.Items.Add(Tlacitko("Export JSON…", (s, e) => Export(false)));
            tool.Items.Add(new ToolStripSeparator());

            var tip = new ToolStripLabel("🎤 Diktování Windows: stiskni Win+H v libovolném poli")
            {
                ForeColor = Color.DimGray
            };
            tool.Items.Add(tip);

            return tool;
        }

        private StatusStrip PostavStatusBar()
        {
            var status = new StatusStrip();
            lblStav = new ToolStripStatusLabel("Připraveno.");
            status.Items.Add(lblStav);
            return status;
        }

        private GroupBox PostavLog()
        {
            var box = new GroupBox
            {
                Text = "Log rozhodnutí (každá změna má čas a důvod)",
                Dock = DockStyle.Bottom,
                Height = 150,
                Padding = new Padding(8),
                ForeColor = Navy
            };

            lvLog = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                ForeColor = Color.Black
            };
            lvLog.Columns.Add("Čas", 140);
            lvLog.Columns.Add("Akce", 140);
            lvLog.Columns.Add("Detail", 820);

            box.Controls.Add(lvLog);
            return box;
        }

        private SplitContainer PostavHlavniPlochu()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = 450,
                SplitterWidth = 6,
                BackColor = SvetlePozadi
            };

            // ----- LEVÝ PANEL: nápad + řízené otázky -----
            var levy = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10, 8, 4, 8),
                BackColor = SvetlePozadi
            };
            levy.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            levy.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            levy.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            levy.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            levy.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblNazev = Nadpis("Název projektu");
            txtNazev = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 8) };
            txtNazev.TextChanged += (s, e) =>
            {
                if (_nacitani) return;
                _projekt.Nazev = txtNazev.Text;
                _dirty = true;
                RenderSpecifikaci();
            };

            var lblNapad = Nadpis("Nápad (piš, nebo diktuj přes Win+H)");
            txtNapad = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 2, 0, 8)
            };
            txtNapad.TextChanged += (s, e) =>
            {
                if (_nacitani) return;
                _projekt.Napad = txtNapad.Text;
                _dirty = true;
                RenderSpecifikaci();
            };
            txtNapad.Enter += (s, e) => _napadSnapshot = _projekt.Napad ?? "";
            txtNapad.Leave += (s, e) =>
            {
                if (_nacitani) return;
                if ((_projekt.Napad ?? "") != _napadSnapshot)
                {
                    _projekt.Verze++;
                    _projekt.Upraveno = DateTime.Now;
                    _projekt.Log.Add(new Rozhodnuti { Cas = DateTime.Now, Akce = "Nápad", Detail = "Upraven text původního nápadu." });
                    ObnovLog();
                    ObnovStav();
                    RenderSpecifikaci();
                }
            };

            var otazkyBox = PostavOtazky();

            levy.Controls.Add(lblNazev, 0, 0);
            levy.Controls.Add(txtNazev, 0, 1);
            levy.Controls.Add(lblNapad, 0, 2);
            levy.Controls.Add(txtNapad, 0, 3);
            levy.Controls.Add(otazkyBox, 0, 4);

            split.Panel1.Controls.Add(levy);

            // ----- PRAVÝ PANEL: živá specifikace -----
            var hlavicka = new Label
            {
                Text = "Živá specifikace (náhled Markdown) – aktualizuje se po každém rozhodnutí",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0),
                ForeColor = Color.White,
                BackColor = Navy,
                Font = new Font("Segoe UI Semibold", 10f)
            };

            rtbSpec = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10.5f),
                WordWrap = true
            };

            split.Panel2.Controls.Add(rtbSpec);
            split.Panel2.Controls.Add(hlavicka);

            return split;
        }

        private GroupBox PostavOtazky()
        {
            var box = new GroupBox
            {
                Text = "Řízené otázky (nejdřív ty s největším dopadem)",
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ForeColor = Navy
            };

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = Color.Transparent
            };
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // seznam otázek
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // otázka
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // nápověda
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));   // odpověď
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // tlačítka
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // postup

            lstOtazky = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.Black,
                Margin = new Padding(0, 2, 0, 6)
            };
            lstOtazky.SelectedIndexChanged += (s, e) => UkazVybranouOtazku();

            lblOtazka = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Navy,
                Margin = new Padding(0, 4, 0, 2)
            };

            lblNapoveda = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                Margin = new Padding(0, 0, 0, 4)
            };

            txtOdpoved = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 2, 0, 4)
            };

            var tlacitka = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            btnOdpovedet = new Button
            {
                Text = "Uložit odpověď",
                AutoSize = true,
                BackColor = Teal,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(8, 2, 8, 2)
            };
            btnOdpovedet.FlatAppearance.BorderSize = 0;
            btnOdpovedet.Click += (s, e) => UlozOdpoved();

            btnPredpoklad = new Button
            {
                Text = "Nevím → použij předpoklad",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Navy,
                Padding = new Padding(8, 2, 8, 2)
            };
            btnPredpoklad.FlatAppearance.BorderColor = Teal;
            btnPredpoklad.Click += (s, e) => PouzitPredpoklad();

            tlacitka.Controls.Add(btnOdpovedet);
            tlacitka.Controls.Add(btnPredpoklad);

            lblPostup = new Label
            {
                AutoSize = true,
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 4, 0, 0)
            };

            tlp.Controls.Add(lstOtazky, 0, 0);
            tlp.Controls.Add(lblOtazka, 0, 1);
            tlp.Controls.Add(lblNapoveda, 0, 2);
            tlp.Controls.Add(txtOdpoved, 0, 3);
            tlp.Controls.Add(tlacitka, 0, 4);
            tlp.Controls.Add(lblPostup, 0, 5);

            box.Controls.Add(tlp);
            return box;
        }

        private static Label Nadpis(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10f),
            ForeColor = Navy,
            Margin = new Padding(0, 4, 0, 0)
        };

        // ---------------- logika UI ----------------

        private void NovyProjekt(bool prvniSpusteni)
        {
            _projekt = new SpecProjekt();
            _projekt.Log.Add(new Rozhodnuti { Cas = DateTime.Now, Akce = "Projekt", Detail = "Založen nový projekt." });
            _cestaSouboru = null;
            _dirty = false;

            _nacitani = true;
            txtNazev.Text = "";
            txtNapad.Text = "";
            txtOdpoved.Text = "";
            _nacitani = false;

            ObnovVse();
            VyberOtazku(SpecSluzba.DalsiNezodpovezena(_projekt));
            if (!prvniSpusteni) Stav("Nový projekt založen.");
        }

        private void OtevritProjekt()
        {
            if (!PotvrdNeulozene()) return;

            using var dlg = new OpenFileDialog
            {
                Title = "Otevřít projekt",
                Filter = "Projekt VoiceCoder Brief (*.vcbrief)|*.vcbrief|Všechny soubory (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                _projekt = SpecSluzba.NactiProjekt(dlg.FileName);
                _cestaSouboru = dlg.FileName;
                _dirty = false;

                _nacitani = true;
                txtNazev.Text = _projekt.Nazev ?? "";
                txtNapad.Text = _projekt.Napad ?? "";
                txtOdpoved.Text = "";
                _nacitani = false;

                ObnovVse();
                VyberOtazku(SpecSluzba.DalsiNezodpovezena(_projekt));
                Stav("Otevřeno: " + Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Soubor se nepodařilo načíst.\n\n" + ex.Message,
                    "Chyba při otevírání", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool UlozitProjekt()
        {
            if (_cestaSouboru == null)
            {
                using var dlg = new SaveFileDialog
                {
                    Title = "Uložit projekt",
                    Filter = "Projekt VoiceCoder Brief (*.vcbrief)|*.vcbrief",
                    FileName = BezpecnyNazevSouboru(_projekt.Nazev, "projekt") + ".vcbrief"
                };
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                _cestaSouboru = dlg.FileName;
            }

            try
            {
                SpecSluzba.UlozProjekt(_projekt, _cestaSouboru);
                _dirty = false;
                Stav("Uloženo: " + Path.GetFileName(_cestaSouboru));
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Projekt se nepodařilo uložit.\n\n" + ex.Message,
                    "Chyba při ukládání", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void Export(bool markdown)
        {
            using var dlg = new SaveFileDialog
            {
                Title = markdown ? "Export specifikace (Markdown)" : "Export specifikace (JSON)",
                Filter = markdown ? "Markdown (*.md)|*.md" : "JSON (*.json)|*.json",
                FileName = BezpecnyNazevSouboru(_projekt.Nazev, "specifikace") + (markdown ? ".md" : ".json")
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var obsah = markdown ? SpecSluzba.RenderMarkdown(_projekt) : SpecSluzba.RenderJson(_projekt);
                File.WriteAllText(dlg.FileName, obsah, new System.Text.UTF8Encoding(true));
                Stav("Export hotový: " + Path.GetFileName(dlg.FileName));
                MessageBox.Show(this, "Specifikace byla exportována:\n\n" + dlg.FileName,
                    "Export hotový", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export se nepodařil.\n\n" + ex.Message,
                    "Chyba exportu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UlozOdpoved()
        {
            var ot = VybranaOtazka();
            if (ot == null) return;

            if (string.IsNullOrWhiteSpace(txtOdpoved.Text))
            {
                MessageBox.Show(this, "Napiš odpověď, nebo zvol „Nevím → použij předpoklad“.",
                    "Chybí odpověď", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SpecSluzba.Odpovez(_projekt, ot.Id, txtOdpoved.Text);
            _dirty = true;
            ObnovVse();
            PrejdiNaDalsi(ot);
        }

        private void PouzitPredpoklad()
        {
            var ot = VybranaOtazka();
            if (ot == null) return;

            SpecSluzba.PouzijPredpoklad(_projekt, ot.Id);
            _dirty = true;
            ObnovVse();
            PrejdiNaDalsi(ot);
        }

        private void PrejdiNaDalsi(Otazka posledni)
        {
            var dalsi = SpecSluzba.DalsiNezodpovezena(_projekt);
            if (dalsi != null)
            {
                VyberOtazku(dalsi);
            }
            else
            {
                VyberOtazku(posledni);
                Stav("Všechny otázky vyřešené – specifikaci můžeš exportovat.");
            }
        }

        // ---------------- pomocné ----------------

        private Otazka VybranaOtazka()
        {
            int i = lstOtazky.SelectedIndex;
            if (i < 0 || i >= Otazky.Vse.Count) return null;
            return Otazky.Vse[i];
        }

        private void VyberOtazku(Otazka ot)
        {
            if (ot == null) return;
            for (int i = 0; i < Otazky.Vse.Count; i++)
            {
                if (Otazky.Vse[i].Id == ot.Id)
                {
                    lstOtazky.SelectedIndex = i;
                    return;
                }
            }
        }

        private void UkazVybranouOtazku()
        {
            var ot = VybranaOtazka();
            if (ot == null) return;

            lblOtazka.Text = ot.Text;
            lblNapoveda.Text = ot.Napoveda + "  (Když nevíš, předpoklad bude: „" + ot.VychoziPredpoklad + "“)";

            var odp = SpecSluzba.OdpovedNa(_projekt, ot.Id);
            _nacitani = true;
            txtOdpoved.Text = odp != null && !odp.JePredpoklad ? odp.Text : "";
            _nacitani = false;
        }

        private void ObnovVse()
        {
            ObnovSeznamOtazek();
            RenderSpecifikaci();
            ObnovLog();
            ObnovStav();
        }

        private void ObnovSeznamOtazek()
        {
            int vybrano = lstOtazky.SelectedIndex;
            _nacitani = true;
            lstOtazky.BeginUpdate();
            lstOtazky.Items.Clear();

            foreach (var ot in Otazky.Vse)
            {
                var odp = SpecSluzba.OdpovedNa(_projekt, ot.Id);
                string stav = odp == null ? "○" : (odp.JePredpoklad ? "≈" : "✔");
                string dopad = ot.Dopad == Dopad.Vysoky ? "V" : "S";
                lstOtazky.Items.Add(stav + " [" + dopad + "] " + ot.Text);
            }

            if (vybrano >= 0 && vybrano < lstOtazky.Items.Count)
                lstOtazky.SelectedIndex = vybrano;
            lstOtazky.EndUpdate();
            _nacitani = false;
        }

        private void RenderSpecifikaci()
        {
            rtbSpec.Text = SpecSluzba.RenderMarkdown(_projekt);
        }

        private void ObnovLog()
        {
            lvLog.BeginUpdate();
            lvLog.Items.Clear();
            // nejnovější nahoře
            foreach (var r in Enumerable.Reverse(_projekt.Log))
            {
                var it = new ListViewItem(r.Cas.ToString("d. M. yyyy H:mm:ss"));
                it.SubItems.Add(r.Akce);
                it.SubItems.Add(r.Detail);
                lvLog.Items.Add(it);
            }
            lvLog.EndUpdate();
        }

        private void ObnovStav()
        {
            int z = SpecSluzba.PocetZodpovezenych(_projekt);
            int p = SpecSluzba.PocetPredpokladu(_projekt);
            int otevrene = SpecSluzba.OtevreneOtazky(_projekt).Count;
            lblPostup.Text = "Zodpovězeno " + z + " · předpoklady " + p + " · otevřené " + otevrene + " (z " + Otazky.Vse.Count + ")";
            Stav("Verze specifikace " + _projekt.Verze + " · zodpovězeno " + z + "/" + Otazky.Vse.Count +
                 " · předpoklady " + p + " · otevřené otázky " + otevrene);
        }

        private void Stav(string text) => lblStav.Text = text;

        private bool PotvrdNeulozene()
        {
            if (!_dirty) return true;
            var res = MessageBox.Show(this,
                "Máš neuložené změny. Chceš je před pokračováním uložit?",
                "Neuložené změny", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (res == DialogResult.Cancel) return false;
            if (res == DialogResult.Yes) return UlozitProjekt();
            return true;
        }

        private static string BezpecnyNazevSouboru(string nazev, string vychozi)
        {
            if (string.IsNullOrWhiteSpace(nazev)) return vychozi;
            var neplatne = Path.GetInvalidFileNameChars();
            var s = new string(nazev.Trim().Select(c => neplatne.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(s) ? vychozi : s;
        }
    }
}
