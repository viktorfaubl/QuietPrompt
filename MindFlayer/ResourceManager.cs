using System;
using System.IO;
using System.IO.Compression;

namespace QuietPrompt
{
    internal static class ResourceManager 
    {
        public static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuietPrompt"
        );
        public static readonly string LlmModelPath = Path.Combine(AppDataDir, "Qwen3-Coder-30B-A3B-Instruct-Q4_K_M.gguf");
        public static readonly string WhisperModelPath = Path.Combine(AppDataDir, "ggml-base-q8_0.bin");
        public static readonly string LlamaDir = Path.Combine(AppDataDir, "llama");
        public static readonly string LlamaExePath = Path.Combine(LlamaDir, "llama-server.exe");

        public static async Task EnsureResources()
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
    }
}