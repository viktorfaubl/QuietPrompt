namespace QuietPrompt
{
    internal class TrayMenu
    {
        private static NotifyIcon? _trayIcon;
        private static Thread? _trayThread;

        internal static NotifyIcon StartTrayMenu()
        {
            // Use the SIID_SERVER stock icon for the tray icon
            var trayIconImage = SystemIcons.GetStockIcon(StockIconId.Server, StockIconOptions.Default);

            var trayIcon = new NotifyIcon
            {
                Icon = trayIconImage,
                Text = "QuietPrompt",
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };

            var menu = trayIcon.ContextMenuStrip;
            menu.Items.Add("Send All to LLM", null, (_, __) => Program.SendAllTranscriptsToLlm());
            menu.Items.Add("Screenshot + OCR (Monitor 2)", null, (_, __) => Program.CaptureSecondMonitorAndAppendOcr());
            menu.Items.Add("Snip + OCR", null, (_, __) => Program.CaptureSnipAndAppendOcr());
            menu.Items.Add("Toggle Mic Transcription", null, (_, __) => Program.ToggleMicTranscription());
            menu.Items.Add("Add User Text", null, (_, __) => Program.PromptAndStoreUserInput());
            menu.Items.Add("Clear All", null, (_, __) => Program.ClearAllTranscripts());
            menu.Items.Add("Toggle Overlay\r\nCTRL-ALT-F12", null, (_, __) => Program.SafeToggleOverlay());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, __) =>
            {
                trayIcon.Visible = false;
                Application.Exit();
            });

            return trayIcon;
        }
    }
}
