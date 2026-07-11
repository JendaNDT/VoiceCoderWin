using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
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
        private string _nazevSnapshot = "";
        private System.Windows.Forms.Timer _debounceTimer;     // Debounce pro název a nápad
        private ToolStrip toolBar;        // Uchovává instanci toolbar pro snadné vypnutí
        private bool _chatBusy = false;   // Flag proti vícenásobnému odeslání chatu
        private CancellationTokenSource _ctsAi = null;
        private CancellationTokenSource _ctsChat = null;                 // storno běžící chatové zprávy
        private System.Windows.Forms.Timer _autosaveTimer;               // automatická záloha rozdělané práce (2 min)
        private System.Windows.Forms.Timer _prubehTimer;                 // ukazuje uplynulý čas běžící AI operace (1 s)
        private System.Windows.Forms.Timer _diktovaniLimitTimer;         // auto-stop diktování po 3 minutách
        private DateTime _casStartuAiOperace;                            // start běžící AI operace (pro průběžný čas)
        private bool _snapshotAnalyzyExistuje = false;                   // v této session vznikla záloha před AI analýzou
        private Button _tlacitkoDiktovaniAktivni = null;                 // které mikrofonní tlačítko právě nahrává
        private ToolStripButton btnVratitAnalyzu;                        // „↩ Vrátit analýzu“ v toolbaru
        private Label lblApiBanner;                                      // banner „chybí API klíč“ nahoře

        private const string PlaceholderChatu = "Např. ‚Co v zadání ještě chybí?‘ nebo ‚Co bude nejtěžší část?‘";
        private const string TipDiktovani = "Podržte a mluvte, nebo klikněte pro zapnutí/vypnutí. Přepis zajišťuje AI (Gemini). Tip: Win+H je vestavěné diktování Windows zdarma.";
        private const string RadaMikrofon = "Zkontrolujte mikrofon v Nastavení Windows → Soukromí → Mikrofon.";

        private static string SlozkaDatAplikace
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodePlanner");
        private static string CestaAutosave => Path.Combine(SlozkaDatAplikace, "autosave.vcbrief");
        private static string CestaZalohyPredAnalyzou => Path.Combine(SlozkaDatAplikace, "pred_analyzou.vcbrief");

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
        private bool _ignorujDalsiMouseUp = false;
        private bool _isBusy = false;
        private Font _chatFontItalic;
        private Font _chatFontBold;
        private Font _chatFontRegular;
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
            _debounceTimer = new System.Windows.Forms.Timer();
            _debounceTimer.Interval = 500;
            _debounceTimer.Tick += DebounceTimer_Tick;

            _autosaveTimer = new System.Windows.Forms.Timer { Interval = 120_000 };
            _autosaveTimer.Tick += AutosaveTimer_Tick;
            _autosaveTimer.Start();

            _prubehTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _prubehTimer.Tick += PrubehTimer_Tick;

            _diktovaniLimitTimer = new System.Windows.Forms.Timer { Interval = 180_000 };
            _diktovaniLimitTimer.Tick += DiktovaniLimitTimer_Tick;

            _chatFontItalic = new Font("Segoe UI", 10f, FontStyle.Italic);
            _chatFontBold = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            _chatFontRegular = new Font("Segoe UI", 9.5f, FontStyle.Regular);

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

            toolBar = PostavToolbar();
            var status = PostavStatusBar();

            lblApiBanner = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "🔑 AI funkce vyžadují bezplatný klíč Gemini – klikněte pro nastavení.",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Color.FromArgb(255, 248, 214),
                ForeColor = Color.FromArgb(133, 100, 4),
                Cursor = Cursors.Hand,
                Visible = false
            };
            lblApiBanner.Click += (s, e) => OtevritNastaveni();

            // pořadí přidání řídí docking: později přidané se dokují dřív
            Controls.Add(split);          // Fill
            Controls.Add(oddelovac);      // Bottom (nad logem, umožní měnit jeho výšku)
            Controls.Add(logBox);         // Bottom
            Controls.Add(lblApiBanner);   // Top (pod toolbarem)
            Controls.Add(toolBar);        // Top
            Controls.Add(status);         // Bottom (pod logem)

            FormClosing += (s, e) =>
            {
                if (!PotvrdNeulozene())
                {
                    e.Cancel = true;
                }
                else if (!e.Cancel)
                {
                    if (!_dirty) SmazAutosave();   // čisté zavření – automatická záloha už není potřeba
                    _chatFontItalic?.Dispose();
                    _chatFontBold?.Dispose();
                    _chatFontRegular?.Dispose();
                }
            };

            NovyProjekt(prvniSpusteni: true);
            ObnovNedavneMenu();
            ObnovApiBanner();
            Shown += (s, e) => NabidniObnovuAutosave();
        }

        // ---------------- klávesové zkratky ----------------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_isBusy || _chatBusy)
            {
                if (keyData == Keys.Escape)
                {
                    // Esc zruší běžící AI operaci (analýzu i chat)
                    _ctsAi?.Cancel();
                    _ctsChat?.Cancel();
                    return true;
                }
                if (keyData == (Keys.Control | Keys.S))
                {
                    // uložit lze kdykoli, i během práce AI
                    UlozitProjekt();
                    return true;
                }
                return true;   // ostatní zkratky během AI operace blokujeme
            }
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
                    // funguje i mimo pole odpovědi, pokud je vybraná otázka (chat má vlastní Enter)
                    if (!txtChatInput.Focused && VybranaOtazka() != null) { UlozOdpoved(); return true; }
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
            tool.Items.Add(Tlacitko("⬇ Markdown…", "Pro lidi – čitelný dokument (Ctrl+M)", (s, e) => Export(true)));
            tool.Items.Add(Tlacitko("⬇ JSON…", "Pro AI agenta – strojová data (Ctrl+J)", (s, e) => Export(false)));
            tool.Items.Add(Tlacitko("📄 PDF…", "Export specifikace do PDF pro klienty (Ctrl+P)", (s, e) => ExportujPdf()));
            tool.Items.Add(Tlacitko("🌐 HTML Web…", "Export specifikace do interaktivního HTML webu", (s, e) => ExportujHtml()));
            tool.Items.Add(Tlacitko("💡 User Stories…", "Správa a generování uživatelských příběhů pro vývojáře", (s, e) => ZobrazUserStories()));
            tool.Items.Add(Tlacitko("📊 Metriky a Odhad…", "Projektové metriky a AI časový odhad", (s, e) => ZobrazMetriky()));
            tool.Items.Add(Tlacitko("✔ Kontrola…", "Kontrola konzistence specifikace – rozpory a varování", (s, e) => ZobrazNalezy(true)));

            btnVratitAnalyzu = new ToolStripButton("↩ Vrátit analýzu")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Obnoví projekt ze zálohy vytvořené před poslední AI analýzou.",
                Padding = new Padding(4, 2, 4, 2),
                Visible = false
            };
            btnVratitAnalyzu.Click += (s, e) => VratitAnalyzu();
            tool.Items.Add(btnVratitAnalyzu);

            tool.Items.Add(new ToolStripSeparator());
            tool.Items.Add(Tlacitko("⚙ Nastavení AI…", "Nastavení Gemini API klíče a modelu", (s, e) => OtevritNastaveni()));
            tool.Items.Add(Tlacitko("❓", "Nápověda – čtyři kroky práce a klávesové zkratky", (s, e) => ZobrazNapovedu()));
            tool.Items.Add(new ToolStripSeparator());

            var tip2 = new ToolStripLabel("🎤 Diktování: podržte tlačítko a mluvte · Win+H = vestavěné diktování Windows zdarma")
            {
                ForeColor = SedaText
            };
            tool.Items.Add(tip2);

            var verze = new ToolStripLabel("v2.0.0")
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
                Text = "Log rozhodnutí (každá změna má čas a důvod) – výšku upravíte tažením horního okraje",
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
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };
            txtNazev.Enter += (s, e) => _nazevSnapshot = _projekt.Nazev ?? "";
            txtNazev.Leave += (s, e) =>
            {
                if (_nacitani) return;
                _debounceTimer.Stop();
                if ((_projekt.Nazev ?? "") != _nazevSnapshot)
                {
                    SpecSluzba.Zmena(_projekt, "Název", $"Změněn název projektu na '{_projekt.Nazev}'.");
                    ObnovLog();
                    ObnovStav();
                }
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
            var lblNapad = Nadpis("1 · Nápad (pište, nebo diktujte)");
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
            _tipReference.SetToolTip(btnDiktovatNapad, TipDiktovani);

            btnReferencie = new Button
            {
                Text = "📎 Referenční podklady",
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
            menuReferencie.Items.Add("Zobrazit referenční podklady...", null, (s, e) => ZobrazitObsahReferenci());
            menuReferencie.Items.Add("Změnit podklady...", null, (s, e) => NahratReferenci());
            menuReferencie.Items.Add("Odebrat podklady", null, (s, e) => OdebratReferenci());

            btnMockup = new Button
            {
                Text = "🖼 Vizuální mockup (skica)",
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
            menuMockup.Items.Add("Zobrazit vizuální mockup...", null, (s, e) => ZobrazitMockup());
            menuMockup.Items.Add("Změnit vizuální mockup...", null, (s, e) => NahratMockup());
            menuMockup.Items.Add("Odebrat vizuální mockup", null, (s, e) => OdebratMockup());

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
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };
            txtNapad.Enter += (s, e) => _napadSnapshot = _projekt.Napad ?? "";
            txtNapad.Leave += (s, e) =>
            {
                if (_nacitani) return;
                _debounceTimer.Stop();
                if ((_projekt.Napad ?? "") != _napadSnapshot)
                {
                    SpecSluzba.Zmena(_projekt, "Nápad", "Upraven text původního nápadu.");
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
            var tabSpecPage = new TabPage("3 · 📄 Specifikace a exporty");
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
                RowCount = 3,
                BackColor = Color.White
            };
            tlpChat.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // chat history log
            tlpChat.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));   // input panel
            tlpChat.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // hint ke klávesám

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
                Text = PlaceholderChatu
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
                    txtChatInput.Text = PlaceholderChatu;
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

            var lblChatHint = new Label
            {
                Text = "Enter = odeslat · Shift+Enter = nový řádek",
                AutoSize = true,
                ForeColor = SedaText,
                Font = new Font("Segoe UI", 8f),
                Margin = new Padding(8, 0, 0, 4)
            };

            tlpChat.Controls.Add(rtbChatLog, 0, 0);
            tlpChat.Controls.Add(pnlChatInputArea, 0, 1);
            tlpChat.Controls.Add(lblChatHint, 0, 2);

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
                Text = "2 · Otázky a odpovědi (nejdřív ty s největším dopadem)",
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ForeColor = Navy
            };

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 9,
                BackColor = Color.Transparent
            };
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // seznam otázek
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // mini-legenda značek
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
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
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
                Text = "Nevím → použít předpoklad",
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
                Text = "🎤 Diktovat",
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
            _tipReference.SetToolTip(btnDiktovatOdpoved, TipDiktovani);

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

            var lblLegenda = new Label
            {
                AutoSize = true,
                Text = "✔ zodpovězeno · P předpoklad · V/S dopad vysoký/střední",
                ForeColor = SedaText,
                Font = new Font("Segoe UI", 8f),
                Margin = new Padding(0, 0, 0, 4)
            };

            tlp.Controls.Add(lstOtazky, 0, 0);
            tlp.Controls.Add(lblLegenda, 0, 1);
            tlp.Controls.Add(lblOtazka, 0, 2);
            tlp.Controls.Add(lblNapoveda, 0, 3);
            tlp.Controls.Add(txtOdpoved, 0, 4);
            tlp.Controls.Add(pnlQuickOptions, 0, 5);
            tlp.Controls.Add(tlacitka, 0, 6);
            tlp.Controls.Add(pnlPostup, 0, 7);
            tlp.Controls.Add(lblPostup, 0, 8);

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

            // Dynamický výpočet velikostí podle DPI
            float scale = (float)this.DeviceDpi / 96f;
            int badgeSize = (int)(14 * scale);
            int chipWidth = (int)(20 * scale);
            int chipHeight = (int)(16 * scale);
            int padding = (int)(8 * scale);
            int chipOffset = padding + badgeSize + (int)(6 * scale);
            int textOffset = chipOffset + chipWidth + (int)(6 * scale);

            // 1. Badge stavu (grafický kruh)
            var badgeRect = new Rectangle(e.Bounds.X + padding, e.Bounds.Y + (e.Bounds.Height - badgeSize) / 2, badgeSize, badgeSize);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (stav == '✔')
            {
                using (var b = new SolidBrush(Zelena))
                {
                    e.Graphics.FillEllipse(b, badgeRect);
                }
                using (var f = new Font("Segoe UI", Math.Max(6.5f, 8f * scale), FontStyle.Bold))
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
                using (var f = new Font("Segoe UI", Math.Max(6f, 7.5f * scale), FontStyle.Bold))
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
            var chip = new Rectangle(e.Bounds.X + chipOffset, e.Bounds.Y + (e.Bounds.Height - chipHeight) / 2, chipWidth, chipHeight);
            using (var b = new SolidBrush(vysoky ? Navy : Color.Gainsboro))
            using (var path = Zaobli(chip, (int)(3 * scale)))
                e.Graphics.FillPath(b, path);

            using (var f = new Font("Segoe UI", Math.Max(6f, 7.5f * scale), FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, vysoky ? "V" : "S", f, chip,
                    vysoky ? Color.White : Color.DimGray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 3. Text otázky
            var textRect = new Rectangle(e.Bounds.X + textOffset, e.Bounds.Y, e.Bounds.Width - (textOffset + 4), e.Bounds.Height);
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

            // záloha před analýzou patřila k předchozímu projektu – tlačítko „↩ Vrátit analýzu“ skryjeme
            _snapshotAnalyzyExistuje = false;
            ObnovTlacitkoVratitAnalyzu();

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

                // záloha před analýzou patřila k předchozímu projektu – tlačítko „↩ Vrátit analýzu“ skryjeme
                _snapshotAnalyzyExistuje = false;
                ObnovTlacitkoVratitAnalyzu();

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
                SmazAutosave();   // po ručním uložení už automatická záloha není potřeba
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
            if (!PotvrdExportSeSouhrnem()) return;

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

                var res = MessageBox.Show(this,
                    "Specifikace byla exportována:\n\n" + dlg.FileName + "\n\nChcete vytvořený soubor ihned otevřít?",
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

        private void ExportujHtml()
        {
            if (!PotvrdExportSeSouhrnem()) return;

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
                    "Interaktivní specifikace byla exportována do HTML.\n\nChcete vytvořený web ihned otevřít v prohlížeči?",
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
            if (!PotvrdExportSeSouhrnem()) return;

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

                    if (!File.Exists(dlg.FileName))
                    {
                        throw new FileNotFoundException("Soubor PDF nebyl vytvořen. Zkontrolujte prosím konfiguraci své PDF tiskárny.");
                    }

                    Stav("Export do PDF dokončen.");
                    var res = MessageBox.Show(this,
                        "Specifikace byla úspěšně exportována do PDF.\n\nChcete vytvořený soubor ihned otevřít?",
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
                MessageBox.Show(this, "Napište odpověď, nebo zvolte „Nevím → použít předpoklad“.",
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
                Stav("Všechny otázky jsou vyřešené – specifikaci můžete exportovat (krok 3).");
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

            bool programoveVolani = _nacitani;   // true = jde o programovou obnovu seznamu, ne o volbu uživatele

            lblOtazka.Text = ot.GetText(_projekt.TypProjektuKlic);
            lblNapoveda.Text = ot.GetNapoveda(_projekt.TypProjektuKlic) + "  (Když nevíte, předpoklad bude: „" + ot.GetVychoziPredpoklad(_projekt.TypProjektuKlic) + "“)";

            var odp = SpecSluzba.OdpovedNa(_projekt, ot.Id);
            _nacitani = true;
            txtOdpoved.Text = odp != null && !odp.JePredpoklad ? odp.Text : "";
            _nacitani = programoveVolani;

            foreach (Control ctrl in pnlQuickOptions.Controls)
            {
                ctrl.Dispose();
            }
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

            // fokus rovnou do pole odpovědi, aby šlo hned psát (ne při programové obnově seznamu ani během AI)
            if (!programoveVolani && !_isBusy && !_chatBusy && txtOdpoved.Enabled)
            {
                txtOdpoved.Focus();
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
            ObnovComboTypu();
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

            int prvyIndex = -1;
            int start = 0;
            while (start < rtbSpec.TextLength)
            {
                int index = rtbSpec.Find(dotaz, start, RichTextBoxFinds.None);
                if (index == -1) break;

                if (prvyIndex == -1) prvyIndex = index;

                rtbSpec.Select(index, dotaz.Length);
                rtbSpec.SelectionBackColor = Color.Yellow;
                rtbSpec.SelectionColor = Color.Black;

                start = index + dotaz.Length;
            }
            rtbSpec.SelectionLength = 0; // zrušíme výběr

            if (prvyIndex != -1)
            {
                rtbSpec.SelectionStart = prvyIndex;
                rtbSpec.ScrollToCaret();
            }
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
                string.Join(" a ", casti) + " – klikněte pro detail";
            lblNalezy.BackColor = rozpory > 0 ? Color.FromArgb(253, 232, 232) : Color.FromArgb(255, 244, 219);
            lblNalezy.ForeColor = rozpory > 0 ? Color.FromArgb(155, 28, 28) : Color.FromArgb(146, 90, 4);
            lblNalezy.Visible = true;
        }

        /// <summary>Otevře okno kontroly konzistence. Z toolbaru (iKdyzPrazdne = true) se otevře
        /// i s prázdným seznamem – uživatel tak vidí, že kontrola existuje a co hlídá.</summary>
        private void ZobrazNalezy(bool iKdyzPrazdne = false)
        {
            if (iKdyzPrazdne) ObnovNalezy();   // ať pracujeme s aktuálním stavem
            if (_nalezy.Count == 0 && !iKdyzPrazdne) return;
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

            if (_isBusy || _chatBusy) return;   // průběžný stav AI operace nepřepisujeme

            if (string.IsNullOrWhiteSpace(_projekt.Napad) && _projekt.Odpovedi.Count == 0)
            {
                Stav("Začněte popsáním nápadu (krok 1) a nechte AI připravit otázky na míru.");
                return;
            }

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
            Text = "CodePlanner – " + nazev + (_dirty ? " *" : "") + " – v2.0.0";
        }

        private void Stav(string text) => lblStav.Text = text;

        private bool PotvrdNeulozene()
        {
            if (!_dirty) return true;
            var res = MessageBox.Show(this,
                "Máte neuložené změny. Chcete je před pokračováním uložit?",
                "Neuložené změny", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (res == DialogResult.Cancel) return false;
            if (res == DialogResult.Yes) return UlozitProjekt();
            return true;
        }

        internal static string BezpecnyNazevSouboru(string nazev, string vychozi)
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
            ObnovApiBanner();   // po uložení klíče banner zmizí
        }

        private async void BtnAiAnalyza_Click(object sender, EventArgs e)
        {
            if (_isBusy)
            {
                _ctsAi?.Cancel();
                return;
            }

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

            if (_projekt.Odpovedi.Count > 0 || _projekt.UserStories.Count > 0)
            {
                var confirm = MessageBox.Show(this,
                    "Analýza přepíše stávající odpovědi (" + _projekt.Odpovedi.Count + "), smaže User Stories (" +
                    _projekt.UserStories.Count + ") i odhad.\n" +
                    "Před spuštěním se vytvoří záloha, kterou lze vrátit tlačítkem „↩ Vrátit analýzu“.\n\nChcete pokračovat?",
                    "Přepsat specifikaci?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;
            }

            // Záloha projektu před analýzou – lze ji vrátit tlačítkem „↩ Vrátit analýzu“ v liště.
            try
            {
                SpecSluzba.UlozProjekt(_projekt, CestaZalohyPredAnalyzou);
                _snapshotAnalyzyExistuje = true;
                ObnovTlacitkoVratitAnalyzu();
            }
            catch (Exception exZaloha)
            {
                var pokracovat = MessageBox.Show(this,
                    "Nepodařilo se vytvořit zálohu před analýzou:\n\n" + exZaloha.Message +
                    "\n\nChcete přesto pokračovat (bez možnosti vrácení)?",
                    "Záloha se nezdařila", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (pokracovat != DialogResult.Yes) return;
            }

            string stavPoDokonceni = "Připraveno.";
            NastavitStavBusy(true, "Komunikuji s Gemini API...");
            _ctsAi = new CancellationTokenSource();

            try
            {
                string model = nastaveni.GeminiModel;
                string mockupMime = (_projekt.MockupNazev != null && (_projekt.MockupNazev.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || _projekt.MockupNazev.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))) ? "image/jpeg" : "image/png";
                var vysledek = await GeminiService.AnalyzujNapadAsync(apiKey, model, napad, _projekt.TypProjektuKlic, _projekt.ReferencniText, _projekt.MockupBase64, mockupMime, _ctsAi.Token);

                if (vysledek == null || vysledek.Otazky == null || vysledek.Otazky.Count == 0)
                {
                    throw new Exception("AI analýza nevrátila žádné otázky.");
                }

                var uniqueQuestions = vysledek.Otazky
                    .Where(ot => !string.IsNullOrWhiteSpace(ot.Id))
                    .GroupBy(ot => ot.Id)
                    .Select(g => g.First())
                    .ToList();

                try
                {
                    _nacitani = true;
                    _projekt.Nazev = vysledek.Nazev ?? "";
                    txtNazev.Text = _projekt.Nazev;
                    _projekt.Otazky.Clear();
                    _projekt.Odpovedi.Clear();
                    _projekt.UserStories.Clear();
                    _projekt.Metriky = new ProjektMetriky();

                    foreach (var ot in uniqueQuestions)
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

                        // Prázdnou odpověď od AI nepřidáváme – otázka zůstane otevřená.
                        // Má-li otázka výchozí předpoklad, použijeme ho a poctivě označíme jako předpoklad.
                        string textOdpovedi = (ot.Odpoved ?? "").Trim();
                        if (textOdpovedi.Length > 0)
                        {
                            _projekt.Odpovedi.Add(new Odpoved
                            {
                                OtazkaId = ot.Id,
                                Text = textOdpovedi,
                                JePredpoklad = ot.JePredpoklad,
                                Cas = DateTime.Now
                            });
                        }
                        else if (!string.IsNullOrWhiteSpace(ot.VychoziPredpoklad))
                        {
                            _projekt.Odpovedi.Add(new Odpoved
                            {
                                OtazkaId = ot.Id,
                                Text = ot.VychoziPredpoklad.Trim(),
                                JePredpoklad = true,
                                Cas = DateTime.Now
                            });
                        }
                    }
                }
                finally
                {
                    _nacitani = false;
                }

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

                // úspěch bez vyskakovacího okna – stačí stavový řádek (viz UX bod „méně modálů“)
                stavPoDokonceni = "✅ Analýza dokončena – projděte si otázky a odpovědi (krok 2). Vrátit ji lze tlačítkem „↩ Vrátit analýzu“.";
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException || ex.InnerException is OperationCanceledException)
                {
                    stavPoDokonceni = "Analýza zrušena – projekt zůstal beze změn.";
                    return;
                }
                MessageBox.Show(this, $"Během analýzy nápadu došlo k chybě:\n\n{ex.Message}",
                    "Chyba AI analýzy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                stavPoDokonceni = "Analýza se nezdařila – projekt zůstal beze změn.";
            }
            finally
            {
                _ctsAi?.Dispose();
                _ctsAi = null;
                NastavitStavBusy(false, stavPoDokonceni);
            }
        }

        private void NastavitStavBusy(bool busy, string textStavu)
        {
            _isBusy = busy;
            Stav(textStavu);
            btnAiAnalyza.Enabled = true;
            btnAiAnalyza.Text = busy ? "❌ Zrušit analýzu" : "🤖 Analyzovat přes Gemini";

            if (busy)
            {
                _casStartuAiOperace = DateTime.Now;
                _prubehTimer.Start();
            }
            else
            {
                _prubehTimer.Stop();
            }

            txtNazev.Enabled = !busy;
            txtNapad.Enabled = !busy;
            lstOtazky.Enabled = !busy;
            txtOdpoved.Enabled = !busy;
            btnOdpovedet.Enabled = !busy;
            btnPredpoklad.Enabled = !busy;

            // typ zůstává zamčený, dokud má projekt dynamické otázky z AI analýzy (viz ObnovComboTypu)
            if (cmbTyp != null) cmbTyp.Enabled = !busy && (_projekt?.Otazky == null || _projekt.Otazky.Count == 0);
            if (btnDiktovatNapad != null) btnDiktovatNapad.Enabled = !busy;
            if (btnDiktovatOdpoved != null) btnDiktovatOdpoved.Enabled = !busy;
            if (btnReferencie != null) btnReferencie.Enabled = !busy;
            if (btnMockup != null) btnMockup.Enabled = !busy;

            if (toolBar != null) toolBar.Enabled = !busy;
            if (txtChatInput != null) txtChatInput.Enabled = !busy;
            if (btnSendChat != null) btnSendChat.Enabled = !busy;
            if (btnClearChat != null) btnClearChat.Enabled = !busy;

            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void BtnDiktovat_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            var b = (Button)sender;

            if (_diktovaniClickToggle)
            {
                _diktovaniClickToggle = false;
                _ignorujDalsiMouseUp = true;
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
                _tlacitkoDiktovaniAktivni = b;
                _diktovaniLimitTimer.Stop();
                _diktovaniLimitTimer.Start();   // pojistka: auto-stop po 3 minutách
                b.BackColor = Color.Crimson;
                b.ForeColor = Color.White;
                b.Text = "🎤 Nahrávám...";
                Stav("Diktování spuštěno. Držte tlačítko, nebo klikněte znovu pro stop (limit 3 minuty).");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Nepodařilo se spustit nahrávání z mikrofonu:\n\n" + ex.Message + "\n\n" + RadaMikrofon,
                    "Chyba nahrávání", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDiktovat_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            
            if (_ignorujDalsiMouseUp)
            {
                _ignorujDalsiMouseUp = false;
                return;
            }

            if (_diktovaniClickToggle) return; // v toggle režimu nereagujeme na uvolnění

            if (_tlacitkoDiktovaniAktivni == null) return; // nahrávání už neběží (např. auto-stop po 3 minutách, nebo selhal start)

            var b = (Button)sender;
            double ms = (DateTime.Now - _casSpusteniDiktovani).TotalMilliseconds;

            if (ms < 400)
            {
                // stisk byl příliš rychlý -> přepneme do režimu klikni-a-mluv
                _diktovaniClickToggle = true;
                b.Text = "🎤 Nahrávám (klikněte pro stop)";
                Stav("Režim klikni-a-mluv spuštěn. Nahrávám… Klikněte na tlačítko znovu pro ukončení (limit 3 minuty).");
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

        private async void ZastavADiktuj(Button b, bool autoStop = false)
        {
            _diktovaniLimitTimer.Stop();
            _tlacitkoDiktovaniAktivni = null;

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
                b.Text = "🎤 Diktovat";
            }

            if (string.IsNullOrEmpty(cestaWav))
            {
                Stav("Diktování zrušeno, nebo se nahrávku nepodařilo uložit. " + RadaMikrofon);
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
                
                if (this.IsDisposed || !this.Created) return;
                if (cil == null || cil.IsDisposed) return;

                if (string.IsNullOrWhiteSpace(prepis))
                {
                    MessageBox.Show(this,
                        "Z nahrávky nebylo rozpoznáno žádné slovo.\n\n" + RadaMikrofon,
                        "Prázdný přepis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Stav("Přepis nevrátil žádný text.");
                }
                else
                {
                    VlozTextNaKurzor(cil, prepis);
                    Stav(autoStop
                        ? "⏱ Nahrávání bylo po 3 minutách automaticky ukončeno a hlas přepsán."
                        : "Hlas úspěšně přepsán.");
                }
            }
            catch (Exception ex)
            {
                if (this.IsDisposed || !this.Created) return;
                MessageBox.Show(this, "Při přepisu hlasu došlo k chybě:\n\n" + ex.Message,
                    "Chyba přepisu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Stav("Chyba při přepisu.");
            }
            finally
            {
                if (!this.IsDisposed && this.Created)
                {
                    b.Enabled = true;
                    b.Text = "🎤 Diktovat";
                }
                
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
                    var fileInfo = new FileInfo(dlg.FileName);
                    if (fileInfo.Length > 2 * 1024 * 1024)
                    {
                        MessageBox.Show(this, "Vybraný soubor je příliš velký (maximum je 2 MB).", "Velký soubor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    string text = File.ReadAllText(dlg.FileName);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        MessageBox.Show(this, "Vybraný soubor je prázdný.", "Prázdný soubor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _projekt.ReferencniText = text;
                    _projekt.ReferencniNazev = Path.GetFileName(dlg.FileName);
                    SpecSluzba.Zmena(_projekt, "Příloha", $"Připojen referenční soubor {_projekt.ReferencniNazev}.");

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
            SpecSluzba.Zmena(_projekt, "Příloha", $"Odebrán referenční soubor {nazev}.");

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

                    // Validace formátu obrázku
                    try
                    {
                        using (var ms = new MemoryStream(bytes))
                        using (var tempImg = Image.FromStream(ms))
                        {
                            // Pokud FromStream nevyhodí výjimku, obrázek je validní
                        }
                    }
                    catch
                    {
                        MessageBox.Show(this, "Vybraný soubor nepředstavuje platný obrázek.", "Neplatný formát", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _projekt.MockupBase64 = Convert.ToBase64String(bytes);
                    _projekt.MockupNazev = Path.GetFileName(dlg.FileName);
                    SpecSluzba.Zmena(_projekt, "Skica", $"Připojen vizuální mockup {_projekt.MockupNazev}.");

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
                using (var img = Image.FromStream(ms))
                using (var dlg = new Form
                {
                    Text = $"Prohlížeč skici: {_projekt.MockupNazev}",
                    Size = new Size(800, 600),
                    StartPosition = FormStartPosition.CenterParent,
                    MinimizeBox = false,
                    MaximizeBox = true,
                    ShowIcon = false
                })
                {
                    dlg.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
                    dlg.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;

                    var pb = new PictureBox
                    {
                        Dock = DockStyle.Fill,
                        Image = img,
                        SizeMode = PictureBoxSizeMode.Zoom
                    };

                    dlg.Controls.Add(pb);
                    dlg.ShowDialog(this);
                }
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
            SpecSluzba.Zmena(_projekt, "Skica", $"Odebrán vizuální mockup {nazev}.");

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
                rtbChatLog.SelectionFont = _chatFontItalic;
                rtbChatLog.SelectionColor = Color.Gray;
                rtbChatLog.AppendText("Zatím zde nejsou žádné zprávy. Zeptejte se na cokoli ohledně své specifikace – třeba co v zadání ještě chybí.\n\n");
                return;
            }

            foreach (var msg in _projekt.ChatHistory)
            {
                bool isUser = string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase);
                rtbChatLog.SelectionFont = _chatFontBold;
                rtbChatLog.SelectionColor = isUser ? Navy : Teal;
                string casText = msg.Cas == default ? "" : $" [{msg.Cas:H:mm}]";
                rtbChatLog.AppendText(isUser ? $"Já{casText}: " : $"Asistent{casText}: ");

                rtbChatLog.SelectionFont = _chatFontRegular;
                rtbChatLog.SelectionColor = Color.Black;
                rtbChatLog.AppendText(msg.Text + "\n\n");
            }

            rtbChatLog.SelectionStart = rtbChatLog.Text.Length;
            rtbChatLog.ScrollToCaret();
        }

        private async void OdeslatChat()
        {
            if (_chatBusy)
            {
                // tlačítko je během čekání přepnuté na „Zrušit“ – druhé kliknutí operaci stornuje
                _ctsChat?.Cancel();
                return;
            }

            if (txtChatInput.ForeColor == Color.Gray || string.IsNullOrWhiteSpace(txtChatInput.Text))
            {
                return;
            }

            // kontrola API klíče předem – zpráva se nesmí dostat do historie
            var nastaveni = GeminiNastaveni.Nacti();
            if (string.IsNullOrWhiteSpace(nastaveni.EfektivniApiKey))
            {
                MessageBox.Show(this,
                    "Není nastaven API klíč pro Gemini.\nOtevřete prosím Nastavení AI a zadejte klíč.",
                    "Chybí API klíč", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                OtevritNastaveni();
                return;
            }

            string text = txtChatInput.Text.Trim();
            _chatBusy = true;
            txtChatInput.Enabled = false;
            btnSendChat.Text = "Zrušit";
            btnClearChat.Enabled = false;
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
            Stav("AI asistent odpovídá...");
            _casStartuAiOperace = DateTime.Now;
            _prubehTimer.Start();
            _ctsChat = new CancellationTokenSource();

            try
            {
                string odpoved = await GeminiService.PosliChatZpravuAsync(nastaveni.EfektivniApiKey, nastaveni.GeminiModel, _projekt, _projekt.ChatHistory, _ctsChat.Token);

                if (this.IsDisposed || !this.Created) return;

                var modelZprava = new ChatMessage { Role = "model", Text = odpoved, Cas = DateTime.Now };
                _projekt.ChatHistory.Add(modelZprava);
                OznacZmenu();
                VykresliHistoriiChatu();
                Stav("AI asistent odpověděl.");
            }
            catch (Exception ex)
            {
                if (this.IsDisposed || !this.Created) return;

                // zprávu vrátíme zpět do vstupu, ať o ni uživatel nepřijde
                if (_projekt.ChatHistory != null && _projekt.ChatHistory.Contains(uzivatelZprava))
                {
                    _projekt.ChatHistory.Remove(uzivatelZprava);
                }
                txtChatInput.Text = text;
                txtChatInput.ForeColor = Color.Black;
                VykresliHistoriiChatu();

                bool zruseno = ex is OperationCanceledException || ex.InnerException is OperationCanceledException;
                if (zruseno)
                {
                    Stav("Odeslání zprávy zrušeno – text zůstal ve vstupním poli.");
                }
                else
                {
                    MessageBox.Show(this, "Chyba při komunikaci s AI asistentem:\n\n" + ex.Message, "Chyba AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Stav("Komunikace selhala.");
                }
            }
            finally
            {
                _ctsChat?.Dispose();
                _ctsChat = null;
                _chatBusy = false;
                _prubehTimer.Stop();

                if (!this.IsDisposed && this.Created)
                {
                    txtChatInput.Enabled = true;
                    btnSendChat.Text = "Odeslat";
                    btnSendChat.Enabled = true;
                    btnClearChat.Enabled = true;
                    Cursor = Cursors.Default;
                    txtChatInput.Focus();
                }
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

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            if (_nacitani) return;
            RenderSpecifikaci();
        }

        // ---------------- autosave, zálohy a další pomocné metody (UX fáze 2) ----------------

        /// <summary>Každé 2 minuty tiše uloží rozdělanou práci do %AppData%\CodePlanner\autosave.vcbrief.</summary>
        private void AutosaveTimer_Tick(object sender, EventArgs e)
        {
            if (!_dirty || _isBusy || _chatBusy) return;
            if (ProjektJePrazdny()) return;

            try
            {
                SpecSluzba.UlozProjekt(_projekt, CestaAutosave);
            }
            catch
            {
                // automatická záloha nikdy nesmí rušit práci uživatele
            }
        }

        private bool ProjektJePrazdny()
            => string.IsNullOrWhiteSpace(_projekt.Nazev)
               && string.IsNullOrWhiteSpace(_projekt.Napad)
               && _projekt.Odpovedi.Count == 0
               && (_projekt.ChatHistory == null || _projekt.ChatHistory.Count == 0);

        private static void SmazAutosave()
        {
            try { if (File.Exists(CestaAutosave)) File.Delete(CestaAutosave); } catch { }
        }

        /// <summary>Po startu nabídne obnovu automatické zálohy (např. po pádu aplikace nebo výpadku proudu).</summary>
        private void NabidniObnovuAutosave()
        {
            try
            {
                if (!File.Exists(CestaAutosave)) return;

                DateTime cas = File.GetLastWriteTime(CestaAutosave);
                var res = MessageBox.Show(this,
                    $"Byla nalezena automatická záloha neuložené práce (z {cas:HH:mm}). Chcete ji obnovit?",
                    "Obnova automatické zálohy", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (res == DialogResult.Yes)
                {
                    var projekt = SpecSluzba.NactiProjekt(CestaAutosave);
                    _cestaSouboru = null;   // záloha nemá „domovský“ soubor – uložení si vyžádá cestu
                    NactiProjektDoUi(projekt);
                    OznacZmenu();
                    Stav("Automatická záloha obnovena – nezapomeňte projekt uložit (Ctrl+S).");
                }

                SmazAutosave();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Automatickou zálohu se nepodařilo načíst.\n\n" + ex.Message,
                    "Chyba obnovy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SmazAutosave();
            }
        }

        /// <summary>Nahradí aktuální projekt daty z jiné instance a obnoví celé UI (ponechá _cestaSouboru beze změny).</summary>
        private void NactiProjektDoUi(SpecProjekt novy)
        {
            _projekt = novy ?? new SpecProjekt();

            _nacitani = true;
            txtNazev.Text = _projekt.Nazev ?? "";
            txtNapad.Text = _projekt.Napad ?? "";
            NastavTypCombo(_projekt.TypProjektuKlic);
            txtOdpoved.Text = "";
            _nacitani = false;

            ObnovVse();
            VyberOtazku(SpecSluzba.DalsiNezodpovezena(_projekt));
        }

        /// <summary>„↩ Vrátit analýzu“ – obnoví projekt ze zálohy vytvořené těsně před poslední AI analýzou.</summary>
        private void VratitAnalyzu()
        {
            if (!_snapshotAnalyzyExistuje || !File.Exists(CestaZalohyPredAnalyzou))
            {
                _snapshotAnalyzyExistuje = false;
                ObnovTlacitkoVratitAnalyzu();
                Stav("Záloha před analýzou není k dispozici.");
                return;
            }

            var res = MessageBox.Show(this,
                "Projekt se vrátí do stavu před poslední AI analýzou. Současné otázky a odpovědi budou nahrazeny.\n\nChcete pokračovat?",
                "Vrátit analýzu", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res != DialogResult.Yes) return;

            try
            {
                var projekt = SpecSluzba.NactiProjekt(CestaZalohyPredAnalyzou);
                NactiProjektDoUi(projekt);
                OznacZmenu();
                Stav("Projekt vrácen do stavu před AI analýzou.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Zálohu se nepodařilo načíst.\n\n" + ex.Message,
                    "Chyba obnovy", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ObnovTlacitkoVratitAnalyzu()
        {
            if (btnVratitAnalyzu == null) return;
            btnVratitAnalyzu.Visible = _snapshotAnalyzyExistuje;
            btnVratitAnalyzu.Enabled = _snapshotAnalyzyExistuje;
        }

        /// <summary>Banner „chybí API klíč“ – zobrazí se jen, dokud uživatel klíč nenastaví.</summary>
        private void ObnovApiBanner()
        {
            if (lblApiBanner == null) return;
            var nastaveni = GeminiNastaveni.Nacti();
            lblApiBanner.Visible = string.IsNullOrWhiteSpace(nastaveni.EfektivniApiKey);
        }

        /// <summary>Po AI analýze je typ projektu daný vygenerovanými otázkami – combo se zamkne.</summary>
        private void ObnovComboTypu()
        {
            if (cmbTyp == null) return;
            bool zafixovan = _projekt?.Otazky != null && _projekt.Otazky.Count > 0;
            cmbTyp.Enabled = !zafixovan && !_isBusy;
            _tipReference?.SetToolTip(cmbTyp, zafixovan
                ? "Typ je zafixován AI analýzou – nová analýza ho může změnit."
                : "Šablona řízených otázek podle typu projektu.");
        }

        /// <summary>Před exportem shrne, co ve specifikaci ještě chybí nebo je zastaralé. Vrací true = exportovat.</summary>
        private bool PotvrdExportSeSouhrnem()
        {
            int celkem = SpecSluzba.VratOtazkyProjektu(_projekt).Count();
            int zodpovezeno = SpecSluzba.PocetZodpovezenych(_projekt) + SpecSluzba.PocetPredpokladu(_projekt);
            int otevrene = SpecSluzba.OtevreneOtazky(_projekt).Count;
            int nalezy = KonzistencniKontrola.Zkontroluj(_projekt).Count;
            bool metrikyStare = SpecSluzba.MetrikyJsouZastarale(_projekt);
            bool storiesStare = SpecSluzba.StoriesJsouZastarale(_projekt);

            if (otevrene == 0 && nalezy == 0 && !metrikyStare && !storiesStare) return true;

            var casti = new List<string>();
            if (otevrene > 0) casti.Add("zodpovězeno " + zodpovezeno + " z " + celkem + " otázek");
            if (nalezy > 0) casti.Add(Mnozne(nalezy, "nález kontroly konzistence", "nálezy kontroly konzistence", "nálezů kontroly konzistence"));
            if (metrikyStare && storiesStare) casti.Add("odhad i user stories jsou zastaralé");
            else if (metrikyStare) casti.Add("odhad je zastaralý");
            else if (storiesStare) casti.Add("user stories jsou zastaralé");

            var res = MessageBox.Show(this,
                "Specifikace zatím není úplná:\n\n• " + string.Join("\n• ", casti) + "\n\nPřesto exportovat?",
                "Souhrn před exportem", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            return res == DialogResult.Yes;
        }

        /// <summary>„❓“ v toolbaru – přehled kroků práce a klávesových zkratek.</summary>
        private void ZobrazNapovedu()
        {
            MessageBox.Show(this,
                "JAK POSTUPOVAT (4 kroky):\n" +
                "1. Popište svůj nápad vlastními slovy (klidně diktujte).\n" +
                "2. Nechte AI připravit otázky na míru (🤖 Analyzovat), nebo rovnou odpovídejte na připravené otázky.\n" +
                "3. Doplňte odpovědi – když nevíte, použijte předpoklad.\n" +
                "4. Hotovou specifikaci exportujte: Markdown pro lidi, JSON pro AI agenta, PDF či HTML pro klienty.\n\n" +
                "KLÁVESOVÉ ZKRATKY:\n" +
                "Ctrl+N – nový projekt\n" +
                "Ctrl+O – otevřít projekt\n" +
                "Ctrl+S – uložit projekt (funguje i během práce AI)\n" +
                "Ctrl+M – export Markdown\n" +
                "Ctrl+J – export JSON\n" +
                "Ctrl+P – export PDF\n" +
                "Ctrl+Enter – uložit odpověď na vybranou otázku\n" +
                "Esc – zrušit běžící AI operaci\n\n" +
                "ZNAČKY V SEZNAMU OTÁZEK:\n" +
                "✔ zodpovězeno · P předpoklad · V/S dopad vysoký/střední",
                "Nápověda", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>Každou sekundu ukazuje, jak dlouho už AI operace běží, a připomíná možnost zrušení.</summary>
        private void PrubehTimer_Tick(object sender, EventArgs e)
        {
            if (!_isBusy && !_chatBusy)
            {
                _prubehTimer.Stop();
                return;
            }
            int sekundy = (int)(DateTime.Now - _casStartuAiOperace).TotalSeconds;
            Stav("⏳ Komunikuji s AI… (" + sekundy + "s) – Esc zruší");
        }

        /// <summary>Pojistka diktování: po 3 minutách nahrávání automaticky zastaví a odešle k přepisu.</summary>
        private void DiktovaniLimitTimer_Tick(object sender, EventArgs e)
        {
            _diktovaniLimitTimer.Stop();
            var b = _tlacitkoDiktovaniAktivni;
            if (b == null) return;

            _diktovaniClickToggle = false;
            Stav("⏱ Nahrávání dosáhlo limitu 3 minut – automaticky je ukončuji a přepisuji.");
            ZastavADiktuj(b, autoStop: true);
        }
    }

}
