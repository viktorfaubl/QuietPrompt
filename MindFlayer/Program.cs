using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Whisper.net;
using NAudio.Wave;
using System.IO.Compression;
using Azure.AI.Vision.ImageAnalysis;
using Azure;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace QuietPrompt
{
    internal class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        public static string _language = "C#";
        public static readonly List<string> OcrResults = new();
        public static readonly List<string> MicTranscripts = new();
        public static readonly List<string> UserTextInputs = new();

        [STAThread]
        static void Main(string[] args)
        {
            AllocConsole();
            ResourceManager.EnsureResources().Wait();
            var _companionProcess = StartLlama(
                ResourceManager.LlamaExePath,
                $"--model \"{ResourceManager.LlmModelPath}\" " +
                "--port 11434 " +
                "--n-gpu-layers 30 " +
                "--log-disable " +
                "--ctx-size 8192 " +
                "--no-webui "
            );
            WaitForQwenReady().GetAwaiter().GetResult();
            FreeConsole();
            OverlayConsole.ToggleOverlay();
            var hotkeyThread = new Thread(HotkeyManager.HotkeyMessageLoop)
            {
                IsBackground = true,
                Name = "HotkeyMessageLoop"
            };
            hotkeyThread.SetApartmentState(ApartmentState.STA);
            hotkeyThread.Start();
            Application.EnableVisualStyles();
            var trayIcon = TrayMenu.StartTrayMenu();
            Application.ApplicationExit += OnApplicationExit;
            Application.Run();
            trayIcon.Dispose();
        }

        private static void OnApplicationExit(object? sender, EventArgs e)
        {
            HotkeyManager.UnregisterAllHotkeys();
            KillAllLlamaServerProcesses();
        }

        public static void KillAllLlamaServerProcesses()
        {
            try
            {
                var processes = Process.GetProcessesByName("llama-server");
                foreach (var proc in processes)
                {
                    try
                    {
                        if (!proc.HasExited)
                        {
                            proc.Kill(true);
                        }
                    }
                    catch (Exception e)
                    {
                        OverlayConsole.SafeWriteLine($"Failed to kill process {proc.ProcessName} (PID: {proc.Id}): {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                OverlayConsole.SafeWriteLine($"[LLMManager] Error killing llama-server processes: {e.Message}");
            }
        }

        private static async Task WaitForQwenReady()
        {
            Console.WriteLine("Loading Qwen, please wait...");
            bool isReady = false;
            char[] spinner = new[] { '|', '/', '-', '\\' };
            int spinnerIndex = 0;
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(600) };
            while (!isReady)
            {
                try
                {
                    var payload = new StringContent(
                        "{\"model\": \"Qwen\", \"prompt\": \"ping\", \"n_predict\": 1}",
                        Encoding.UTF8,
                        "application/json"
                    );
                    var response = await httpClient.PostAsync("http://127.0.0.1:11434/completion", payload);
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("content", out var contentProp) && !string.IsNullOrWhiteSpace(contentProp.ToString()))
                    {
                        isReady = true;
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("LLM ERROR, exiting.");
                    Environment.Exit(1);
                }
                Console.Write($"\rLoading Qwen... {spinner[spinnerIndex++ % spinner.Length]}");
                await Task.Delay(1000);
            }
            Console.WriteLine("Qwen ping sucessfull. Ready.");
        }

        private static Process StartLlama(string exePath, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var process = new Process { StartInfo = psi };
            process.Start();
            return process;
        }

        // The following methods are now wrappers to call the new modules
        public static void SendAllTranscriptsToLlm() => LlmClient.SendAllTranscriptsToLlm(OcrResults, MicTranscripts, UserTextInputs, _language);
        public static void CaptureSecondMonitorAndAppendOcr() => OcrManager.CaptureSecondMonitorAndAppendOcr(OcrResults);
        public static void CaptureSnipAndAppendOcr() => OcrManager.CaptureSnipAndAppendOcr(OcrResults);
        public static void ToggleMicTranscription() => MicTranscriber.ToggleMicTranscription(MicTranscripts, ResourceManager.WhisperModelPath);
        public static void SetLanguage(string selectedLanguage) => MiscUtils.SetLanguage(selectedLanguage);
        public static void ClearAllTranscripts() => MiscUtils.ClearAllTranscripts();
        public static void PromptAndStoreUserInput() => MiscUtils.PromptAndStoreUserInput();
        public static void SafeWriteLine(string message) => OverlayConsole.SafeWriteLine(message);
        public static void SafeToggleOverlay() => OverlayConsole.SafeToggleOverlay();
    }
}
