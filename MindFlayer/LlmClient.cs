using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace QuietPrompt
{
    internal static class LlmClient
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(600) };

        public static void SendAllTranscriptsToLlm(List<string> ocrResults, List<string> micTranscripts, List<string> userTextInputs, string language)
        {
            if (ocrResults.Count == 0 && micTranscripts.Count == 0 && userTextInputs.Count == 0)
            {
                OverlayConsole.SafeWriteLine("No OCR, mic transcripts, or user inputs to send.");
                return;
            }

            var allText = new List<string>();
            if (ocrResults.Count > 0)
                allText.AddRange(ocrResults);
            if (micTranscripts.Count > 0)
                allText.AddRange(micTranscripts);
            if (userTextInputs.Count > 0)
                allText.AddRange(userTextInputs);

            string concatenated = string.Join("\n", allText);
            OverlayConsole.SafeWriteLine("All transcripts sent. Qwen is thinking, please wait (on a 3060 Ti with 8GB RAM and 32GB RAM it can be around 1 min / screenshot)");
            SendOcrResultToLlm(concatenated, language);
        }

        private static void SendOcrResultToLlm(string ocrText, string language)
        {
            string promptJson = BuildPromptJson(ocrText, language);

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
                    OverlayConsole.SafeWriteLine("LLM response:");
                    OverlayConsole.SafeWriteLine(llmResponse);

                    var textToCopy = llmResponse ?? string.Empty;
                    var thread = new Thread(() => Clipboard.SetText(textToCopy));
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();

                    System.Media.SystemSounds.Asterisk.Play();
                }
                else
                {
                    OverlayConsole.SafeWriteLine("No 'content' property found in LLM response.");
                }
            }
            catch (Exception e)
            {
                OverlayConsole.SafeWriteLine($"[LLM ERROR] {e.Message}");
            }
        }

        public static string BuildPromptJson(string userInput, string language)
        {
            string prompt = BuildQwenPrompt($"You are a senior {language} developer." +
                                               "Solve the following problem using the most efficient algorithm you know." +
                                               "Time complexity should be optimal (O(n) if possible)." +
                                               $"Return only clean, working {language} code.", userInput);

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
    }
}