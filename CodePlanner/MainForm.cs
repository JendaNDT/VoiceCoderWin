using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CodePlanner.Core;

namespace CodePlanner
{
    public class MainForm : Form
    {
        // barvy podle vizuálu návrhu (tmavě modrá + teal)
        private static readonly Color Navy = Color.FromArgb(16, 35, 63);
        private static readonly Color Teal = Color.FromArgb(23, 176, 160);
        private static readonly Color TealSvetla = Color.FromArgb(224, 244, 241);
        private static readonly Color SvetlePozadi = Color.FromArgb(246, 248, 250);
        private static readonly Color Zelena = Color.FromArgb(0, 150, 90);
        private static readonly Color Oranzova = Color.FromArgb(230, 140, 0);
        private static readonly Color SedaText = Color.FromArgb(105, 105, 105);

        private SpecProjekt _projekt = new SpecProjekt();
        private string _cestaSouboru = null;
        private bool _dirty = false;
        private bool _nacitani = false;   // potlačí eventy při programových změnách
        private string _napadSnapshot = "";

        // stav otázek pro vykreslení seznamu (plní ObnovSeznamOtazek)
        private readonly List<char> _stavyOtazek = new List<char>();     // '○' / '≈' / '✔'
        private readonly List<bool> _vysokyDopad = new List<bool>();
        private double _podilHotovo = 0;                                  // 0..1 pro progress bar

        // ovládací prvky
        private TextBox txtNazev;
        private ComboBox cmbTyp;
        private ToolStripSplitButton btnOtevritSplit;
        private TextBox txtNapad;
        private Button btnDiktovatNapad;
        private Button btnReferencie;
        private ContextMenuStrip menuReferencie;
        private ListBox lstOtazky;
        private Label lblOtazka;
        private Label lblNapoveda;
        private TextBox txtOdpoved;
        private Button btnOdpovedet;
        private Button btnPredpoklad;
        private Button btnDiktovatOdpoved;
        private Button btnAiAnalyza;
        private Label lblPostup;
        private Panel pnlPostup;
        private DateTime _casSpusteniDiktovani;
        private bool _diktovaniClickToggle = false;
        private RichTextBox rtbSpec;
        private Label lblSpecHlavicka;
        private Label lblNalezy;
        private List<Nalez> _nalezy = new List<Nalez>();
        private ListView lvLog;
        private ToolStripStatusLabel lblStav;
        private ToolTip _tipReference;
        private FlowLayoutPanel pnlQuickOptions;
        private RichTextBox rtbChatLog;
        private TextBox txtChatInput;
        private Button btnSendChat;
        private Button btnClearChat;
        private Button btnMockup;
        private ContextMenuStrip menuMockup;

        public MainForm()
        {
            SablonaSluzba.NactiCustomSablony();
            _tipReference = new ToolTip();
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            ClientSize = new Size(1280, 800);
            MinimumSize = new Size(1100, 720);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.75f);
            BackColor = SvetlePozadi;
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;

            var split = PostavHlavniPlochu();
            var logBox = PostavLog();
            var oddelovac = new Splitter
            {
                Dock = DockStyle.Bottom,
                Height = 6,
                BackColor = SvetlePozadi,
                MinExtra = 300,   // minimum pro hlavní plochu
                MinSize = 90      // minimum pro log
            };
            var tool = PostavToolbar();
            var status = PostavStatusBar();

            // pořadí přidání řídí docking: později přidané se dokují dřív
            Controls.Add(split);       // Fill
            Controls.Add(oddelovac);   // Bottom (nad logem, umožní měnit jeho výšku)
            Controls.Add(logBox);      // Bottom
            Controls.Add(tool);        // Top
            Controls.Add(status);      // Bottom (pod logem)

            FormClosing += (s, e) =>
            {
                if (!PotvrdNeulozene()) e.Cancel = true;
            };

            NovyProjekt(prvniSpusteni: true);
            ObnovNedavneMenu();
        }

        // ---------------- klávesové zkratky ----------------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.N:
                    if (PotvrdNeulozene()) NovyProjekt(false);
                    return true;
                case Keys.Control | Keys.O:
                    OtevritProjekt();
                    return true;
                case Keys.Control | Keys.S:
                    UlozitProjekt();
                    return true;
                case Keys.Control | Keys.M:
                    Export(true);
                    return true;
                case Keys.Control | Keys.J:
                    Export(false);
                    return true;
                case Keys.Control | Keys.P:
                    ExportujPdf();
                    return true;
                case Keys.Control | Keys.Enter:
                    if (txtOdpoved.Focused) { UlozOdpoved(); return true; }
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------------- stavba UI ----------------

        private ToolStrip PostavToolbar()
        {
            var tool = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(8, 5, 8, 5),
                BackColor = Color.White,
                RenderMode = ToolStripRenderMode.System
            };

            ToolStripButton Tlacitko(string text, string tip, EventHandler klik)
            {
                var b = new ToolStripButton(text)
                {
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                    ToolTipText = tip,
                    Padding = new Padding(4, 2, 4, 2)
                };
                b.Click += klik;
                return b;
            }

            tool.Items.Add(Tlacitko("🗒 Nový", "Založit nový projekt (Ctrl+N)", (s, e) => { if (PotvrdNeulozene()) NovyProjekt(false); }));
            
            btnOtevritSplit = new ToolStripSplitButton("📂 Otevřít…")
            {
                ToolTipText = "Otevřít uložený projekt (Ctrl+O)",
                Padding = new Padding(4, 2, 4, 2),
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            btnOtevritSplit.ButtonClick += (s, e) => OtevritProjekt();
            tool.Items.Add(btnOtevritSplit);

            tool.Items.Add(Tlacitko("💾 Uložit", "Uložit projekt (Ctrl+S)", (s, e) => UlozitProjekt()));
            tool.Items.Add(new ToolStripSeparator());
            tool.Items.Add(Tlacitko("⬇ Markdown…", "Export specifikace pro člověka (Ctrl+M)", (s, e) => Export(true)));
            tool.Items.Add(Tlacitko("⬇ JSON…", "Export specifikace pro kódovacího agenta (Ctrl+J)", (s, e) => Export(false)));
            tool.Items.Add(Tlacitko("📄 PDF…", "Export specifikace do PDF pro klienty (Ctrl+P)", (s, e) => ExportujPdf()));
            tool.Items.Add(Tlacitko("🌐 HTML Web…", "Export specifikace do interaktivního HTML webu", (s, e) => ExportujHtml()));
            tool.Items.Add(Tlacitko("💡 User Stories…", "Správa a generování uživatelských příběhů pro vývojáře", (s, e) => ZobrazUserStories()));
            tool.Items.Add(Tlacitko("📊 Metriky a Odhad…", "Projektové metriky a AI časový odhad", (s, e) => ZobrazMetriky()));
            tool.Items.Add(new ToolStripSeparator());
            tool.Items.Add(Tlacitko("⚙ Nastavení AI…", "Nastavení Gemini API klíče a modelu", (s, e) => OtevritNastaveni()));
            tool.Items.Add(new ToolStripSeparator());

            var tip2 = new ToolStripLabel("🎤 Diktování Windows: stiskni Win+H v libovolném poli")
            {
                ForeColor = SedaText
            };
            tool.Items.Add(tip2);

            var verze = new ToolStripLabel("v1.7")
            {
                Alignment = ToolStripItemAlignment.Right,
                ForeColor = Color.Silver
            };
            tool.Items.Add(verze);

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
                Text = "Log rozhodnutí (každá změna má čas a důvod) – výšku upravíš tažením horního okraje",
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
                ForeColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle
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

            var pnlNazevATypHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Height = 20
            };
            pnlNazevATypHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            pnlNazevATypHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            var lblNazev = Nadpis("Název projektu");
            var lblTyp = Nadpis("Typ / Šablona");
            pnlNazevATypHeader.Controls.Add(lblNazev, 0, 0);
            pnlNazevATypHeader.Controls.Add(lblTyp, 1, 0);

            var pnlNazevATyp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 8),
                Height = 32
            };
            pnlNazevATyp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            pnlNazevATyp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            txtNazev = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 4, 0), BorderStyle = BorderStyle.FixedSingle };
            txtNazev.TextChanged += (s, e) =>
            {
                if (_nacitani) return;
                _projekt.Nazev = txtNazev.Text;
                OznacZmenu();
                RenderSpecifikaci();
            };

            cmbTyp = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(4, 2, 0, 0)
            };
            ObnovTypyProjektuCombo();
            NastavTypCombo("Obecna");
            cmbTyp.SelectedIndexChanged += CmbTyp_SelectedIndexChanged;

            pnlNazevATyp.Controls.Add(txtNazev, 0, 0);
            pnlNazevATyp.Controls.Add(cmbTyp, 1, 0);

            var pnlNapadHeader = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Height = 28,
                Margin = new Padding(0)
            };
            var lblNapad = Nadpis("Nápad (piš, nebo diktuj přes Win+H)");
            btnAiAnalyza = new Button
            {
                Text = "🤖 Analyzovat přes Gemini",
                Height = 22,
                Margin = new Padding(12, 2, 0, 0),
                BackColor = Teal,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnAiAnalyza.FlatAppearance.BorderSize = 0;
            btnAiAnalyza.FlatAppearance.MouseOverBackColor = Color.FromArgb(19, 150, 137);
            btnAiAnalyza.Click += BtnAiAnalyza_Click;

            btnDiktovatNapad = new Button
            {
                Text = "🎤 Diktovat",
                Height = 22,
                Margin = new Padding(12, 2, 0, 0),
                BackColor = Color.Gainsboro,
                ForeColor = Navy,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnDiktovatNapad.FlatAppearance.BorderSize = 0;
            btnDiktovatNapad.FlatAppearance.MouseOverBackColor = Color.LightGray;
            btnDiktovatNapad.MouseDown += BtnDiktovat_MouseDown;
            btnDiktovatNapad.MouseUp += BtnDiktovat_MouseUp;
            btnDiktovatNapad.Click += BtnDiktovat_Click;

            btnReferencie = new Button
            {
                Text = "📎 Připojit podklad",
                Height = 22,
                Margin = new Padding(12, 2, 0, 0),
                BackColor = Color.Gainsboro,
                ForeColor = Navy,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnReferencie.FlatAppearance.BorderSize = 0;
            btnReferencie.FlatAppearance.MouseOverBackColor = Color.LightGray;
            btnReferencie.Click += BtnReferencie_Click;

            menuReferencie = new ContextMenuStrip();
            menuReferencie.Items.Add("Zobrazit obsah přílohy...", null, (s, e) => ZobrazitObsahReferenci());
            menuReferencie.Items.Add("Změnit soubor...", null, (s, e) => NahratReferenci());
            menuReferencie.Items.Add("Odebrat přílohu", null, (s, e) => OdebratReferenci());

            btnMockup = new Button
            {
                Text = "🖼 Připojit skicu",
                Height = 22,
                Margin = new Padding(12, 2, 0, 0),
                BackColor = Color.Gainsboro,
                ForeColor = Navy,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnMockup.FlatAppearance.BorderSize = 0;
            btnMockup.FlatAppearance.MouseOverBackColor = Color.LightGray;
            btnMockup.Click += BtnMockup_Click;

            menuMockup = new ContextMenuStrip();
            menuMockup.Items.Add("Zobrazit skicu...", null, (s, e) => ZobrazitMockup());
            menuMockup.Items.Add("Změnit skicu...", null, (s, e) => NahratMockup());
            menuMockup.Items.Add("Odebrat skicu", null, (s, e) => OdebratMockup());

            pnlNapadHeader.Controls.Add(lblNapad);
            pnlNapadHeader.Controls.Add(btnAiAnalyza);
            pnlNapadHeader.Controls.Add(btnDiktovatNapad);
            pnlNapadHeader.Controls.Add(btnReferencie);
            pnlNapadHeader.Controls.Add(btnMockup);

            txtNapad = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 2, 0, 8),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtNapad.TextChanged += (s, e) =>
            {
                if (_nacitani) return;
                _projekt.Napad = txtNapad.Text;
                OznacZmenu();
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

            levy.Controls.Add(pnlNazevATypHeader, 0, 0);
            levy.Controls.Add(pnlNazevATyp, 0, 1);
            levy.Controls.Add(pnlNapadHeader, 0, 2);
            levy.Controls.Add(txtNapad, 0, 3);
            levy.Controls.Add(otazkyBox, 0, 4);

            split.Panel1.Controls.Add(levy);

            // ----- PRAVÝ PANEL: TabControl (Specifikace vs AI Asistent) -----
            var tabRight = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f)
            };

            // TAB 1: Specifikace
            var tabSpecPage = new TabPage("📄 Specifikace projektu");
            tabSpecPage.BackColor = Color.White;
            
            var pnlSpecHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Navy
            };

            lblSpecHlavicka = new Label
            {
                Text = "Živá specifikace",
                Dock = DockStyle.Left,
                Width = 250,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                ForeColor = Color.White,
                BackColor = Navy,
                Font = new Font("Segoe UI Semibold", 10f)
            };

            var txtHledat = new TextBox
            {
                Width = 160,
                Height = 20,
                Location = new Point(tabRight.Width - 180, 6),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.Gray,
                Text = "Hledat..."
            };

            txtHledat.Enter += (s, e) =>
            {
                if (txtHledat.Text == "Hledat...")
                {
                    txtHledat.Text = "";
                    txtHledat.ForeColor = Color.Black;
                }
            };

            txtHledat.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtHledat.Text))
                {
                    txtHledat.Text = "Hledat...";
                    txtHledat.ForeColor = Color.Gray;
                }
            };

            txtHledat.TextChanged += (s, e) =>
            {
                if (txtHledat.Text != "Hledat...")
                {
                    HledatText(txtHledat.Text);
                }
            };

            pnlSpecHeader.Controls.Add(lblSpecHlavicka);
            pnlSpecHeader.Controls.Add(txtHledat);

            rtbSpec = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                WordWrap = true
            };

            lblNalezy = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Visible = false,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            lblNalezy.Click += (s, e) => ZobrazNalezy();

            tabSpecPage.Controls.Add(rtbSpec);
            tabSpecPage.Controls.Add(lblNalezy);
            tabSpecPage.Controls.Add(pnlSpecHeader);
            tabRight.TabPages.Add(tabSpecPage);

            // TAB 2: AI Asistent (Chat)
            var tabChatPage = new TabPage("💬 AI Asistent (Chat)");
            tabChatPage.BackColor = Color.White;
            
            var tlpChat = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.White
            };
            tlpChat.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // chat history log
            tlpChat.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));   // input panel

            rtbChatLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                WordWrap = true
            };

            var pnlChatInputArea = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(6)
            };

            txtChatInput = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f),
                Text = "Zeptej se na specifikaci projektu (např. 'Napiš SQL schémata', 'Navrhni refactoring')..."
            };
            txtChatInput.ForeColor = Color.Gray;

            txtChatInput.Enter += (s, e) =>
            {
                if (txtChatInput.ForeColor == Color.Gray)
                {
                    txtChatInput.Text = "";
                    txtChatInput.ForeColor = Color.Black;
                }
            };
            txtChatInput.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtChatInput.Text))
                {
                    txtChatInput.Text = "Zeptej se na specifikaci projektu (např. 'Napiš SQL schémata', 'Navrhni refactoring')...";
                    txtChatInput.ForeColor = Color.Gray;
                }
            };

            txtChatInput.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    OdeslatChat();
                }
            };

            btnSendChat = new Button
            {
                Text = "Odeslat",
                Width = 75,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Navy,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9f)
            };
            btnSendChat.FlatAppearance.BorderSize = 0;
            btnSendChat.Click += (s, e) => OdeslatChat();

            btnClearChat = new Button
            {
                Text = "Smazat",
                Width = 65,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Navy,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9f)
            };
            btnClearChat.FlatAppearance.BorderColor = Color.Silver;
            btnClearChat.Click += (s, e) => SmazatChat();

            var pnlChatButtons = new Panel
            {
                Width = 145,
                Dock = DockStyle.Right,
                Padding = new Padding(4, 0, 0, 0)
            };
            pnlChatButtons.Controls.Add(btnSendChat);
            pnlChatButtons.Controls.Add(btnClearChat);

            pnlChatInputArea.Controls.Add(txtChatInput);
            pnlChatInputArea.Controls.Add(pnlChatButtons);

            tlpChat.Controls.Add(rtbChatLog, 0, 0);
            tlpChat.Controls.Add(pnlChatInputArea, 0, 1);

            tabChatPage.Controls.Add(tlpChat);
            tabRight.TabPages.Add(tabChatPage);

            split.Panel2.Controls.Add(tabRight);

            tabRight.Resize += (s, e) =>
            {
                txtHledat.Location = new Point(tabRight.Width - 180, 6);
            };

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
                RowCount = 8,
                BackColor = Color.Transparent
            };
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // seznam otázek
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // otázka
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // nápověda
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));   // odpověď
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // AI rychlé nápovědy odpovědí (pnlQuickOptions)
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // tlačítka
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));   // progress bar
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // postup text

            pnlQuickOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 2, 0, 4),
                Height = 26,
                BackColor = Color.Transparent
            };

            lstOtazky = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.Black,
                Margin = new Padding(0, 2, 0, 6),
                DrawMode = DrawMode.OwnerDrawFixed,
                BorderStyle = BorderStyle.FixedSingle
            };
            lstOtazky.ItemHeight = (int)(lstOtazky.Font.Height * 2.2);
            lstOtazky.DrawItem += KresliOtazku;
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
                ForeColor = SedaText,
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                Margin = new Padding(0, 0, 0, 4)
            };

            txtOdpoved = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 2, 0, 4),
                BorderStyle = BorderStyle.FixedSingle
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
                Text = "Uložit odpověď  (Ctrl+Enter)",
                AutoSize = true,
                BackColor = Teal,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(8, 2, 8, 2),
                Cursor = Cursors.Hand
            };
            btnOdpovedet.FlatAppearance.BorderSize = 0;
            btnOdpovedet.FlatAppearance.MouseOverBackColor = Color.FromArgb(19, 150, 137);
            btnOdpovedet.Click += (s, e) => UlozOdpoved();

            btnPredpoklad = new Button
            {
                Text = "Nevím → použij předpoklad",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Navy,
                Padding = new Padding(8, 2, 8, 2),
                Cursor = Cursors.Hand
            };
            btnPredpoklad.FlatAppearance.BorderColor = Teal;
            btnPredpoklad.FlatAppearance.MouseOverBackColor = TealSvetla;
            btnPredpoklad.Click += (s, e) => PouzitPredpoklad();

            btnDiktovatOdpoved = new Button
            {
                Text = "🎤 Diktovat (držet)",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Navy,
                Padding = new Padding(8, 2, 8, 2),
                Cursor = Cursors.Hand
            };
            btnDiktovatOdpoved.FlatAppearance.BorderColor = Teal;
            btnDiktovatOdpoved.FlatAppearance.MouseOverBackColor = TealSvetla;
            btnDiktovatOdpoved.MouseDown += BtnDiktovat_MouseDown;
            btnDiktovatOdpoved.MouseUp += BtnDiktovat_MouseUp;
            btnDiktovatOdpoved.Click += BtnDiktovat_Click;

            tlacitka.Controls.Add(btnOdpovedet);
            tlacitka.Controls.Add(btnPredpoklad);
            tlacitka.Controls.Add(btnDiktovatOdpoved);

            pnlPostup = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 0),
                BackColor = Color.Transparent
            };
            pnlPostup.Paint += KresliPostup;
            pnlPostup.Resize += (s, e) => pnlPostup.Invalidate();

            lblPostup = new Label
            {
                AutoSize = true,
                ForeColor = SedaText,
                Margin = new Padding(0, 2, 0, 0)
            };

            tlp.Controls.Add(lstOtazky, 0, 0);
            tlp.Controls.Add(lblOtazka, 0, 1);
            tlp.Controls.Add(lblNapoveda, 0, 2);
            tlp.Controls.Add(txtOdpoved, 0, 3);
            tlp.Controls.Add(pnlQuickOptions, 0, 4);
            tlp.Controls.Add(tlacitka, 0, 5);
            tlp.Controls.Add(pnlPostup, 0, 6);
            tlp.Controls.Add(lblPostup, 0, 7);

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

        // ---------------- vlastní vykreslování ----------------

        /// <summary>Řádek seznamu otázek: barevný stav, štítek dopadu, text s výpustkou.</summary>
        private void KresliOtazku(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstOtazky.Items.Count) return;

            bool vybrano = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var pozadi = vybrano ? TealSvetla : Color.White;
            using (var b = new SolidBrush(pozadi)) e.Graphics.FillRectangle(b, e.Bounds);
            if (vybrano)
                using (var b = new SolidBrush(Teal))
                    e.Graphics.FillRectangle(b, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);

            // Spodní jemný oddělovač řádku
            using (var p = new Pen(Color.FromArgb(240, 240, 240)))
                e.Graphics.DrawLine(p, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            char stav = e.Index < _stavyOtazek.Count ? _stavyOtazek[e.Index] : '○';
            bool vysoky = e.Index < _vysokyDopad.Count && _vysokyDopad[e.Index];

            // 1. Badge stavu (grafický kruh)
            var badgeRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + (e.Bounds.Height - 14) / 2, 14, 14);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (stav == '✔')
            {
                using (var b = new SolidBrush(Zelena))
                {
                    e.Graphics.FillEllipse(b, badgeRect);
                }
                using (var f = new Font("Segoe UI", 8f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(e.Graphics, "✓", f, badgeRect, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }
            else if (stav == '≈')
            {
                using (var b = new SolidBrush(Oranzova))
                {
                    e.Graphics.FillEllipse(b, badgeRect);
                }
                using (var f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(e.Graphics, "P", f, badgeRect, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }
            else // '○'
            {
                using (var p = new Pen(Color.Silver, 1.5f))
                {
                    e.Graphics.DrawEllipse(p, badgeRect);
                }
            }

            // 2. Štítek dopadu (pill tag se zaoblenými rohy)
            var chip = new Rectangle(e.Bounds.X + 28, e.Bounds.Y + (e.Bounds.Height - 16) / 2, 20, 16);
            using (var b = new SolidBrush(vysoky ? Navy : Color.Gainsboro))
            using (var path = Zaobli(chip, 3))
                e.Graphics.FillPath(b, path);

            using (var f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, vysoky ? "V" : "S", f, chip,
                    vysoky ? Color.White : Color.DimGray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 3. Text otázky
            var textRect = new Rectangle(e.Bounds.X + 54, e.Bounds.Y, e.Bounds.Width - 58, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, lstOtazky.Items[e.Index].ToString(), lstOtazky.Font,
                textRect, Color.Black,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        /// <summary>Progress bar postupu: teal výplň na světlé dráze, zaoblené rohy.</summary>
        private void KresliPostup(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = pnlPostup.ClientRectangle;
            r.Inflate(-1, -2);
            if (r.Width <= 0 || r.Height <= 0) return;

            using (var draha = Zaobli(r, r.Height / 2))
            using (var b = new SolidBrush(Color.FromArgb(225, 230, 234)))
                g.FillPath(b, draha);

            int w = (int)(r.Width * Math.Max(0, Math.Min(1, _podilHotovo)));
            if (w > r.Height)   // aby zaoblení nedegenerovalo
            {
                var fill = new Rectangle(r.X, r.Y, w, r.Height);
                using (var cesta = Zaobli(fill, r.Height / 2))
                using (var b = new SolidBrush(Teal))
                    g.FillPath(b, cesta);
            }
        }

        private static GraphicsPath Zaobli(Rectangle r, int polomer)
        {
            var p = new GraphicsPath();
            int d = polomer * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ---------------- formátovaný náhled specifikace ----------------

        /// <summary>Převede markdown z RenderMarkdown na jednoduché RTF (nadpisy, odrážky, citace, tučné, zvýrazněné předpoklady).</summary>
        private static string MarkdownNaRtf(string md)
        {
            var sb = new StringBuilder();
            // fonty a barvy: 1=Navy 2=Teal 3=šedá 4=oranžová(předpoklad) 5=text
            sb.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}");
            sb.Append(@"{\colortbl ;\red16\green35\blue63;\red23\green176\blue160;\red105\green105\blue105;\red217\green119\blue6;\red33\green37\blue41;}");
            sb.Append(@"\f0\fs20 ");

            foreach (var syrovy in md.Replace("\r\n", "\n").Split('\n'))
            {
                string radek = syrovy.TrimEnd();

                if (radek.StartsWith("# "))
                {
                    sb.Append(@"{\pard\sb40\sa80\b\cf1\fs32 ");
                    PridejInline(sb, radek.Substring(2));
                    sb.Append(@"\par}");
                }
                else if (radek.StartsWith("## "))
                {
                    sb.Append(@"{\pard\sb160\sa50\b\cf1\fs25 ");
                    PridejInline(sb, radek.Substring(3));
                    sb.Append(@"\par}");
                }
                else if (radek.StartsWith("> "))
                {
                    sb.Append(@"{\pard\li280\cf3\i ");
                    PridejRtfText(sb, radek.Substring(2));
                    sb.Append(@"\i0\par}");
                }
                else if (radek.StartsWith("- "))
                {
                    sb.Append(@"{\pard\li300\fi-160\sa20\cf2\b ");
                    PridejRtfText(sb, "•  ");
                    sb.Append(@"\b0\cf5 ");
                    PridejInline(sb, radek.Substring(2));
                    sb.Append(@"\par}");
                }
                else if (radek.StartsWith("  ") && radek.Trim().Length > 0)
                {
                    sb.Append(@"{\pard\li300\sa20\cf5 ");
                    PridejInline(sb, radek.Trim());
                    sb.Append(@"\par}");
                }
                else if (radek.Length == 0)
                {
                    sb.Append(@"{\pard\fs10\par}");
                }
                else
                {
                    sb.Append(@"{\pard\cf5 ");
                    PridejInline(sb, radek);
                    sb.Append(@"\par}");
                }
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Zpracuje **tučné**, *kurzívu* a zvýrazní [PŘEDPOKLAD] oranžově.</summary>
        private static void PridejInline(StringBuilder sb, string text)
        {
            foreach (var kus in Regex.Split(text, @"(\*\*.*?\*\*|\*.*?\*)"))
            {
                if (kus.Length == 0) continue;

                if (kus.Length >= 4 && kus.StartsWith("**") && kus.EndsWith("**"))
                {
                    string vnitrek = kus.Substring(2, kus.Length - 4);
                    if (vnitrek.Contains("[PŘEDPOKLAD]"))
                    {
                        sb.Append(@"{\b\cf4 ");
                        PridejRtfText(sb, vnitrek);
                        sb.Append('}');
                    }
                    else
                    {
                        sb.Append(@"{\b ");
                        PridejRtfText(sb, vnitrek);
                        sb.Append('}');
                    }
                }
                else if (kus.Length >= 2 && kus.StartsWith("*") && kus.EndsWith("*"))
                {
                    sb.Append(@"{\i\cf3 ");
                    PridejRtfText(sb, kus.Substring(1, kus.Length - 2));
                    sb.Append('}');
                }
                else
                {
                    PridejRtfText(sb, kus);
                }
            }
        }

        /// <summary>RTF escapování včetně českých znaků (\uNNNN?).</summary>
        private static void PridejRtfText(StringBuilder sb, string text)
        {
            foreach (char c in text)
            {
                if (c == '\\' || c == '{' || c == '}') { sb.Append('\\').Append(c); }
                else if (c > 127)
                {
                    int v = c;
                    if (v > 32767) v -= 65536;   // RTF \u je signed 16-bit (emoji, surrogáty)
                    sb.Append(@"\u").Append(v).Append('?');
                }
                else sb.Append(c);
            }
        }

        // ---------------- logika UI ----------------

        private void CmbTyp_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_nacitani) return;
            if (!(cmbTyp.SelectedItem is ComboItemTypProjektu vybrany)) return;
            if (string.Equals(_projekt.TypProjektuKlic, vybrany.Klic, StringComparison.OrdinalIgnoreCase)) return;

            SpecSluzba.ZmenTypProjektu(_projekt, vybrany.Klic);
            OznacZmenu();
            ObnovVse();
            UkazVybranouOtazku();
        }

        private void NovyProjekt(bool prvniSpusteni)
        {
            _projekt = new SpecProjekt();
            _projekt.Log.Add(new Rozhodnuti { Cas = DateTime.Now, Akce = "Projekt", Detail = "Založen nový projekt." });
            _cestaSouboru = null;
            _dirty = false;

            _nacitani = true;
            txtNazev.Text = "";
            txtNapad.Text = "";
            NastavTypCombo("Obecna");
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
                Filter = "Projekt CodePlanner (*.vcbrief)|*.vcbrief|Všechny soubory (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            OtevritProjektCestu(dlg.FileName);
        }

        private void OtevritProjektCestu(string cesta)
        {
            if (!File.Exists(cesta))
            {
                MessageBox.Show(this, $"Soubor nebyl nalezen:\n\n{cesta}\n\nBude odebrán ze seznamu nedávných.",
                    "Soubor nenalezen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                var nast = GeminiNastaveni.Nacti();
                nast.OdeberNedavnyProjekt(cesta);
                ObnovNedavneMenu();
                return;
            }

            try
            {
                _projekt = SpecSluzba.NactiProjekt(cesta);
                _cestaSouboru = cesta;
                _dirty = false;

                _nacitani = true;
                txtNazev.Text = _projekt.Nazev ?? "";
                txtNapad.Text = _projekt.Napad ?? "";
                NastavTypCombo(_projekt.TypProjektuKlic);
                txtOdpoved.Text = "";
                _nacitani = false;

                ObnovVse();
                VyberOtazku(SpecSluzba.DalsiNezodpovezena(_projekt));
                Stav("Otevřeno: " + Path.GetFileName(cesta));

                var nast = GeminiNastaveni.Nacti();
                nast.PridejNedavnyProjekt(cesta);
                ObnovNedavneMenu();
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
                    Filter = "Projekt CodePlanner (*.vcbrief)|*.vcbrief",
                    FileName = BezpecnyNazevSouboru(_projekt.Nazev, "projekt") + ".vcbrief"
                };
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                _cestaSouboru = dlg.FileName;
            }

            try
            {
                SpecSluzba.UlozProjekt(_projekt, _cestaSouboru);
                _dirty = false;
                ObnovTitulek();
                Stav("Uloženo: " + Path.GetFileName(_cestaSouboru));

                var nast = GeminiNastaveni.Nacti();
                nast.PridejNedavnyProjekt(_cestaSouboru);
                ObnovNedavneMenu();

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

        private void ExportujHtml()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Export specifikace (Interaktivní HTML Web)",
                Filter = "HTML soubory (*.html;*.htm)|*.html;*.htm",
                FileName = BezpecnyNazevSouboru(_projekt.Nazev, "specifikace") + ".html"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var obsah = SpecSluzba.RenderHtml(_projekt);
                File.WriteAllText(dlg.FileName, obsah, Encoding.UTF8);
                Stav("HTML export hotový: " + Path.GetFileName(dlg.FileName));
                
                var res = MessageBox.Show(this,
                    "Interaktivní specifikace byla exportována do HTML.\n\nChceš vytvořený web ihned otevřít v prohlížeči?",
                    "Export hotový", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (res == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dlg.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export se nepodařil.\n\n" + ex.Message,
                    "Chyba exportu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportujPdf()
        {
            using (var dlg = new SaveFileDialog
            {
                Title = "Exportovat specifikaci do PDF",
                Filter = "PDF soubory (*.pdf)|*.pdf",
                FileName = BezpecnyNazevSouboru(_projekt.Nazev, "specifikace") + ".pdf"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                Stav("Exportuji do PDF...");
                try
                {
                    var exp = new PdfExporter(_projekt);
                    exp.Export(this, dlg.FileName);

                    Stav("Export do PDF dokončen.");
                    var res = MessageBox.Show(this,
                        "Specifikace byla úspěšně exportována do PDF.\n\nChceš vytvořený soubor ihned otevřít?",
                        "Export dokončen", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (res == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = dlg.FileName,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Při exportu do PDF došlo k chybě:\n\n" + ex.Message,
                        "Chyba exportu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
            }
        }

        private void ZobrazUserStories()
        {
            var nastaveni = GeminiNastaveni.Nacti();
            using (var dlg = new UserStoriesForm(_projekt.UserStories, nastaveni.EfektivniApiKey, nastaveni.GeminiModel, _projekt, () => OznacZmenu()))
            {
                dlg.ShowDialog(this);
            }
            ObnovVse();
        }

        private void ZobrazMetriky()
        {
            var nastaveni = GeminiNastaveni.Nacti();
            using (var dlg = new MetrikyForm(_projekt.Metriky, nastaveni.EfektivniApiKey, nastaveni.GeminiModel, _projekt, () => OznacZmenu()))
            {
                dlg.ShowDialog(this);
            }
            ObnovVse();
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
            OznacZmenu();
            ObnovVse();
            PrejdiNaDalsi(ot);
        }

        private void PouzitPredpoklad()
        {
            var ot = VybranaOtazka();
            if (ot == null) return;

            SpecSluzba.PouzijPredpoklad(_projekt, ot.Id);
            OznacZmenu();
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
            var otazky = SpecSluzba.VratOtazkyProjektu(_projekt).ToList();
            if (i < 0 || i >= otazky.Count) return null;
            return otazky[i];
        }

        private void VyberOtazku(Otazka ot)
        {
            if (ot == null) return;
            var otazky = SpecSluzba.VratOtazkyProjektu(_projekt).ToList();
            for (int i = 0; i < otazky.Count; i++)
            {
                if (otazky[i].Id == ot.Id)
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

            lblOtazka.Text = ot.GetText(_projekt.TypProjektuKlic);
            lblNapoveda.Text = ot.GetNapoveda(_projekt.TypProjektuKlic) + "  (Když nevíš, předpoklad bude: „" + ot.GetVychoziPredpoklad(_projekt.TypProjektuKlic) + "“)";

            var odp = SpecSluzba.OdpovedNa(_projekt, ot.Id);
            _nacitani = true;
            txtOdpoved.Text = odp != null && !odp.JePredpoklad ? odp.Text : "";
            _nacitani = false;

            pnlQuickOptions.Controls.Clear();
            var moznosti = ot.GetMoznosti(_projekt.TypProjektuKlic);
            if (moznosti != null && moznosti.Count > 0)
            {
                var lblTip = new Label
                {
                    Text = "Tip:",
                    AutoSize = true,
                    ForeColor = SedaText,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Margin = new Padding(0, 4, 4, 0)
                };
                pnlQuickOptions.Controls.Add(lblTip);

                foreach (var m in moznosti)
                {
                    var btnVolba = new Button
                    {
                        Text = m,
                        AutoSize = true,
                        BackColor = TealSvetla,
                        ForeColor = Navy,
                        FlatStyle = FlatStyle.Flat,
                        Cursor = Cursors.Hand,
                        Font = new Font("Segoe UI", 8f),
                        Margin = new Padding(2, 0, 2, 0),
                        Padding = new Padding(4, 1, 4, 1)
                    };
                    btnVolba.FlatAppearance.BorderSize = 0;
                    btnVolba.Click += (s, e) =>
                    {
                        txtOdpoved.Text = m;
                        txtOdpoved.Focus();
                        UlozOdpoved();
                    };
                    pnlQuickOptions.Controls.Add(btnVolba);
                }
            }
        }

        private void ObnovVse()
        {
            ObnovSeznamOtazek();
            RenderSpecifikaci();
            ObnovLog();
            ObnovStav();
            ObnovTitulek();
            ObnovTlacitkoReference();
            ObnovTlacitkoMockupu();
            VykresliHistoriiChatu();
        }

        private void ObnovSeznamOtazek()
        {
            int vybrano = lstOtazky.SelectedIndex;
            _nacitani = true;
            lstOtazky.BeginUpdate();
            lstOtazky.Items.Clear();
            _stavyOtazek.Clear();
            _vysokyDopad.Clear();

            var otazky = SpecSluzba.VratOtazkyProjektu(_projekt).ToList();
            foreach (var ot in otazky)
            {
                var odp = SpecSluzba.OdpovedNa(_projekt, ot.Id);
                _stavyOtazek.Add(odp == null ? '○' : (odp.JePredpoklad ? '≈' : '✔'));
                _vysokyDopad.Add(ot.Dopad == Dopad.Vysoky);
                lstOtazky.Items.Add(ot.GetText(_projekt.TypProjektuKlic));
            }

            if (vybrano >= 0 && vybrano < lstOtazky.Items.Count)
                lstOtazky.SelectedIndex = vybrano;
            lstOtazky.EndUpdate();
            _nacitani = false;
        }

        private void RenderSpecifikaci()
        {
            string md = SpecSluzba.RenderMarkdown(_projekt);
            try
            {
                rtbSpec.Rtf = MarkdownNaRtf(md);
            }
            catch
            {
                rtbSpec.Text = md;   // nouzový režim: syrový markdown
            }
            int celkem = SpecSluzba.VratOtazkyProjektu(_projekt).Count();
            lblSpecHlavicka.Text = "Živá specifikace · verze " + _projekt.Verze +
                " · zodpovězeno " + (SpecSluzba.PocetZodpovezenych(_projekt) + SpecSluzba.PocetPredpokladu(_projekt)) +
                "/" + celkem;
            ObnovNalezy();
        }

        private void HledatText(string dotaz)
        {
            // Resetujeme formátování na původní stav
            RenderSpecifikaci();

            if (string.IsNullOrWhiteSpace(dotaz) || dotaz.Length < 2) return;

            int start = 0;
            while (start < rtbSpec.TextLength)
            {
                int index = rtbSpec.Find(dotaz, start, RichTextBoxFinds.None);
                if (index == -1) break;

                rtbSpec.Select(index, dotaz.Length);
                rtbSpec.SelectionBackColor = Color.Yellow;
                rtbSpec.SelectionColor = Color.Black;

                start = index + dotaz.Length;
            }
            rtbSpec.SelectionLength = 0; // zrušíme výběr
        }

        private void ObnovNalezy()
        {
            _nalezy = KonzistencniKontrola.Zkontroluj(_projekt);
            if (_nalezy.Count == 0)
            {
                lblNalezy.Visible = false;
                return;
            }

            int rozpory = _nalezy.Count(n => n.Zavaznost == Zavaznost.Rozpor);
            int varovani = _nalezy.Count - rozpory;
            var casti = new List<string>();
            if (rozpory > 0) casti.Add(Mnozne(rozpory, "rozpor", "rozpory", "rozporů"));
            if (varovani > 0) casti.Add(Mnozne(varovani, "varování", "varování", "varování"));

            lblNalezy.Text = (rozpory > 0 ? "❗ " : "⚠️ ") + "Kontrola konzistence: " +
                string.Join(" a ", casti) + " – klikni pro detail";
            lblNalezy.BackColor = rozpory > 0 ? Color.FromArgb(253, 232, 232) : Color.FromArgb(255, 244, 219);
            lblNalezy.ForeColor = rozpory > 0 ? Color.FromArgb(155, 28, 28) : Color.FromArgb(146, 90, 4);
            lblNalezy.Visible = true;
        }

        private void ZobrazNalezy()
        {
            if (_nalezy.Count == 0) return;
            var nastaveni = GeminiNastaveni.Nacti();
            using (var dlg = new NalezyForm(_nalezy, nastaveni.EfektivniApiKey, nastaveni.GeminiModel, _projekt))
            {
                dlg.ShowDialog(this);
            }
        }

        private static string Mnozne(int n, string jedna, string dvaAzCtyri, string vice)
            => n == 1 ? "1 " + jedna : (n >= 2 && n <= 4 ? n + " " + dvaAzCtyri : n + " " + vice);

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
            int celkem = SpecSluzba.VratOtazkyProjektu(_projekt).Count();
            _podilHotovo = celkem > 0 ? (z + p) / (double)celkem : 0;
            pnlPostup.Invalidate();
            lblPostup.Text = "Zodpovězeno " + z + " · předpoklady " + p + " · otevřené " + otevrene + " (z " + celkem + ")";
            Stav("Verze specifikace " + _projekt.Verze + " · zodpovězeno " + z + "/" + celkem +
                 " · předpoklady " + p + " · otevřené otázky " + otevrene);
        }

        private void OznacZmenu()
        {
            _dirty = true;
            ObnovTitulek();
        }

        private void ObnovTitulek()
        {
            string nazev = string.IsNullOrWhiteSpace(_projekt.Nazev) ? "nový projekt" : _projekt.Nazev.Trim();
            Text = "CodePlanner – " + nazev + (_dirty ? " *" : "") + " – v1.2";
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

        private void OtevritNastaveni()
        {
            using var dlg = new SettingsForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                Stav("Nastavení Gemini API uloženo.");
            }
        }

        private async void BtnAiAnalyza_Click(object sender, EventArgs e)
        {
            var nastaveni = GeminiNastaveni.Nacti();
            string apiKey = nastaveni.EfektivniApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show(this,
                    "Není nastaven API klíč pro Gemini.\nOtevřete prosím Nastavení AI a zadejte klíč.",
                    "Chybí API klíč", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                OtevritNastaveni();
                return;
            }

            string napad = txtNapad.Text.Trim();
            if (string.IsNullOrWhiteSpace(napad))
            {
                MessageBox.Show(this,
                    "Zadejte nejprve původní nápad, který chcete analyzovat.",
                    "Prázdný nápad", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtNapad.Focus();
                return;
            }

            if (_projekt.Odpovedi.Count > 0)
            {
                var confirm = MessageBox.Show(this,
                    "Tato akce analyzuje nápad pomocí Gemini API a přepíše všechny stávající odpovědi specifikace.\n\nChcete pokračovat?",
                    "Přepsat odpovědi?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;
            }

            NastavitStavBusy(true, "Komunikuji s Gemini API...");

            try
            {
                string model = nastaveni.GeminiModel;
                string mockupMime = (_projekt.MockupNazev != null && (_projekt.MockupNazev.EndsWith(".jpg") || _projekt.MockupNazev.EndsWith(".jpeg"))) ? "image/jpeg" : "image/png";
                var vysledek = await GeminiService.AnalyzujNapadAsync(apiKey, model, napad, _projekt.TypProjektuKlic, _projekt.ReferencniText, _projekt.MockupBase64, mockupMime);

                _nacitani = true;
                _projekt.Nazev = vysledek.Nazev ?? "";
                txtNazev.Text = _projekt.Nazev;
                _projekt.Otazky.Clear();
                _projekt.Odpovedi.Clear();

                foreach (var ot in vysledek.Otazky)
                {
                    var dopadEnum = string.Equals(ot.Dopad, "Vysoky", StringComparison.OrdinalIgnoreCase) ? Dopad.Vysoky : Dopad.Stredni;
                    _projekt.Otazky.Add(new Otazka
                    {
                        Id = ot.Id,
                        Sekce = ot.Sekce,
                        Dopad = dopadEnum,
                        Text = ot.Text,
                        Napoveda = ot.Napoveda,
                        VychoziPredpoklad = ot.VychoziPredpoklad,
                        Moznosti = ot.Moznosti ?? new List<string>()
                    });

                    _projekt.Odpovedi.Add(new Odpoved
                    {
                        OtazkaId = ot.Id,
                        Text = ot.Odpoved ?? "",
                        JePredpoklad = ot.JePredpoklad,
                        Cas = DateTime.Now
                    });
                }

                _nacitani = false;

                _projekt.Verze++;
                _projekt.Upraveno = DateTime.Now;
                _projekt.Log.Add(new Rozhodnuti
                {
                    Cas = DateTime.Now,
                    Akce = "AI Analýza",
                    Detail = $"Specifikace vygenerována pomocí Gemini API (model: {model})."
                });

                OznacZmenu();
                ObnovVse();
                VyberOtazku(SpecSluzba.DalsiNezodpovezena(_projekt));

                MessageBox.Show(this, "Specifikace byla úspěšně vygenerována pomocí AI.",
                    "Analýza dokončena", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Během analýzy nápadu došlo k chybě:\n\n{ex.Message}",
                    "Chyba AI analýzy", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                NastavitStavBusy(false, "Připraveno.");
            }
        }

        private void NastavitStavBusy(bool busy, string textStavu)
        {
            Stav(textStavu);
            btnAiAnalyza.Enabled = !busy;
            btnAiAnalyza.Text = busy ? "⏳ Analyzuji..." : "🤖 Analyzovat přes Gemini";

            txtNazev.Enabled = !busy;
            txtNapad.Enabled = !busy;
            lstOtazky.Enabled = !busy;
            txtOdpoved.Enabled = !busy;
            btnOdpovedet.Enabled = !busy;
            btnPredpoklad.Enabled = !busy;

            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void BtnDiktovat_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            var b = (Button)sender;
            
            // pokud již nahráváme v ClickToggle režimu, tak tímto stiskem nahrávání ukončujeme
            if (_diktovaniClickToggle)
            {
                _diktovaniClickToggle = false;
                ZastavADiktuj(b);
                return;
            }

            // zkontrolujeme API klíč dřív, než začneme nahrávat
            var nastaveni = GeminiNastaveni.Nacti();
            if (string.IsNullOrWhiteSpace(nastaveni.EfektivniApiKey))
            {
                MessageBox.Show(this,
                    "Není nastaven API klíč pro Gemini.\nPro diktování je nutné mít nastaven klíč v Nastavení AI.",
                    "Chybí API klíč", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                OtevritNastaveni();
                return;
            }

            // spustíme nahrávání
            _casSpusteniDiktovani = DateTime.Now;
            try
            {
                HlasovyVstup.SpustNahravani();
                b.BackColor = Color.Crimson;
                b.ForeColor = Color.White;
                b.Text = "🎤 Nahrávám...";
                Stav("Diktování spuštěno. Držte tlačítko nebo klikněte znovu pro stop.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Nepodařilo se spustit nahrávání z mikrofonu:\n\n" + ex.Message,
                    "Chyba nahrávání", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDiktovat_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (_diktovaniClickToggle) return; // v toggle režimu nereagujeme na uvolnění

            var b = (Button)sender;
            double ms = (DateTime.Now - _casSpusteniDiktovani).TotalMilliseconds;

            if (ms < 400)
            {
                // stisk byl příliš rychlý -> přepneme do Toggle-to-Talk
                _diktovaniClickToggle = true;
                b.Text = "🎤 Nahrávám (klikni stop)";
                Stav("Režim klikni-a-mluv spuštěn. Nahrávám... Klikněte na tlačítko pro ukončení.");
            }
            else
            {
                // hold-to-talk: uvolněno, stopujeme a přepisujeme
                ZastavADiktuj(b);
            }
        }

        private void BtnDiktovat_Click(object sender, EventArgs e)
        {
            // Click se spouští po MouseUp. Vše obsloužíme v MouseDown a MouseUp.
        }

        private async void ZastavADiktuj(Button b)
        {
            Stav("Zastavuji nahrávání...");
            string cestaWav = HlasovyVstup.ZastavNahravani();
            
            // obnovíme výchozí vzhled tlačítka
            if (b == btnDiktovatNapad)
            {
                b.BackColor = Color.Gainsboro;
                b.ForeColor = Navy;
                b.Text = "🎤 Diktovat";
            }
            else
            {
                b.BackColor = Color.White;
                b.ForeColor = Navy;
                b.Text = "🎤 Diktovat (držet)";
            }

            if (string.IsNullOrEmpty(cestaWav))
            {
                Stav("Diktování zrušeno nebo se nepodařilo uložit nahrávku.");
                return;
            }

            // zkontrolujeme, zda nahrávka trvala aspoň chvíli
            double delkaSekund = 0;
            try
            {
                if (File.Exists(cestaWav))
                {
                    var info = new FileInfo(cestaWav);
                    // 16kHz 16-bit mono wav = 32000 bajtů za sekundu (+ ~44 bajtů hlavička)
                    delkaSekund = (info.Length - 44) / 32000.0;
                }
            }
            catch { }

            if (delkaSekund < 0.4)
            {
                Stav("Nahrávka byla příliš krátká.");
                return;
            }

            TextBox cil = b == btnDiktovatNapad ? txtNapad : txtOdpoved;
            b.Enabled = false;
            b.Text = "⏳ Přepisuji...";
            Stav("Komunikuji s Gemini API (přepis hlasu)...");

            try
            {
                var nastaveni = GeminiNastaveni.Nacti();
                string model = nastaveni.GeminiModel;
                string apiKey = nastaveni.EfektivniApiKey;

                string prepis = await GeminiService.PrepisAudioAsync(apiKey, model, cestaWav);
                
                if (string.IsNullOrWhiteSpace(prepis))
                {
                    Stav("Nebylo rozpoznáno žádné slovo.");
                }
                else
                {
                    VlozTextNaKurzor(cil, prepis);
                    Stav("Hlas úspěšně přepsán.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Při přepisu hlasu došlo k chybě:\n\n" + ex.Message,
                    "Chyba přepisu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Stav("Chyba při přepisu.");
            }
            finally
            {
                b.Enabled = true;
                b.Text = b == btnDiktovatNapad ? "🎤 Diktovat" : "🎤 Diktovat (držet)";
                
                // smažeme dočasný soubor
                try { if (File.Exists(cestaWav)) File.Delete(cestaWav); } catch { }
            }
        }

        private static void VlozTextNaKurzor(TextBox tb, string novyText)
        {
            if (string.IsNullOrWhiteSpace(novyText)) return;
            
            tb.Focus();
            int index = tb.SelectionStart;
            string staryText = tb.Text ?? "";
            
            // přidáme mezery podle kontextu
            string vkladany = novyText.Trim();
            if (index > 0 && !char.IsWhiteSpace(staryText[index - 1]))
            {
                vkladany = " " + vkladany;
            }
            if (index < staryText.Length && !char.IsWhiteSpace(staryText[index]))
            {
                vkladany = vkladany + " ";
            }

            tb.SelectedText = vkladany;
            tb.SelectionStart = index + vkladany.Length;
            tb.SelectionLength = 0;
        }

        private void ObnovTlacitkoReference()
        {
            if (btnReferencie == null) return;

            if (string.IsNullOrWhiteSpace(_projekt.ReferencniText))
            {
                btnReferencie.Text = "📎 Připojit podklad";
                btnReferencie.BackColor = Color.Gainsboro;
                btnReferencie.ForeColor = Navy;
                _tipReference.SetToolTip(btnReferencie, "Připojit textový soubor (TXT, MD, JSON) jako referenční podklad pro AI analýzu.");
            }
            else
            {
                string zkracenyNazev = _projekt.ReferencniNazev ?? "příloha.txt";
                if (zkracenyNazev.Length > 20)
                {
                    zkracenyNazev = zkracenyNazev.Substring(0, 17) + "...";
                }
                btnReferencie.Text = "📎 " + zkracenyNazev;
                btnReferencie.BackColor = TealSvetla;
                btnReferencie.ForeColor = Navy;
                _tipReference.SetToolTip(btnReferencie, $"Připojen soubor: {_projekt.ReferencniNazev}\nObsah: {(_projekt.ReferencniText.Length > 100 ? _projekt.ReferencniText.Substring(0, 100) + "..." : _projekt.ReferencniText)}\n\nKliknutím zobrazíte možnosti.");
            }
        }

        private void BtnReferencie_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_projekt.ReferencniText))
            {
                NahratReferenci();
            }
            else
            {
                menuReferencie.Show(btnReferencie, new Point(0, btnReferencie.Height));
            }
        }

        private void NahratReferenci()
        {
            using var dlg = new OpenFileDialog();
            dlg.Filter = "Podporované textové soubory (*.txt;*.md;*.json;*.html)|*.txt;*.md;*.json;*.html|Všechny soubory (*.*)|*.*";
            dlg.Title = "Vyberte soubor s referenčními podklady";

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    string text = File.ReadAllText(dlg.FileName);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        MessageBox.Show(this, "Vybraný soubor je prázdný.", "Prázdný soubor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _projekt.ReferencniText = text;
                    _projekt.ReferencniNazev = Path.GetFileName(dlg.FileName);
                    _projekt.Log.Add(new Rozhodnuti 
                    { 
                        Cas = DateTime.Now, 
                        Akce = "Příloha", 
                        Detail = $"Připojen referenční soubor {_projekt.ReferencniNazev}." 
                    });

                    OznacZmenu();
                    ObnovVse();
                    Stav($"Připojen soubor: {_projekt.ReferencniNazev}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Chyba při čtení souboru:\n\n" + ex.Message, "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ZobrazitObsahReferenci()
        {
            if (string.IsNullOrWhiteSpace(_projekt.ReferencniText)) return;

            using var dlg = new Form
            {
                Text = $"Obsah přílohy: {_projekt.ReferencniNazev}",
                Size = new Size(600, 500),
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = true,
                ShowIcon = false
            };

            var txt = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Text = _projekt.ReferencniText,
                Font = new Font("Consolas", 10f)
            };

            dlg.Controls.Add(txt);
            dlg.ShowDialog(this);
        }

        private void OdebratReferenci()
        {
            if (string.IsNullOrWhiteSpace(_projekt.ReferencniText)) return;

            string nazev = _projekt.ReferencniNazev;
            _projekt.ReferencniText = null;
            _projekt.ReferencniNazev = null;
            
            _projekt.Log.Add(new Rozhodnuti 
            { 
                Cas = DateTime.Now, 
                Akce = "Příloha", 
                Detail = $"Odebrán referenční soubor {nazev}." 
            });

            OznacZmenu();
            ObnovVse();
            Stav("Referenční soubor odebrán.");
        }

        private void ObnovTlacitkoMockupu()
        {
            if (btnMockup == null) return;

            if (string.IsNullOrWhiteSpace(_projekt.MockupBase64))
            {
                btnMockup.Text = "🖼 Připojit skicu";
                btnMockup.BackColor = Color.Gainsboro;
                btnMockup.ForeColor = Navy;
                if (_tipReference != null)
                {
                    _tipReference.SetToolTip(btnMockup, "Připojit obrázek, screenshot nebo diagram (PNG/JPG) jako vizuální kontext pro AI analýzu.");
                }
            }
            else
            {
                string zkracenyNazev = _projekt.MockupNazev ?? "skica.png";
                if (zkracenyNazev.Length > 20)
                {
                    zkracenyNazev = zkracenyNazev.Substring(0, 17) + "...";
                }
                btnMockup.Text = "🖼 " + zkracenyNazev;
                btnMockup.BackColor = TealSvetla;
                btnMockup.ForeColor = Navy;
                if (_tipReference != null)
                {
                    _tipReference.SetToolTip(btnMockup, $"Připojen vizuální mockup: {_projekt.MockupNazev}\n\nKliknutím zobrazíte možnosti.");
                }
            }
        }

        private void BtnMockup_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_projekt.MockupBase64))
            {
                NahratMockup();
            }
            else
            {
                menuMockup.Show(btnMockup, new Point(0, btnMockup.Height));
            }
        }

        private void NahratMockup()
        {
            using var dlg = new OpenFileDialog();
            dlg.Filter = "Obrázky (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Všechny soubory (*.*)|*.*";
            dlg.Title = "Vyberte soubor s nákresem rozhraní (mockup)";

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(dlg.FileName);
                    if (bytes.Length == 0)
                    {
                        MessageBox.Show(this, "Vybraný soubor je prázdný.", "Prázdný soubor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (bytes.Length > 4 * 1024 * 1024)
                    {
                        MessageBox.Show(this, "Vybraný soubor je příliš velký (maximum je 4 MB).", "Velký soubor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _projekt.MockupBase64 = Convert.ToBase64String(bytes);
                    _projekt.MockupNazev = Path.GetFileName(dlg.FileName);
                    _projekt.Log.Add(new Rozhodnuti 
                    { 
                        Cas = DateTime.Now, 
                        Akce = "Skica", 
                        Detail = $"Připojen vizuální mockup {_projekt.MockupNazev}." 
                    });

                    OznacZmenu();
                    ObnovVse();
                    Stav($"Připojen vizuální mockup: {_projekt.MockupNazev}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Chyba při čtení souboru:\n\n" + ex.Message, "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ZobrazitMockup()
        {
            if (string.IsNullOrWhiteSpace(_projekt.MockupBase64)) return;

            try
            {
                byte[] bytes = Convert.FromBase64String(_projekt.MockupBase64);
                using var ms = new MemoryStream(bytes);
                var img = Image.FromStream(ms);

                var dlg = new Form
                {
                    Text = $"Prohlížeč skici: {_projekt.MockupNazev}",
                    Size = new Size(800, 600),
                    StartPosition = FormStartPosition.CenterParent,
                    MinimizeBox = false,
                    MaximizeBox = true,
                    ShowIcon = false
                };

                var pb = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = img,
                    SizeMode = PictureBoxSizeMode.Zoom
                };

                dlg.Controls.Add(pb);
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Nepodařilo se zobrazit obrázek:\n\n" + ex.Message, "Chyba zobrazení", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OdebratMockup()
        {
            if (string.IsNullOrWhiteSpace(_projekt.MockupBase64)) return;

            string nazev = _projekt.MockupNazev;
            _projekt.MockupBase64 = null;
            _projekt.MockupNazev = null;
            
            _projekt.Log.Add(new Rozhodnuti 
            { 
                Cas = DateTime.Now, 
                Akce = "Skica", 
                Detail = $"Odebrán vizuální mockup {nazev}." 
            });

            OznacZmenu();
            ObnovVse();
            Stav("Vizuální mockup odebrán.");
        }

        private void ObnovNedavneMenu()
        {
            if (btnOtevritSplit == null) return;

            btnOtevritSplit.DropDownItems.Clear();

            var nastaveni = GeminiNastaveni.Nacti();
            if (nastaveni.NedavneProjekty == null || nastaveni.NedavneProjekty.Count == 0)
            {
                var emptyItem = new ToolStripMenuItem("Žádné nedávné projekty") { Enabled = false };
                btnOtevritSplit.DropDownItems.Add(emptyItem);
                return;
            }

            foreach (var cesta in nastaveni.NedavneProjekty)
            {
                if (string.IsNullOrWhiteSpace(cesta)) continue;

                string nazevSouboru = Path.GetFileName(cesta);
                var item = new ToolStripMenuItem(nazevSouboru)
                {
                    ToolTipText = cesta,
                    Tag = cesta
                };
                item.Click += (s, e) =>
                {
                    var menuIt = (ToolStripMenuItem)s;
                    string path = menuIt.Tag.ToString();
                    OtevritProjektCestu(path);
                };
                btnOtevritSplit.DropDownItems.Add(item);
            }

            btnOtevritSplit.DropDownItems.Add(new ToolStripSeparator());
            var clearItem = new ToolStripMenuItem("Vymazat historii nedávných...", null, (s, e) => 
            {
                var confirm = MessageBox.Show(this, "Opravdu chcete vymazat historii nedávných projektů?", "Vymazat historii", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    var nast = GeminiNastaveni.Nacti();
                    nast.NedavneProjekty.Clear();
                    nast.Uloz();
                    ObnovNedavneMenu();
                }
            });
            btnOtevritSplit.DropDownItems.Add(clearItem);
        }

        private void ObnovTypyProjektuCombo()
        {
            _nacitani = true;
            cmbTyp.Items.Clear();

            // Přidáme built-in typy
            cmbTyp.Items.Add(new ComboItemTypProjektu { Klic = "Obecna", Nazev = "Obecná aplikace" });
            cmbTyp.Items.Add(new ComboItemTypProjektu { Klic = "Hra", Nazev = "Hra (Game)" });
            cmbTyp.Items.Add(new ComboItemTypProjektu { Klic = "Evidence", Nazev = "Evidence / Registr" });
            cmbTyp.Items.Add(new ComboItemTypProjektu { Klic = "Nastroj", Nazev = "Nástroj / Utilita" });

            // Přidáme custom šablony
            foreach (var sab in SablonaSluzba.CustomSablony)
            {
                cmbTyp.Items.Add(new ComboItemTypProjektu { Klic = sab.Klic, Nazev = sab.Nazev });
            }

            _nacitani = false;
        }

        private void NastavTypCombo(string typKlic)
        {
            _nacitani = true;
            for (int i = 0; i < cmbTyp.Items.Count; i++)
            {
                if (cmbTyp.Items[i] is ComboItemTypProjektu item && string.Equals(item.Klic, typKlic, StringComparison.OrdinalIgnoreCase))
                {
                    cmbTyp.SelectedIndex = i;
                    _nacitani = false;
                    return;
                }
            }
            cmbTyp.SelectedIndex = 0;
            _nacitani = false;
        }

        private void VykresliHistoriiChatu()
        {
            if (rtbChatLog == null) return;

            rtbChatLog.Clear();
            if (_projekt.ChatHistory == null)
            {
                _projekt.ChatHistory = new List<ChatMessage>();
            }

            if (_projekt.ChatHistory.Count == 0)
            {
                rtbChatLog.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Italic);
                rtbChatLog.SelectionColor = Color.Gray;
                rtbChatLog.AppendText("Zatím zde nejsou žádné zprávy. Zeptej se na cokoliv ohledně aktuální specifikace!\n\n");
                return;
            }

            foreach (var msg in _projekt.ChatHistory)
            {
                bool isUser = string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase);
                rtbChatLog.SelectionFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
                rtbChatLog.SelectionColor = isUser ? Navy : Teal;
                rtbChatLog.AppendText(isUser ? "Já: " : "Asistent: ");

                rtbChatLog.SelectionFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
                rtbChatLog.SelectionColor = Color.Black;
                rtbChatLog.AppendText(msg.Text + "\n\n");
            }

            rtbChatLog.SelectionStart = rtbChatLog.Text.Length;
            rtbChatLog.ScrollToCaret();
        }

        private async void OdeslatChat()
        {
            if (txtChatInput.ForeColor == Color.Gray || string.IsNullOrWhiteSpace(txtChatInput.Text))
            {
                return;
            }

            string text = txtChatInput.Text.Trim();
            txtChatInput.Clear();

            if (_projekt.ChatHistory == null)
            {
                _projekt.ChatHistory = new List<ChatMessage>();
            }

            var uzivatelZprava = new ChatMessage { Role = "user", Text = text, Cas = DateTime.Now };
            _projekt.ChatHistory.Add(uzivatelZprava);
            OznacZmenu();
            VykresliHistoriiChatu();

            Cursor = Cursors.WaitCursor;
            btnSendChat.Enabled = false;
            btnClearChat.Enabled = false;
            Stav("AI asistent odpovídá...");

            try
            {
                var nastaveni = GeminiNastaveni.Nacti();
                string odpoved = await GeminiService.PosliChatZpravuAsync(nastaveni.EfektivniApiKey, nastaveni.GeminiModel, _projekt, _projekt.ChatHistory);

                var modelZprava = new ChatMessage { Role = "model", Text = odpoved, Cas = DateTime.Now };
                _projekt.ChatHistory.Add(modelZprava);
                OznacZmenu();
                VykresliHistoriiChatu();
                Stav("AI asistent odpověděl.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Chyba při komunikaci s AI asistentem:\n\n" + ex.Message, "Chyba AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Stav("Komunikace selhala.");
            }
            finally
            {
                btnSendChat.Enabled = true;
                btnClearChat.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void SmazatChat()
        {
            if (_projekt.ChatHistory == null || _projekt.ChatHistory.Count == 0) return;

            var confirm = MessageBox.Show(this, "Opravdu chcete smazat celou historii chatu s asistentem?", "Smazat chat", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                _projekt.ChatHistory.Clear();
                OznacZmenu();
                VykresliHistoriiChatu();
                Stav("Chat smazán.");
            }
        }
    }

    public class ComboItemTypProjektu
    {
        public string Klic { get; set; } = "";
        public string Nazev { get; set; } = "";
        public override string ToString() => Nazev;
    }

    public class NalezyForm : Form
    {
        private readonly List<Nalez> _offlineNalezy;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly SpecProjekt _projekt;
        private ListView lvNalezy;
        private Button btnAiCheck;
        private Label lblStatus;

        public NalezyForm(List<Nalez> offlineNalezy, string apiKey, string model, SpecProjekt projekt)
        {
            _offlineNalezy = offlineNalezy;
            _apiKey = apiKey;
            _model = model;
            _projekt = projekt;

            Text = "Kontrola konzistence specifikace";
            Size = new Size(750, 480);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(500, 350);
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9.5f);

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
            btnAiCheck.Enabled = false;
            btnAiCheck.Text = "Analyzuji specifikaci přes Gemini...";
            lblStatus.Text = "Volám Gemini API pro hloubkovou kontrolu, chvíli strpení...";
            lblStatus.ForeColor = Color.FromArgb(16, 35, 63);

            try
            {
                var aiNalezy = await GeminiService.AnalyzujKonzistenciAsync(_apiKey, _model, _projekt);
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
                MessageBox.Show(this, "AI analýza selhala:\n\n" + ex.Message, "Chyba AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Během AI analýzy došlo k chybě.";
                lblStatus.ForeColor = Color.Red;
            }
            finally
            {
                btnAiCheck.Text = "🧠 Spustit hloubkovou AI analýzu";
                btnAiCheck.Enabled = !string.IsNullOrWhiteSpace(_apiKey);
            }
        }
    }

    public class PdfExporter
    {
        private readonly SpecProjekt _projekt;
        private readonly List<string> _radky;
        private int _aktualniRadek = 0;
        private int _strana = 0;

        public PdfExporter(SpecProjekt projekt)
        {
            _projekt = projekt;
            string md = SpecSluzba.RenderMarkdown(projekt);
            _radky = md.Replace("\r\n", "\n").Split('\n').ToList();
        }

        public void Export(IWin32Window parent, string pdfPath)
        {
            var pd = new PrintDocument();
            
            // Vybereme PDF tiskárnu
            string tiskarna = PrinterSettings.InstalledPrinters.Cast<string>()
                .FirstOrDefault(p => p.Contains("Print to PDF") || p.Contains("PDF"));
            if (tiskarna != null)
            {
                pd.PrinterSettings.PrinterName = tiskarna;
                pd.PrinterSettings.PrintToFile = true;
                pd.PrinterSettings.PrintFileName = pdfPath;
            }
            else
            {
                using (var dlg = new PrintDialog { Document = pd })
                {
                    if (dlg.ShowDialog(parent) != DialogResult.OK) return;
                }
            }

            pd.PrintPage += Pd_PrintPage;
            _aktualniRadek = 0;
            _strana = 0;
            pd.Print();
        }

        private void Pd_PrintPage(object sender, PrintPageEventArgs e)
        {
            var g = e.Graphics;
            _strana++;

            // Margins
            float marginL = e.MarginBounds.Left;
            float marginT = e.MarginBounds.Top;
            float width = e.MarginBounds.Width;
            float height = e.MarginBounds.Height;
            float marginR = e.MarginBounds.Right;
            float marginB = e.MarginBounds.Bottom;

            // Barvy
            var navy = Color.FromArgb(16, 35, 63);
            var teal = Color.FromArgb(23, 176, 160);
            var oranzova = Color.FromArgb(230, 140, 0);

            if (_strana == 1)
            {
                // Vykreslíme titulní stranu
                g.Clear(Color.White);

                // Levý dekorační panel (navy)
                using (var b = new SolidBrush(navy))
                {
                    g.FillRectangle(b, 0, 0, 60, e.PageBounds.Height);
                }
                // Levý tenký proužek (teal)
                using (var b = new SolidBrush(teal))
                {
                    g.FillRectangle(b, 60, 0, 6, e.PageBounds.Height);
                }

                float startX = 100;
                float startY = 200;

                using (var fTag = new Font("Segoe UI", 12f, FontStyle.Bold))
                using (var bTeal = new SolidBrush(teal))
                {
                    g.DrawString("SPECIFIKACE PROJEKTU", fTag, bTeal, startX, startY);
                }

                startY += 30;
                string nazev = string.IsNullOrWhiteSpace(_projekt.Nazev) ? "Nový projekt" : _projekt.Nazev.Trim();
                using (var fTitle = new Font("Segoe UI", 26f, FontStyle.Bold))
                using (var bNavy = new SolidBrush(navy))
                {
                    // Zabalíme název, kdyby byl moc dlouhý
                    VykresliOdstavec(g, nazev, fTitle, bNavy, startX, startY, e.PageBounds.Width - startX - 50, 6);
                }

                startY += 100;
                using (var fSub = new Font("Segoe UI", 12f, FontStyle.Italic))
                using (var bGray = new SolidBrush(Color.Gray))
                {
                    g.DrawString("Strukturovaný technický brief a analýza požadavků", fSub, bGray, startX, startY);
                }

                startY = e.PageBounds.Height - 220;
                using (var fLabel = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (var fVal = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                using (var bNavy = new SolidBrush(navy))
                using (var bGray = new SolidBrush(Color.DimGray))
                {
                    g.DrawString("Verze specifikace:", fLabel, bNavy, startX, startY);
                    g.DrawString(_projekt.Verze.ToString(), fVal, bGray, startX + 130, startY);

                    g.DrawString("Datum vygenerování:", fLabel, bNavy, startX, startY + 22);
                    g.DrawString(DateTime.Now.ToString("d. M. yyyy"), fVal, bGray, startX + 130, startY + 22);

                    g.DrawString("Typ / Šablona:", fLabel, bNavy, startX, startY + 44);
                    g.DrawString(SpecSluzba.VratNazevTypu(_projekt.TypProjektuKlic), fVal, bGray, startX + 130, startY + 44);

                    g.DrawString("Nástroj:", fLabel, bNavy, startX, startY + 66);
                    g.DrawString("CodePlanner (AI-Powered)", fVal, bGray, startX + 130, startY + 66);
                }

                e.HasMorePages = true;
                return;
            }

            // Následující stránky s obsahem
            g.Clear(Color.White);

            // Záhlaví
            using (var fHead = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (var bGray = new SolidBrush(Color.Gray))
            using (var pen = new Pen(Color.FromArgb(220, 224, 230), 0.75f))
            {
                string headText = $"Specifikace projektu: {(_projekt.Nazev ?? "nový projekt")}";
                g.DrawString(headText, fHead, bGray, marginL, marginT - 25);
                g.DrawLine(pen, marginL, marginT - 12, marginR, marginT - 12);
            }

            // Zápatí
            using (var fFoot = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (var bGray = new SolidBrush(Color.Gray))
            {
                string footText = $"Strana {_strana}";
                var size = g.MeasureString(footText, fFoot);
                g.DrawString(footText, fFoot, bGray, marginL + (width - size.Width) / 2, marginB + 15);
            }

            float currentY = marginT;
            using (var fH2 = new Font("Segoe UI Semibold", 13f, FontStyle.Bold))
            using (var fH3 = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold))
            using (var fText = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (var bNavy = new SolidBrush(navy))
            using (var bBlack = new SolidBrush(Color.FromArgb(33, 37, 41)))
            {
                while (_aktualniRadek < _radky.Count)
                {
                    string radekRaw = _radky[_aktualniRadek];
                    string radek = radekRaw.TrimEnd();

                    // Měříme výšku podle typu řádku
                    float h = 0;
                    if (radek.StartsWith("## "))
                    {
                        h = ZmerVyskuOdstavce(g, radek.Substring(3), fH2, width) + 16;
                    }
                    else if (radek.StartsWith("### "))
                    {
                        h = ZmerVyskuOdstavce(g, radek.Substring(4), fH3, width) + 8;
                    }
                    else if (radek.StartsWith("- "))
                    {
                        h = ZmerVyskuOdstavce(g, radek.Substring(2), fText, width - 18) + 4;
                    }
                    else if (radek.StartsWith("> "))
                    {
                        h = ZmerVyskuOdstavce(g, radek.Substring(2), fText, width - 20) + 12;
                    }
                    else if (radek.Length == 0)
                    {
                        h = 10;
                    }
                    else
                    {
                        h = ZmerVyskuOdstavce(g, radek, fText, width) + 4;
                    }

                    // Pokud řádek přeteče stránku, odložíme ho na další stránku
                    if (currentY + h > marginB && currentY > marginT)
                    {
                        e.HasMorePages = true;
                        return;
                    }

                    // Vykreslíme řádek
                    if (radek.StartsWith("## "))
                    {
                        currentY += 10; // extra top margin
                        VykresliOdstavec(g, radek.Substring(3), fH2, bNavy, marginL, currentY, width, 4);
                        currentY += h - 10;
                    }
                    else if (radek.StartsWith("### "))
                    {
                        currentY += 4;
                        VykresliOdstavec(g, radek.Substring(4), fH3, bNavy, marginL, currentY, width, 3);
                        currentY += h - 4;
                    }
                    else if (radek.StartsWith("- "))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var bTeal = new SolidBrush(teal))
                        {
                            g.FillEllipse(bTeal, marginL + 4, currentY + (fText.Height - 5) / 2, 5, 5);
                        }
                        VykresliOdstavec(g, radek.Substring(2), fText, bBlack, marginL + 18, currentY, width - 18, 3);
                        currentY += h;
                    }
                    else if (radek.StartsWith("> "))
                    {
                        string vnitrek = radek.Substring(2);
                        float vyskaOdst = ZmerVyskuOdstavce(g, vnitrek, fText, width - 20);
                        using (var pozadi = new SolidBrush(Color.FromArgb(245, 247, 250)))
                        {
                            g.FillRectangle(pozadi, marginL, currentY, width, vyskaOdst + 8);
                        }
                        using (var linka = new SolidBrush(oranzova))
                        {
                            g.FillRectangle(linka, marginL, currentY, 3, vyskaOdst + 8);
                        }
                        using (var fItalic = new Font(fText.FontFamily, fText.Size, FontStyle.Italic))
                        using (var bGray = new SolidBrush(Color.FromArgb(60, 60, 60)))
                        {
                            VykresliOdstavec(g, vnitrek, fItalic, bGray, marginL + 12, currentY + 4, width - 20, 3);
                        }
                        currentY += h;
                    }
                    else if (radek.Length == 0)
                    {
                        currentY += h;
                    }
                    else
                    {
                        VykresliOdstavec(g, radek, fText, bBlack, marginL, currentY, width, 3);
                        currentY += h;
                    }

                    _aktualniRadek++;
                }
            }

            e.HasMorePages = false;
        }

        private static List<string> ZabalText(Graphics g, string text, Font font, float maxSirka)
        {
            var lines = new List<string>();
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return new List<string> { "" };

            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                string testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
                var size = g.MeasureString(testLine, font);
                if (size.Width > maxSirka && currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear().Append(word);
                }
                else
                {
                    currentLine.Append(currentLine.Length == 0 ? word : " " + word);
                }
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
            }

            return lines;
        }

        private static float ZmerVyskuOdstavce(Graphics g, string text, Font font, float maxSirka, float radekSpacing = 3)
        {
            float y = 0;
            foreach (var radek in text.Split('\n'))
            {
                var zabalene = ZabalText(g, radek, font, maxSirka);
                y += zabalene.Count * (font.Height + radekSpacing);
            }
            return y;
        }

        private static float VykresliOdstavec(Graphics g, string text, Font font, Brush brush, float x, float y, float maxSirka, float radekSpacing = 3)
        {
            float startY = y;
            foreach (var radek in text.Split('\n'))
            {
                var zabalene = ZabalText(g, radek, font, maxSirka);
                foreach (var z in zabalene)
                {
                    g.DrawString(z, font, brush, x, y);
                    y += font.Height + radekSpacing;
                }
            }
            return y - startY;
        }
    }

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

        public UserStoriesForm(List<UserStory> stories, string apiKey, string model, SpecProjekt projekt, Action onZmena)
        {
            _stories = stories;
            _apiKey = apiKey;
            _model = model;
            _projekt = projekt;
            _onZmena = onZmena;

            Text = "Uživatelské příběhy (User Stories)";
            Size = new Size(850, 580);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowIcon = false;
            Font = new Font("Segoe UI", 9.5f);

            PostavUI();
            NaplnStories();
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

            // Vykreslíme detail s barvami
            rtbDetail.SelectionFont = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
            rtbDetail.SelectionColor = Color.FromArgb(16, 35, 63);
            rtbDetail.AppendText($"{s.Id}: {s.Titulek}\n\n");

            rtbDetail.SelectionFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            rtbDetail.SelectionColor = Color.DimGray;
            rtbDetail.AppendText("Priorita: ");
            rtbDetail.SelectionFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            rtbDetail.SelectionColor = s.Priorita == "Vysoká" ? Color.Red : (s.Priorita == "Střední" ? Color.DarkGoldenrod : Color.Green);
            rtbDetail.AppendText($"{s.Priorita}\n\n");

            rtbDetail.SelectionFont = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
            rtbDetail.SelectionColor = Color.FromArgb(16, 35, 63);
            rtbDetail.AppendText("Uživatelský příběh (User Story)\n");
            
            rtbDetail.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Italic);
            rtbDetail.SelectionColor = Color.FromArgb(50, 50, 50);
            rtbDetail.AppendText($"> {s.Popis}\n\n");

            rtbDetail.SelectionFont = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
            rtbDetail.SelectionColor = Color.FromArgb(16, 35, 63);
            rtbDetail.AppendText("Akceptační kritéria (Acceptance Criteria)\n");

            rtbDetail.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Regular);
            rtbDetail.SelectionColor = Color.Black;
            foreach (var k in s.Kriteria)
            {
                rtbDetail.AppendText($"• {k}\n");
            }
        }

        private async void BtnAiStories_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            btnAiStories.Enabled = false;
            btnAiStories.Text = "🤖 Generuji Stories...";
            lblStatus.Text = "Volám Gemini API, chvíli strpení...";

            try
            {
                var noveStories = await GeminiService.GenerujUserStoriesAsync(_apiKey, _model, _projekt);
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
                MessageBox.Show(this, "Chyba při generování User Stories:\n\n" + ex.Message, "Chyba AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Generování selhalo.";
            }
            finally
            {
                btnAiStories.Text = "🤖 Generovat přes Gemini";
                btnAiStories.Enabled = !string.IsNullOrWhiteSpace(_apiKey);
                Cursor = Cursors.Default;
            }
        }

        private void BtnExportMd_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog
            {
                Title = "Export User Stories do Markdown",
                Filter = "Markdown (*.md)|*.md",
                FileName = $"user_stories_{(_projekt.Nazev != null ? _projekt.Nazev.Replace(" ", "_") : "projekt")}.md"
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
                FileName = $"user_stories_{(_projekt.Nazev != null ? _projekt.Nazev.Replace(" ", "_") : "projekt")}.csv"
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
            if (text.Contains("\"") || text.Contains(",") || text.Contains("\n") || text.Contains("\r"))
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }
            return text;
        }
    }

    public class MetrikyForm : Form
    {
        private readonly ProjektMetriky _metriky;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly SpecProjekt _projekt;
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

        public MetrikyForm(ProjektMetriky metriky, string apiKey, string model, SpecProjekt projekt, Action onZmena)
        {
            _metriky = metriky;
            _apiKey = apiKey;
            _model = model;
            _projekt = projekt;
            _onZmena = onZmena;

            Text = "Metriky a odhad projektu";
            Size = new Size(820, 580);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(600, 400);
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = true;
            Font = new Font("Segoe UI", 9.5f);

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
                BackColor = Color.FromArgb(16, 35, 63) // Navy
            };
            var lblTitle = new Label
            {
                Text = "📊 AI Odhad a projektové metriky",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
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
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); // Doba vývoje card
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); // Komplexita card
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); // Doporučený rozpočet card
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); // Složení týmu card
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Empty space

            // Card Builder
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
                    ForeColor = Color.DimGray,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Dock = DockStyle.Top,
                    Height = 16
                };

                valLabel = new Label
                {
                    ForeColor = Color.FromArgb(16, 35, 63),
                    Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
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
                Dock = DockStyle.Fill
            };

            var tabRozbor = new TabPage("🔧 Technický rozbor a architektura");
            rtbRozbor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.75f),
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
                Font = new Font("Segoe UI", 9.75f),
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
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
            };

            btnAiMetriky = new Button
            {
                Text = "🤖 Spočítat odhad přes AI",
                Width = 200,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(16, 35, 63), // Navy
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };
            btnAiMetriky.FlatAppearance.BorderSize = 0;
            btnAiMetriky.Click += BtnAiMetriky_Click;

            btnCopy = new Button
            {
                Text = "📋 Kopírovat text",
                Width = 140,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(16, 35, 63),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
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
            if (_metriky == null || _metriky.CasVypoctu == default)
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
            lblCasOdhaduVal.Text = $"{_metriky.CasovyOdhadMin} až {_metriky.CasovyOdhadMax}";
            lblKomplexitaVal.Text = _metriky.Komplexita;
            
            // Barevné odlišení komplexity
            if (_metriky.Komplexita.Contains("Vysoká"))
                lblKomplexitaVal.ForeColor = Color.Crimson;
            else if (_metriky.Komplexita.Contains("Střední"))
                lblKomplexitaVal.ForeColor = Color.DarkGoldenrod;
            else
                lblKomplexitaVal.ForeColor = Color.Green;

            lblRozpocetVal.Text = _metriky.DoporucenyRozpocet;
            lblSlozeniVal.Text = _metriky.SlozeniTymu;

            rtbRozbor.Text = _metriky.TechnickyRozbor;
            
            rtbRizika.Clear();
            if (_metriky.RizikaMetriky != null && _metriky.RizikaMetriky.Count > 0)
            {
                foreach (var r in _metriky.RizikaMetriky)
                {
                    rtbRizika.AppendText($"• {r}\n\n");
                }
            }
            else
            {
                rtbRizika.Text = "Nebyly definovány žádné specifické hrozby.";
            }

            lblStatus.Text = $"Odhad aktualizován: {_metriky.CasVypoctu:d. M. yyyy v H:mm}";
        }

        private async void BtnAiMetriky_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            btnAiMetriky.Enabled = false;
            btnAiMetriky.Text = "⏳ Počítám...";
            lblStatus.Text = "Počítám odhad pomocí Gemini API...";

            try
            {
                var noveMetriky = await GeminiService.GenerujMetrikyAsync(_apiKey, _model, _projekt);
                
                _metriky.CasovyOdhadMin = noveMetriky.CasovyOdhadMin;
                _metriky.CasovyOdhadMax = noveMetriky.CasovyOdhadMax;
                _metriky.Komplexita = noveMetriky.Komplexita;
                _metriky.DoporucenyRozpocet = noveMetriky.DoporucenyRozpocet;
                _metriky.SlozeniTymu = noveMetriky.SlozeniTymu;
                _metriky.TechnickyRozbor = noveMetriky.TechnickyRozbor;
                _metriky.RizikaMetriky = noveMetriky.RizikaMetriky;
                _metriky.CasVypoctu = DateTime.Now;

                _projekt.Log.Add(new Rozhodnuti
                {
                    Cas = DateTime.Now,
                    Akce = "Projektové metriky",
                    Detail = $"AI odhad pracnosti: {_metriky.CasovyOdhadMin} až {_metriky.CasovyOdhadMax} (komplexita: {_metriky.Komplexita})."
                });

                _onZmena();
                NaplnMetriky();
                MessageBox.Show(this, "AI analýza a odhad projektu byly úspěšně dokončeny.", "Odhad hotov", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Výpočet odhadu selhal:\n\n" + ex.Message, "Chyba AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Výpočet selhal.";
            }
            finally
            {
                btnAiMetriky.Enabled = !string.IsNullOrWhiteSpace(_apiKey);
                btnAiMetriky.Text = "🤖 Spočítat odhad přes AI";
                Cursor = Cursors.Default;
            }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== METRIKY A ODHAD PROJEKTU ===");
            sb.AppendLine($"Projekt: {_projekt.Nazev}");
            sb.AppendLine($"Odhad času: {_metriky.CasovyOdhadMin} až {_metriky.CasovyOdhadMax}");
            sb.AppendLine($"Složitost: {_metriky.Komplexita}");
            sb.AppendLine($"Doporučený rozpočet: {_metriky.DoporucenyRozpocet}");
            sb.AppendLine($"Doporučené složení týmu: {_metriky.SlozeniTymu}");
            sb.AppendLine();
            sb.AppendLine("--- Technický rozbor ---");
            sb.AppendLine(_metriky.TechnickyRozbor);
            sb.AppendLine();
            sb.AppendLine("--- Odhadovaná rizika ---");
            foreach (var r in _metriky.RizikaMetriky)
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
