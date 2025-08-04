namespace QuietPrompt
{
    public class OverlayForm : Form
    {
        private readonly FlowLayoutPanel _menuPanel;
        private readonly RichTextBox _consoleBox;
        private readonly ComboBox _languageComboBox;

        // List of top 30 programming languages (can be adjusted as needed)
        private static readonly List<string> TopLanguages = new()
        {
            "C#", "T-SQL", "Python", "JavaScript", "Java", "C", "C++", "TypeScript", "Go", "Rust",
            "PHP", "Swift", "Kotlin", "Ruby", "Dart", "Scala", "Objective-C", "Shell", "PowerShell", "Perl",
            "R", "MATLAB", "Groovy", "Visual Basic", "Assembly", "Delphi", "Lua", "Haskell", "Elixir", "Julia"
        };

        public OverlayForm()
        {
            // Get primary screen bounds
            var screen = Screen.PrimaryScreen;
            int overlayHeight = (int)(screen.Bounds.Height * 0.35);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, overlayHeight);
            Location = new Point(screen.Bounds.X, screen.Bounds.Y);
            TopMost = true;
            BackColor = Color.Black;
            Opacity = 0.85;
            ShowInTaskbar = false;

            // Menu panel (horizontal row of buttons)
            _menuPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 49,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.FromArgb(220, 220, 220, 220),
                Padding = new Padding(8, 4, 8, 4),
                AutoSize = false
            };

            // Add language dropdown
            _languageComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 160,
                Font = new Font("Segoe UI", 10),
                Margin = new Padding(4, 8, 12, 8)
            };
            _languageComboBox.Items.AddRange(TopLanguages.ToArray());
            _languageComboBox.SelectedIndex = 0; // Default to first language
            _languageComboBox.SelectedIndexChanged += LanguageComboBox_SelectedIndexChanged;
            _menuPanel.Controls.Add(_languageComboBox);

            AddMenuButton("Send All to LLM\r\nCTRL-F12", (_, __) => Program.SendAllTranscriptsToLlm());
            AddMenuButton("Screenshot + OCR (Monitor 2)\r\nCTRL-F11", (_, __) => Program.CaptureSecondMonitorAndAppendOcr());
            AddMenuButton("Snip + OCR\r\nCTRL-F10", (_, __) => Program.CaptureSnipAndAppendOcr());
            AddMenuButton("Toggle Mic Transcription\r\nCTRL-F9", (_, __) => Program.ToggleMicTranscription());
            AddMenuButton("Add User Text\r\nCTRL-F8", (_, __) => Program.PromptAndStoreUserInput());
            AddMenuButton("Clear All\r\nCTRL-F7", (_, __) => Program.ClearAllTranscripts());
            AddMenuButton("Toggle Overlay\r\nCTRL-ALT-F12", (_, __) => this.Hide());
            AddMenuButton("Exit", (_, __) => Application.Exit());

            // Console area
            _consoleBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0)
            };
            Controls.Add(_consoleBox);
            Controls.Add(_menuPanel);

            Padding = new Padding(0);
        }

        private void AddMenuButton(string text, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(4, 0, 4, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 240, 240, 240),
                ForeColor = Color.Black
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            _menuPanel.Controls.Add(btn);
        }

        // Handle language selection change
        private void LanguageComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            string selectedLanguage = _languageComboBox.SelectedItem?.ToString() ?? "C#";
            // You can store this selection in a static/global variable or pass it to Program as needed
            Program.SetLanguage(selectedLanguage);
        }

        // Call this method to write messages to the overlay console
        public void WriteLine(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(WriteLine), message);
                return;
            }
            _consoleBox.AppendText(message + Environment.NewLine);
            _consoleBox.SelectionStart = _consoleBox.TextLength;
            _consoleBox.ScrollToCaret();
        }
    }
}