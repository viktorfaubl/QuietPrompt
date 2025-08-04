# QuietPrompt



\*\*Local-first productivity tool for coding with AI from your screen, mic, or text — no cloud, no noise.\*\*



QuietPrompt captures what you see or say, runs it through OCR/transcription, sends it to your local LLM (like Qwen, Mistral), and pastes back a solution. It's like Copilot, but fully offline and under your control.



---



\## ✨ Features



\- \*\*🖼️ Screen OCR\*\* — Capture full or partial screen and extract text via Tesseract or Azure OCR.

\- \*\*🎙️ Voice Input\*\* — Dictate tasks to your LLM using Whisper.

\- \*\*⌨️ Text Input\*\* — Type prompts manually when needed.

\- \*\*🧠 LLM Integration\*\* — Works with any local llama-server-compatible GGUF model.

\- \*\*📋 Clipboard Return\*\* — Outputs responses directly to clipboard.

\- \*\*🖥️ Overlay UI\*\* — Clean floating window with input log and status display.

\- \*\*🪟 System Tray\*\* — Minimal UI with keybindings and status toggles.

\- \*\*🔐 Fully local\*\* — Choose your own models, OCR engines, and endpoints.



---



\## 🎛️ Default Keybindings



| Key         | Action                             |

|-------------|------------------------------------|

| `Ctrl+F12`  | Send to LLM                        |

| `Ctrl+F11`  | Screenshot full screen + OCR       |

| `Ctrl+F10`  | Snip selection + OCR               |

| `Ctrl+F9`   | Voice dictation (Whisper)          |

| `Ctrl+F8`   | Manual prompt input                |

| `Ctrl+F7`   | Clear state / history              |



---



\## 🚀 Installation



1\. \*\*Download the release\*\*

2\. \*\*Double click the `.exe`\*\* (starts in tray and overlay)

3\. \*\*First launch will download missing models\*\* (optional)

4\. \*\*Use hotkeys or tray menu to interact\*\*

\*

5\. \*\*Manual model download, if you want to pre-download the models used:

&nbsp;   - place it to the app root:

&nbsp;	https://huggingface.co/unsloth/Qwen3-Coder-30B-A3B-Instruct-GGUF/resolve/main/Qwen3-Coder-30B-A3B-Instruct-Q4\_K\_M.gguf?download=true

&nbsp;	https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base-q8\_0.bin?download=true

&nbsp;   - place it under llama folder and unzip:

&nbsp;	https://github.com/ggml-org/llama.cpp/releases/download/b6081/llama-b6081-bin-win-cuda-12.4-x64.zip

&nbsp;	https://github.com/ggml-org/llama.cpp/releases/download/b6081/cudart-llama-bin-win-cuda-12.4-x64.zip



> Requires .NET 6+, 32GB+ RAM, and CUDA for LLM acceleration, recommended minimum 3060 Ti 8GB VRAM.



---



\## 🧠 Recommended Models



\- \*\*LLM\*\*: Qwen 30B A3B Q4\_K\_M (`gguf`, llama-server compatible)

\- \*\*OCR\*\*: Tesseract (bundled) or Azure Vision (via API key)

\- \*\*Transcription\*\*: Whisper (`ggml-base-q8\_0` or above)



---



🧭 Origin Story



It started with a frustration.



I was working on a project in the banking sector — isolated network, no internet access, strict software policies. I wanted to use GitHub Copilot to speed up development, but it couldn’t integrate with the older version of Visual Studio we were stuck with. Even if it could, cloud access was off-limits.



So I did what any developer does when boxed in: I built my own tool.



QuietPrompt was born from that constraint — a fully offline, hotkey-driven assistant that could understand what I was working on and help me code faster. First from screenshots. Then from my voice. Now, from anything I throw at it.



It started as a weekend experiment, but quickly became something more useful than I expected. No tracking. No latency. No fees. Just local AI helping me focus.



Now I’m sharing it in case others find themselves in the same situation — or just want full control over how their code is written.



---



\## 🛠️ Planned Features (v2)



\- Editable prompt before send

\- Configurable OCR languages

\- Online endpoint toggle (OpenAI, Claude, Azure)

\- Custom screen selection

\- Per-language AI response tuning



---



\## License

QuietPrompt is licensed for personal and educational use only.  
Commercial or corporate use is prohibited without written permission.  
See [LICENSE.md](LICENSE.md) for details.

---



\## 🤖 Why?



Copilot is great, but expensive, cloud-tied, and... blind.  

QuietPrompt sees what you see — and responds in full privacy.



---



\## 📸 Screenshots



Soon



---



\## 🔗 Links



\- \[Download latest release](https://github.com/viktorfaubl/quietprompt/releases)

\- \[Project page](https://github.com/viktorfaubl/quietprompt)

\- \[Discussion / Issues](https://github.com/viktorfaubl/quietprompt/issues)



---



\## 🙋 FAQ



\*\*Q:\*\* Is this a Copilot replacement?  

\*\*A:\*\* It's not an IDE plugin — it works alongside anything by pulling in prompts from \*your screen, mic or clipboard\*.



\*\*Q:\*\* Does it work offline?  

\*\*A:\*\* Yes. Models run locally. OCR is also local.



\*\*Q:\*\* Can I customize models or prompts?  

\*\*A:\*\* Absolutely. It’s open-ended. Add any LLM or workflow behind the scenes.



---



\## 👏 Credits



Built by \[Viktor Faubl](https://linkedin.com/in/...), powered by:



\- \[llama.cpp / llama-server](https://github.com/ggerganov/llama.cpp)

\- \[Whisper.cpp](https://github.com/ggerganov/whisper.cpp)

\- \[Tesseract OCR](https://github.com/tesseract-ocr/tesseract)



---



