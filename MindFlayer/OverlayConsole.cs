using System;
using System.Windows.Forms;

namespace QuietPrompt
{
    internal static class OverlayConsole
    {
        private static OverlayForm? _overlayForm;

        public static void ToggleOverlay()
        {
            if (_overlayForm == null || _overlayForm.IsDisposed)
            {
                _overlayForm = new OverlayForm();
                _overlayForm.WriteLine("Overlay started. Console output will appear here.");
                _overlayForm.Show();
            }
            else if (_overlayForm.Visible)
            {
                _overlayForm.Hide();
            }
            else
            {
                _overlayForm.Show();
            }
        }

        public static void SafeWriteLine(string message)
        {
            if (_overlayForm == null || _overlayForm.IsDisposed)
                return;

            if (_overlayForm.InvokeRequired)
            {
                _overlayForm.Invoke(new Action(() => _overlayForm.WriteLine(message)));
            }
            else
            {
                _overlayForm.WriteLine(message);
            }
        }

        public static void SafeToggleOverlay()
        {
            if (_overlayForm == null || _overlayForm.IsDisposed)
            {
                if (Application.OpenForms.Count > 0)
                {
                    var mainForm = Application.OpenForms[0];
                    mainForm.Invoke(new Action(() =>
                    {
                        _overlayForm = new OverlayForm();
                        _overlayForm.WriteLine("Overlay started. Console output will appear here.");
                        _overlayForm.Show();
                    }));
                }
                else
                {
                    _overlayForm = new OverlayForm();
                    _overlayForm.WriteLine("Overlay started. Console output will appear here.");
                    _overlayForm.Show();
                }
            }
            else if (_overlayForm.InvokeRequired)
            {
                _overlayForm.Invoke(new Action(() =>
                {
                    if (_overlayForm.Visible)
                        _overlayForm.Hide();
                    else
                        _overlayForm.Show();
                }));
            }
            else
            {
                if (_overlayForm.Visible)
                    _overlayForm.Hide();
                else
                    _overlayForm.Show();
            }
        }
    }
}