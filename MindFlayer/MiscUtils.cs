using System;
using System.Threading;
using System.Windows.Forms;

namespace QuietPrompt
{
    internal static class MiscUtils
    {
        // Miscellaneous utility logic will be moved here from Program.cs
        public static void SetLanguage(string selectedLanguage)
        {
            Program._language = selectedLanguage;
            OverlayConsole.SafeWriteLine($"Language changed to: {selectedLanguage}");
        }

        public static void ClearAllTranscripts()
        {
            Program.OcrResults.Clear();
            Program.MicTranscripts.Clear();
            Program.UserTextInputs.Clear();
            OverlayConsole.SafeWriteLine("All prompts cleared.");
        }

        public static void PromptAndStoreUserInput()
        {
            string? input = null;
            var thread = new Thread(() =>
            {
                using var form = new MultilineInputForm(
                    "Enter your text to add to the prompt:",
                    "User Input"
                );
                if (form.ShowDialog() == DialogResult.OK)
                {
                    input = form.InputText;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (!string.IsNullOrWhiteSpace(input))
            {
                Program.UserTextInputs.Add(input.Trim());
                OverlayConsole.SafeWriteLine("User input stored.");
            }
            else
            {
                OverlayConsole.SafeWriteLine("No input entered.");
            }
        }
    }
}