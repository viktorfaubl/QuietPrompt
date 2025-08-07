using System;
using System.Text;
using System.Threading;
using Whisper.net;
using NAudio.Wave;

namespace QuietPrompt
{
    internal static class MicTranscriber
    {
        private static bool _isTranscribing = false;
        private static StringBuilder? _transcriptionBuilder;
        private static WhisperFactory? _whisperFactory;
        private static object _transcribeLock = new();
        private static ManualResetEvent? _transcriptionDoneEvent;

        public static void ToggleMicTranscription(List<string> micTranscripts, string whisperModelPath)
        {
            lock (_transcribeLock)
            {
                if (!_isTranscribing)
                {
                    StartMicTranscription(whisperModelPath);
                }
                else
                {
                    StopMicTranscriptionAndStore(micTranscripts);
                }
            }
        }

        private static void StartMicTranscription(string whisperModelPath)
        {
            _isTranscribing = true;
            _transcriptionBuilder = new StringBuilder();
            _transcriptionDoneEvent = new ManualResetEvent(false);

            if (_whisperFactory == null)
            {
                _whisperFactory = WhisperFactory.FromPath(whisperModelPath, WhisperFactoryOptions.Default);
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
                    OverlayConsole.SafeWriteLine($"Captured {audio.Length} bytes of audio.");

                    if (audio.Length < 32000)
                    {
                        OverlayConsole.SafeWriteLine("Audio too short for transcription.");
                        return;
                    }

                    var floatAudio = new float[audio.Length / 2];
                    for (int i = 0; i < floatAudio.Length; i++)
                        floatAudio[i] = BitConverter.ToInt16(audio, i * 2) / 32768f;

                    var builder = _whisperFactory.CreateBuilder();
                    builder.WithSegmentEventHandler(segment =>
                    {
                        OverlayConsole.SafeWriteLine($"[Whisper] Segment: '{segment.Text}'");
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
                    OverlayConsole.SafeWriteLine($"[Transcription error] {ex.Message}");
                }
                finally
                {
                    _transcriptionDoneEvent?.Set();
                }
            });

            OverlayConsole.SafeWriteLine("Microphone transcription started. Press Ctrl+F9 again to stop.");
        }

        private static void StopMicTranscriptionAndStore(List<string> micTranscripts)
        {
            _isTranscribing = false;
            _transcriptionDoneEvent?.WaitOne();

            var transcript = _transcriptionBuilder?.ToString();
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                micTranscripts.Add(transcript.Trim());
                OverlayConsole.SafeWriteLine("Transcription complete. Transcript stored. Transcript: ");
                OverlayConsole.SafeWriteLine(transcript.Trim());
            }
            else
            {
                OverlayConsole.SafeWriteLine("No transcription captured.");
            }
        }
    }
}