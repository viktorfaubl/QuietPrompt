namespace QuietPrompt
{
    public class MultilineInputForm : Form
    {
        private readonly TextBox _textBox;
        private readonly Button _okButton;
        private readonly Button _cancelButton;

        public string InputText => _textBox.Text;

        public MultilineInputForm(string prompt, string title)
        {
            Text = title;
            Width = 500;
            Height = 350;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var label = new Label
            {
                Text = prompt,
                AutoSize = true,
                Top = 15,
                Left = 15
            };
            Controls.Add(label);

            _textBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Left = 15,
                Top = label.Bottom + 10,
                Width = ClientSize.Width - 30,
                Height = ClientSize.Height - 100,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(_textBox);

            _okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Left = ClientSize.Width - 180,
                Width = 75,
                Top = _textBox.Bottom + 10,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            Controls.Add(_okButton);

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Left = ClientSize.Width - 95,
                Width = 75,
                Top = _textBox.Bottom + 10,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            Controls.Add(_cancelButton);

            AcceptButton = _okButton;
            CancelButton = _cancelButton;
        }
    }
}