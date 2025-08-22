using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using Azure.AI.Vision.ImageAnalysis;
using Azure;

namespace QuietPrompt
{
    internal static class OcrManager
    {
        public enum OcrEngine { Azure, Tesseract }
        public static OcrEngine SelectedOcrEngine = OcrEngine.Tesseract;

        public static void CaptureSecondMonitorAndAppendOcr(List<string> ocrResults)
        {
            var screens = Screen.AllScreens;
            if (screens.Length < 2)
            {
                OverlayConsole.SafeWriteLine("Second monitor not detected.");
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
            OverlayConsole.SafeWriteLine($"Screenshot saved: {filePath}");

            string ocrText = AnalyzeImageWithOcr(filePath);
            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                ocrResults.Add(ocrText.Trim());
                OverlayConsole.SafeWriteLine("OCR result appended:");
                OverlayConsole.SafeWriteLine(ocrText.Trim());
            }
        }

        public static Bitmap AdjustContrastAndBrightness(Bitmap image, float contrast, float brightness)
        {
            float t = 0.5f * (1.0f - contrast);
            float[][] ptsArray ={
                new float[] {contrast, 0, 0, 0, 0},
                new float[] {0, contrast, 0, 0, 0},
                new float[] {0, 0, contrast, 0, 0},
                new float[] {0, 0, 0, 1f, 0},
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

        public static void CaptureSnipAndAppendOcr(List<string> ocrResults)
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
                OverlayConsole.SafeWriteLine("No area selected.");
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
            OverlayConsole.SafeWriteLine($"Screenshot saved: {filePath}");

            string ocrText = AnalyzeImageWithOcr(filePath);
            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                ocrResults.Add(ocrText.Trim());
                OverlayConsole.SafeWriteLine("OCR result appended: ");
                OverlayConsole.SafeWriteLine(ocrText.Trim());
            }
        }

        private static string AnalyzeImageWithOcr(string filePath)
        {
            return SelectedOcrEngine switch
            {
                OcrEngine.Azure => AnalyzeImageWithAzureOcr(filePath),
                OcrEngine.Tesseract => AnalyzeImageWithTesseract(filePath),
                _ => throw new NotSupportedException("Unknown OCR engine")
            };
        }

        private static string AnalyzeImageWithAzureOcr(string filePath)
        {
            string endpoint = "https://<YourEndpoint>.cognitiveservices.azure.com/";
            string key = "<YourKey>";
            var client = new ImageAnalysisClient(
                new Uri(endpoint),
                new AzureKeyCredential(key)
            );
            using var imageStream = File.OpenRead(filePath);
            var result = client.Analyze(
                BinaryData.FromStream(imageStream),
                VisualFeatures.Read
            );
            var ocrText = new StringBuilder();
            if (result.Value != null && result.Value.Read.Blocks != null)
            {
                foreach (var block in result.Value.Read.Blocks)
                {
                    foreach (var line in block.Lines)
                    {
                        ocrText.AppendLine(line.Text);
                    }
                }
                return ocrText.ToString();
            }
            else
            {
                OverlayConsole.SafeWriteLine("No text found.");
                return string.Empty;
            }
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
                OverlayConsole.SafeWriteLine($"[Tesseract OCR ERROR] {ex.Message}");
                return string.Empty;
            }
        }
    }
}