using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Whisper.net;
using NAudio.Wave;
using System.IO.Compression;

//Codename: MindFlyer
namespace QuietPrompt
{
    internal class Program
    {
        #region Constants & Win32 Interop

        private const int MOD_CONTROL = 0x0002;
        private const int MOD_ALT = 0x0001; // Add this line
        private const int VK_F12 = 0x7B;
        private const int VK_F11 = 0x7A;
        private const int VK_F10 = 0x79;
        private const int VK_F9 = 0x78;
        private const int VK_F8 = 0x77;
        private const int VK_F7 = 0x76;
        private const int WM_HOTKEY = 0x0312;

        private static string _language = "C#";

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(nint hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(nint hWnd, int id);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public nint hwnd;
            public uint message;
            public nint wParam;
            public nint lParam;
            public uint time;
            public Point pt;
        }

        #endregion

        #region State

        private static readonly List<string> OcrResults = new();
        private static readonly List<string> MicTranscripts = new();
        private static readonly List<string> UserTextInputs = new();

        private static Process? _companionProcess;
        private static bool _isTranscribing = false;
        private static StringBuilder? _transcriptionBuilder;
        private static WhisperFactory? _whisperFactory;
        private static object _transcribeLock = new();
        private static ManualResetEvent? _transcriptionDoneEvent;

        private enum OcrEngine { Tesseract }
        private static OcrEngine SelectedOcrEngine = OcrEngine.Tesseract;

        //for local llm-server queries
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(600) };

        // Add: Centralized app data directory for models and binaries
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuietPrompt"
        );
        private static readonly string LlmModelPath = Path.Combine(AppDataDir, "Qwen3-Coder-30B-A3B-Instruct-Q4_K_M.gguf");
        private static readonly string WhisperModelPath = Path.Combine(AppDataDir, "ggml-base-q8_0.bin");
        private static readonly string LlamaDir = Path.Combine(AppDataDir, "llama");
        private static readonly string LlamaExePath = Path.Combine(LlamaDir, "llama-server.exe");

        #endregion

        #region Main Entry

        [STAThread]
        static void Main(string[] args)
        {
            // Allocate a console window
            AllocConsole();

            EnsureResources().Wait();

            _companionProcess = StartLlama(LlamaExePath, $"--model \"{LlmModelPath}\" --port 11434 --n-gpu-layers 30 --log-disable");

            WaitForQwenReady().GetAwaiter().GetResult();
            // Free (hide) the console window
            FreeConsole();

            ToggleOverlay();

            // Run hotkey message loop in a background thread
            var hotkeyThread = new Thread(HotkeyMessageLoop)
            {
                IsBackground = true,
                Name = "HotkeyMessageLoop"
            };
            hotkeyThread.SetApartmentState(ApartmentState.STA);
            hotkeyThread.Start();

            Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);

            var trayIcon = TrayMenu.StartTrayMenu();

            Application.ApplicationExit += OnApplicationExit;
            Application.Run(); // This keeps the app running for the tray menu

            trayIcon.Dispose(); // Clean up on exit
        }
        

        private static void OnApplicationExit(object? sender, EventArgs e)
        {
            UnregisterAllHotkeys();
            if (_companionProcess != null && !_companionProcess.HasExited)
            {
                _companionProcess.Kill();
                _companionProcess.Dispose();
            }

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
                        SafeWriteLine($"Failed to kill process {proc.ProcessName} (PID: {proc.Id}): {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                SafeWriteLine($"[LLMManager] Error killing llama-server processes: {e.Message}");
            }
        }

        #endregion

        #region Console Helpers

        private static async Task WaitForQwenReady()
        {
            Console.WriteLine("Loading Qwen, please wait...");

            bool isReady = false;
            char[] spinner = new[] { '|', '/', '-', '\\' };
            int spinnerIndex = 0;

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

        #endregion

        #region Resource/Model Download

        private static async Task EnsureResources()
        {
            Directory.CreateDirectory(AppDataDir);
            Directory.CreateDirectory(LlamaDir);

            // LLM model
            string modelUrl = "https://huggingface.co/unsloth/Qwen3-Coder-30B-A3B-Instruct-GGUF/resolve/main/Qwen3-Coder-30B-A3B-Instruct-Q4_K_M.gguf?download=true";
            if (!File.Exists(LlmModelPath))
            {
                Console.WriteLine("Model file not found. Downloading...");
                await DownloadFileWithProgress(modelUrl, LlmModelPath);
                Console.WriteLine("\nDownload complete.");
            }
            else
            {
                Console.WriteLine("Model file found.");
            }

            // Whisper model
            string whisperModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base-q8_0.bin?download=true";
            if (!File.Exists(WhisperModelPath))
            {
                Console.WriteLine("Whisper model not found. Downloading...");
                await DownloadFileWithProgress(whisperModelUrl, WhisperModelPath);
                Console.WriteLine("\nWhisper model download complete.");
            }
            else
            {
                Console.WriteLine("Whisper model found.");
            }

            // Llama server
            if (!File.Exists(LlamaExePath))
            {
                Console.WriteLine("llama-server.exe not found. Downloading and extracting required binaries...");
                string llamaZipUrl = "https://github.com/ggml-org/llama.cpp/releases/download/b6081/llama-b6081-bin-win-cuda-12.4-x64.zip";
                string cudartZipUrl = "https://github.com/ggml-org/llama.cpp/releases/download/b6081/cudart-llama-bin-win-cuda-12.4-x64.zip";
                await DownloadAndExtractZip(llamaZipUrl, LlamaDir);
                await DownloadAndExtractZip(cudartZipUrl, LlamaDir);
                Console.WriteLine("llama-server.exe and dependencies downloaded and extracted.");
            }
            else
            {
                Console.WriteLine("llama-server.exe found.");
            }
        }

        private static async Task DownloadFileWithProgress(string url, string destinationPath)
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;
            var lastProgress = -1;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;
                if (canReportProgress)
                {
                    int progress = (int)(totalRead * 100 / totalBytes);
                    if (progress != lastProgress)
                    {
                        Console.Write($"\rDownloading: {progress}%");
                        lastProgress = progress;
                    }
                }
            }
        }

        private static async Task DownloadAndExtractZip(string url, string extractPath)
        {
            string tempZip = Path.GetTempFileName();
            try
            {
                await DownloadFileWithProgress(url, tempZip);
                ZipFile.ExtractToDirectory(tempZip, extractPath, overwriteFiles: true);
            }
            finally
            {
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
            }
        }

        #endregion

        #region Hotkey Registration & Message Loop

        private const int HOTKEY_ID_F12 = 9000;
        private const int HOTKEY_ID_F11 = 9001;
        private const int HOTKEY_ID_F10 = 9002;
        private const int HOTKEY_ID_F9  = 9003;
        private const int HOTKEY_ID_F8  = 9004;
        private const int HOTKEY_ID_F7  = 9005;
        private const int HOTKEY_ID_CTRL_ALT_F12 = 9006;

        private static void RegisterHotkeysOrExit()
        {
            if (!RegisterHotKey(nint.Zero, HOTKEY_ID_F12, MOD_CONTROL, VK_F12) ||
                !RegisterHotKey(nint.Zero, HOTKEY_ID_F11, MOD_CONTROL, VK_F11) ||
                !RegisterHotKey(nint.Zero, HOTKEY_ID_F10, MOD_CONTROL, VK_F10) ||
                !RegisterHotKey(nint.Zero, HOTKEY_ID_F9,  MOD_CONTROL, VK_F9)  ||
                !RegisterHotKey(nint.Zero, HOTKEY_ID_F8,  MOD_CONTROL, VK_F8)  ||
                !RegisterHotKey(nint.Zero, HOTKEY_ID_F7,  MOD_CONTROL, VK_F7)  ||
                !RegisterHotKey(nint.Zero, HOTKEY_ID_CTRL_ALT_F12, MOD_CONTROL | MOD_ALT, VK_F12))
            {
                SafeWriteLine("Failed to register hotkey(s).");
                Environment.Exit(1);
            }
        }

        private static void UnregisterAllHotkeys()
        {
            UnregisterHotKey(nint.Zero, HOTKEY_ID_F12);
            UnregisterHotKey(nint.Zero, HOTKEY_ID_F11);
            UnregisterHotKey(nint.Zero, HOTKEY_ID_F10);
            UnregisterHotKey(nint.Zero, HOTKEY_ID_F9);
            UnregisterHotKey(nint.Zero, HOTKEY_ID_F8);
            UnregisterHotKey(nint.Zero, HOTKEY_ID_F7);
            UnregisterHotKey(nint.Zero, HOTKEY_ID_CTRL_ALT_F12);
        }

        private static void HotkeyMessageLoop()
        {
            RegisterHotkeysOrExit();

            while (true)
            {
                GetMessage(out MSG msg, nint.Zero, 0, 0);
                if (msg.message == WM_HOTKEY)
                {
                    switch (msg.wParam.ToInt32())
                    {
                        case HOTKEY_ID_F12:
                            SafeWriteLine("Ctrl+F12 pressed!");
                            SendAllTranscriptsToLlm();
                            break;
                        case HOTKEY_ID_F11:
                            SafeWriteLine("Ctrl+F11 pressed!");
                            CaptureSecondMonitorAndAppendOcr();
                            break;
                        case HOTKEY_ID_F10:
                            SafeWriteLine("Ctrl+F10 pressed!");
                            CaptureSnipAndAppendOcr();
                            break;
                        case HOTKEY_ID_F9:
                            SafeWriteLine("Ctrl+F9 pressed!");
                            ToggleMicTranscription();
                            break;
                        case HOTKEY_ID_F8:
                            SafeWriteLine("Ctrl+F8 pressed!");
                            PromptAndStoreUserInput();
                            break;
                        case HOTKEY_ID_F7:
                            SafeWriteLine("Ctrl+F7 pressed!");
                            ClearAllTranscripts();
                            break;
                        case HOTKEY_ID_CTRL_ALT_F12:
                            // Handle CTRL+ALT+F12 here
                            SafeToggleOverlay();
                            break;
                    }
                }
            }
        }

        private static OverlayForm? _overlayForm;
        private static void ToggleOverlay()
        {
            if (_overlayForm == null || _overlayForm.IsDisposed)
            {
                _overlayForm = new OverlayForm();
                // Optional: Write a welcome message
                SafeWriteLine("Overlay started. Console output will appear here.");
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

        private static void SafeWriteLine(string message)
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

        internal static void SafeToggleOverlay()
        {
            if (_overlayForm == null || _overlayForm.IsDisposed)
            {
                if (Application.OpenForms.Count > 0)
                {
                    var mainForm = Application.OpenForms[0];
                    mainForm.Invoke(new Action(() =>
                    {
                        _overlayForm = new OverlayForm();
                        SafeWriteLine("Overlay started. Console output will appear here.");
                        _overlayForm.Show();
                    }));
                }
                else
                {
                    // Fallback: create and show on current thread (should only happen on startup)
                    _overlayForm = new OverlayForm();
                    SafeWriteLine("Overlay started. Console output will appear here.");
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

        #endregion

        #region Llama Process

        private static Process StartLlama(string exePath, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true, // Start hidden
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var process = new Process { StartInfo = psi };
            process.Start();

            //process.OutputDataReceived += (sender, e) =>
            //{
            //    if (!string.IsNullOrEmpty(e.Data))
            //        Console.WriteLine("[LLAMA OUT] " + e.Data);
            //};
            //process.ErrorDataReceived += (sender, e) =>
            //{
            //    if (!string.IsNullOrEmpty(e.Data))
            //        Console.WriteLine("[LLAMA OUTE] " + e.Data);
            //};
            //process.BeginOutputReadLine();
            //process.BeginErrorReadLine();

            return process;
        }

        #endregion

        #region OCR

        internal static void CaptureSecondMonitorAndAppendOcr()
        {
            var screens = Screen.AllScreens;
            if (screens.Length < 2)
            {
                SafeWriteLine("Second monitor not detected.");
                return;
            }

            var bounds = screens[1].Bounds;
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            }
            using var adjustedBitmap = AdjustContrastAndBrightness(bitmap, 2.0f, 0.5f);

            string screenshotsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Screenshots"
            );
            Directory.CreateDirectory(screenshotsDir);

            string fileName = $"screenshot_monitor2_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(screenshotsDir, fileName);
            adjustedBitmap.Save(filePath, ImageFormat.Png);
            SafeWriteLine($"Screenshot saved: {filePath}");

            string ocrText = AnalyzeImageWithOcr(filePath);
            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                OcrResults.Add(ocrText.Trim());
                SafeWriteLine("OCR result appended:");
                SafeWriteLine(ocrText.Trim());
            }
        }

        public static Bitmap AdjustContrastAndBrightness(Bitmap image, float contrast, float brightness)
        {
            // contrast: 1.0 = no change, >1.0 = higher contrast, <1.0 = lower contrast
            // brightness: 0 = black, 1.0 = original, >1.0 = brighter
            float t = 0.5f * (1.0f - contrast);

            float[][] ptsArray ={
                new float[] {contrast, 0, 0, 0, 0}, // scale red
                new float[] {0, contrast, 0, 0, 0}, // scale green
                new float[] {0, 0, contrast, 0, 0}, // scale blue
                new float[] {0, 0, 0, 1f, 0},       // don't change alpha
                new float[] {brightness + t, brightness + t, brightness + t, 0, 1}
            };

            var imageAttributes = new ImageAttributes();
            var colorMatrix = new ColorMatrix(ptsArray);
            imageAttributes.SetColorMatrix(colorMatrix);

            var result = new Bitmap(image.Width, image.Height);
            using (var g = Graphics.FromImage(result))
            {
                g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, imageAttributes);
            }
            return result;
        }
        
        internal static void CaptureSnipAndAppendOcr()
        {
            Rectangle rect = Rectangle.Empty;
            var thread = new Thread(() =>
            {
                using var overlay = new SnippingOverlay();
                if (overlay.ShowDialog() == DialogResult.OK)
                    rect = overlay.Selection;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (rect == Rectangle.Empty || rect.Width == 0 || rect.Height == 0)
            {
                SafeWriteLine("No area selected.");
                return;
            }

            using var bitmap = new Bitmap(rect.Width, rect.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(rect.Location, Point.Empty, rect.Size);
            }
            using var adjustedBitmap = AdjustContrastAndBrightness(bitmap, 3.0f, 0.5f);

            string screenshotsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Screenshots"
            );
            Directory.CreateDirectory(screenshotsDir);

            string fileName = $"screenshot_snip_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(screenshotsDir, fileName);
            adjustedBitmap.Save(filePath, ImageFormat.Png);
            SafeWriteLine($"Screenshot saved: {filePath}");

            string ocrText = AnalyzeImageWithOcr(filePath);
            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                OcrResults.Add(ocrText.Trim());
                SafeWriteLine("OCR result appended: ");
                SafeWriteLine(ocrText.Trim());
            }
        }

        private static string AnalyzeImageWithOcr(string filePath)
        {
            return SelectedOcrEngine switch
            {
                OcrEngine.Tesseract => AnalyzeImageWithTesseract(filePath),
                _ => throw new NotSupportedException("Unknown OCR engine")
            };
        }

        private static string AnalyzeImageWithTesseract(string filePath)
        {
            try
            {
                string tessDataPath = "tessdata";
                using var engine = new Tesseract.TesseractEngine(tessDataPath, "eng", Tesseract.EngineMode.Default);
                using var img = Tesseract.Pix.LoadFromFile(filePath);
                using var page = engine.Process(img);
                return page.GetText();
            }
            catch (Exception ex)
            {
                SafeWriteLine($"[Tesseract OCR ERROR] {ex.Message}");
                return string.Empty;
            }
        }

        #endregion

        #region LLM Communication

        internal static void SendAllTranscriptsToLlm()
        {
            if (OcrResults.Count == 0 && MicTranscripts.Count == 0 && UserTextInputs.Count == 0)
            {
                SafeWriteLine("No OCR, mic transcripts, or user inputs to send.");
                return;
            }

            var allText = new List<string>();
            if (OcrResults.Count > 0)
                allText.AddRange(OcrResults);
            if (MicTranscripts.Count > 0)
                allText.AddRange(MicTranscripts);
            if (UserTextInputs.Count > 0)
                allText.AddRange(UserTextInputs);

            string concatenated = string.Join("\n", allText);
            SafeWriteLine("All transcripts sent. Qwen is thinking, please wait (on a 3060 Ti with 8GB RAM and 32GB RAM it can be around 1 min / screenshot)");
            SendOcrResultToLlm(concatenated);
        }

        private static void SendOcrResultToLlm(string ocrText)
        {
            string promptJson = BuildPromptJson(ocrText);

            var content = new StringContent(
                promptJson,
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                var response = httpClient.PostAsync("http://localhost:11434/completion", content).GetAwaiter().GetResult();
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("content", out var contentElement))
                {
                    var llmResponse = contentElement.GetString();
                    SafeWriteLine("LLM response:");
                    SafeWriteLine(llmResponse);

                    var textToCopy = llmResponse ?? string.Empty;
                    var thread = new Thread(() => Clipboard.SetText(textToCopy));
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();

                    System.Media.SystemSounds.Asterisk.Play();
                }
                else
                {
                    SafeWriteLine("No 'content' property found in LLM response.");
                }
            }
            catch (Exception e)
            {
                SafeWriteLine($"[LLM ERROR] {e.Message}");
            }
        }

        public static string BuildPromptJson(string userInput)
        {
            string prompt = BuildQwenPrompt($"You are a senior {_language} developer." +
                                               "Solve the following problem using the most efficient algorithm you know." +
                                               "Time complexity should be optimal (O(n) if possible)." +
                                               $"Return only clean, working {_language} code.", userInput);

            var payload = new
            {
                prompt,
                temperature = 0.5
            };
            return JsonSerializer.Serialize(payload);
        }

        private static string BuildQwenPrompt(string systemPrompt, string userPrompt)
        {
            var sb = new StringBuilder();
            sb.Append("<|im_start|>system\n");
            sb.Append(systemPrompt);
            sb.Append("<|im_end|>\n");
            sb.Append("<|im_start|>user\n");
            sb.Append(userPrompt);
            sb.Append("<|im_end|>\n");
            sb.Append("<|im_start|>assistant\n");
            return sb.ToString();
        }

        #endregion

        #region Transcription

        internal static void ToggleMicTranscription()
        {
            lock (_transcribeLock)
            {
                if (!_isTranscribing)
                {
                    StartMicTranscription();
                }
                else
                {
                    StopMicTranscriptionAndStore();
                }
            }
        }

        private static void StartMicTranscription()
        {
            _isTranscribing = true;
            _transcriptionBuilder = new StringBuilder();
            _transcriptionDoneEvent = new ManualResetEvent(false);

            if (_whisperFactory == null)
            {
                _whisperFactory = WhisperFactory.FromPath(WhisperModelPath, WhisperFactoryOptions.Default);
            }

            var buffer = new List<byte>();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using var waveIn = new WaveInEvent();
                    waveIn.WaveFormat = new WaveFormat(16000, 1);

                    waveIn.DataAvailable += (s, e) =>
                    {
                        if (!_isTranscribing) return;
                        buffer.AddRange(e.Buffer.Take(e.BytesRecorded));
                    };

                    waveIn.StartRecording();

                    while (_isTranscribing)
                    {
                        Thread.Sleep(100);
                    }

                    waveIn.StopRecording();

                    var audio = buffer.ToArray();
                    SafeWriteLine($"Captured {audio.Length} bytes of audio.");

                    if (audio.Length < 32000)
                    {
                        SafeWriteLine("Audio too short for transcription.");
                        return;
                    }

                    var floatAudio = new float[audio.Length / 2];
                    for (int i = 0; i < floatAudio.Length; i++)
                        floatAudio[i] = BitConverter.ToInt16(audio, i * 2) / 32768f;

                    var builder = _whisperFactory.CreateBuilder();
                    builder.WithSegmentEventHandler(segment =>
                    {
                        SafeWriteLine($"[Whisper] Segment: '{segment.Text}'");
                        if (!string.IsNullOrWhiteSpace(segment.Text))
                        {
                            _transcriptionBuilder!.AppendLine(segment.Text);
                        }
                    });

                    using var processor = builder.Build();
                    processor.Process(floatAudio);
                }
                catch (Exception ex)
                {
                    SafeWriteLine($"[Transcription error] {ex.Message}");
                }
                finally
                {
                    _transcriptionDoneEvent?.Set();
                }
            });

            SafeWriteLine("Microphone transcription started. Press Ctrl+F9 again to stop.");
        }

        private static void StopMicTranscriptionAndStore()
        {
            _isTranscribing = false;
            _transcriptionDoneEvent?.WaitOne();

            var transcript = _transcriptionBuilder?.ToString();
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                MicTranscripts.Add(transcript.Trim());
                SafeWriteLine("Transcription complete. Transcript stored. Transcript: ");
                SafeWriteLine(transcript.Trim());

            }
            else
            {
                SafeWriteLine("No transcription captured.");
            }
        }

        #endregion

        #region Misc

        public static void SetLanguage(string selectedLanguage)
        {
            _language = selectedLanguage;
            SafeWriteLine($"Language changed to: {selectedLanguage}");
        }

        internal static void ClearAllTranscripts()
        {
            OcrResults.Clear();
            MicTranscripts.Clear();
            UserTextInputs.Clear();
            SafeWriteLine("All prompts cleared.");
        }

        internal static void PromptAndStoreUserInput()
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
                UserTextInputs.Add(input.Trim());
                SafeWriteLine("User input stored.");
            }
            else
            {
                SafeWriteLine("No input entered.");
            }
        }

        #endregion


    }
}
