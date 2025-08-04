namespace QuietPrompt
{
    public class SnippingOverlay : Form
    {
        private Point _start;
        private Point _end;
        private Rectangle _selection;
        private bool _dragging = false;

        public Rectangle Selection => _selection;

        public SnippingOverlay()
        {
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            Location = SystemInformation.VirtualScreen.Location;
            TopMost = true;
            BackColor = Color.White;
            Opacity = 0.2;
            Cursor = Cursors.Cross;
            ShowInTaskbar = false;
        }
        
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Ensure the form covers the entire virtual screen
            Bounds = SystemInformation.VirtualScreen;
            Location = SystemInformation.VirtualScreen.Location;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _start = e.Location;
                _dragging = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                _end = e.Location;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _dragging)
            {
                _dragging = false;
                _end = e.Location;
                _selection = GetRectangle(_start, _end);
                // Offset selection to virtual screen coordinates
                _selection.Offset(Bounds.Location);
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_dragging)
            {
                var rect = GetRectangle(_start, _end);
                using var pen = new Pen(Color.Red, 2);
                e.Graphics.DrawRectangle(pen, rect);
            }
        }

        private Rectangle GetRectangle(Point p1, Point p2)
        {
            return new Rectangle(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y),
                Math.Abs(p1.X - p2.X),
                Math.Abs(p1.Y - p2.Y));
        }

        public static Rectangle GetSnipRectangle()
        {
            using var overlay = new SnippingOverlay();
            return overlay.ShowDialog() == DialogResult.OK ? overlay.Selection : Rectangle.Empty;
        }
    }
}