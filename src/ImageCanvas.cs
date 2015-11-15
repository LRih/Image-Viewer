using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ImageViewer
{
    public class ImageCanvas : UserControl
    {
        //===================================================================== EVENTS
        public event EventHandler Zoomed;

        //===================================================================== VARIABLES
        private IContainer _components = new Container();
        private Timer _timerGIF;

        // flags
        private bool _isPanning = false;

        // image details
        private Bitmap _currentImage;

        // image spatial data
        private Point _origin = new Point(0, 0);
        private float _zoom = 1.0f;

        // gif handling data
        private FrameDimension _frameDimension;
        private int _gifFrame;

        //===================================================================== INITIALIZE
        public ImageCanvas()
        {
            _timerGIF = new Timer(_components);
            _timerGIF.Enabled = true;
            _timerGIF.Tick += timerGIF_Tick;
            this.AllowDrop = true;
            this.BackColor = Color.DimGray;
            this.DoubleBuffered = true;
        }

        //===================================================================== TERMINATE
        protected override void Dispose(bool disposing)
        {
            DisposeImage();
            if (disposing && _components != null) _components.Dispose();
            base.Dispose(disposing);
        }
        private void DisposeImage()
        {
            if (_currentImage != null)
            {
                _currentImage.Dispose();
                _currentImage = null;
            }
        }

        //===================================================================== FUNCTIONS
        public void LoadImage(Bitmap image)
        {
            DisposeImage();
            try
            {
                _currentImage = image;
                _frameDimension = new FrameDimension(_currentImage.FrameDimensionsList[0]);
                if (FrameCount > 1)
                {
                    _timerGIF.Interval = BitConverter.ToInt32(_currentImage.GetPropertyItem(0x5100).Value, 0) * 10;
                    _timerGIF.Enabled = true;
                }
                else _timerGIF.Enabled = false;
                FitToScreen();
                this.Invalidate();
            }
            catch (Exception ex) { throw ex; }
        }
        private void FitToScreen()
        {
            float zoomX = ClientSize.Width / (float)_currentImage.Width;
            float zoomY = ClientSize.Height / (float)_currentImage.Height;
            Zoom = Math.Min(zoomX, zoomY);
        }

        private void ZoomIn()
        {
            if (Zoom >= 0.9 && Zoom < 1.0) Zoom = 1.0f;
            else if (Zoom >= 1.0) Zoom += 0.5f;
            else Zoom += 0.1f;
        }
        private void ZoomOut()
        {
            if (Zoom > 1.0 && Zoom <= 1.5) Zoom = 1.0f;
            else if (Zoom > 1.0) Zoom -= 0.5f;
            else Zoom -= 0.1f;
        }

        //===================================================================== PROPERTIES
        public bool IsImageLoaded
        {
            get { return _currentImage != null; }
        }
        public InterpolationMode Quality
        {
            get
            {
                if (_currentImage.GetFrameCount(_frameDimension) > 1) return InterpolationMode.NearestNeighbor; // gif
                if (_currentImage.Width > 1920) return InterpolationMode.NearestNeighbor; // high-res image
                if (_isPanning) return InterpolationMode.NearestNeighbor; // panning
                if (_zoom >= 1.0) return InterpolationMode.NearestNeighbor; // zooming
                return InterpolationMode.High;
            }
        }
        private int FrameCount
        {
            get { return _currentImage.GetFrameCount(_frameDimension); }
        }
        private int X
        {
            get { return _origin.X; }
            set
            {
                float imageWidth = ZoomWidth;
                int x;
                if (imageWidth < ClientSize.Width) x = (int)((ClientSize.Width - imageWidth) / 2);
                else x = Math.Min(Math.Max(value, ClientSize.Width - (int)imageWidth), 0);
                _origin = new Point(x, _origin.Y);
            }
        }
        private int Y
        {
            get { return _origin.Y; }
            set
            {
                float imageHeight = ZoomHeight;
                int y;
                if (imageHeight < ClientSize.Height) y = (int)((ClientSize.Height - imageHeight) / 2);
                else y = Math.Min(Math.Max(value, ClientSize.Height - (int)imageHeight), 0);
                _origin = new Point(_origin.X, y);
            }
        }
        private int ZoomWidth
        {
            get { return (int)Math.Round(_currentImage.Width * Zoom); }
        }
        private int ZoomHeight
        {
            get { return (int)Math.Round(_currentImage.Height * Zoom); }
        }
        public float Zoom
        {
            get { return _zoom; }
            private set
            {
                Point mouseClient = this.PointToClient(MousePosition);
                float mouseImageX = (mouseClient.X - X) / Zoom;
                float mouseImageY = (mouseClient.Y - Y) / Zoom;

                _zoom = Math.Min(Math.Max(value, 0.1f), 6.0f);

                X = mouseClient.X - (int)Math.Round(mouseImageX * Zoom);
                Y = mouseClient.Y - (int)Math.Round(mouseImageY * Zoom);
                if (Zoomed != null) Zoomed(this, new EventArgs());
            }
        }
        private int PanUnitX
        {
            get { return (int)Math.Round(_currentImage.Width / 20 * Zoom); }
        }
        private int PanUnitY
        {
            get { return (int)Math.Round(_currentImage.Height / 20 * Zoom); }
        }

        //===================================================================== EVENTS
        protected override void OnPaint(PaintEventArgs e)
        {
            if (_currentImage == null) return;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            e.Graphics.InterpolationMode = Quality;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.FillRectangle(Brushes.White, X, Y, ZoomWidth, ZoomHeight);
            e.Graphics.DrawImage(_currentImage, X, Y, ZoomWidth, ZoomHeight);
            base.OnPaint(e);
        }

        // pan controls
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (IsImageLoaded)
            {
                _isPanning = true;
                Point startingOrigin = _origin;
                Point startingMouse = MousePosition;
                do
                {
                    Point movement = new Point(startingMouse.X - MousePosition.X, startingMouse.Y - MousePosition.Y);
                    X = startingOrigin.X - movement.X;
                    Y = startingOrigin.Y - movement.Y;
                    this.Invalidate();
                    Application.DoEvents();
                }
                while (_isPanning);
                this.Invalidate();
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            _isPanning = false;
            base.OnMouseUp(e);
        }

        // key pan controls
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (IsImageLoaded)
            {
                bool changed = false;
                if ((keyData & Keys.Control) != Keys.None)
                {
                    switch (keyData & Keys.KeyCode)
                    {
                        case Keys.Up: ZoomIn(); changed = true; break;
                        case Keys.Down: ZoomOut(); changed = true; break;
                    }
                }
                else if ((keyData & Keys.Shift) != Keys.None)
                {
                    switch (keyData & Keys.KeyCode)
                    {
                        case Keys.Left: X = 0; changed = true; break;
                        case Keys.Up: Y = 0; changed = true; break;
                        case Keys.Right: X = (int)(-ZoomWidth); changed = true; break;
                        case Keys.Down: Y = (int)(-ZoomHeight); changed = true; break;
                    }
                }
                else
                {
                    switch (keyData & Keys.KeyCode)
                    {
                        case Keys.Left: X += PanUnitX; changed = true; break;
                        case Keys.Up: Y += PanUnitY; changed = true; break;
                        case Keys.Right: X -= PanUnitX; changed = true; break;
                        case Keys.Down: Y -= PanUnitY; changed = true; break;
                    }
                }
                if (changed) this.Invalidate();
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // zoom controls
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (IsImageLoaded)
            {
                if (e.Delta > 0) ZoomIn();
                else if (e.Delta < 0) ZoomOut();
                this.Invalidate();
            }
            base.OnMouseWheel(e);
        }

        // refresh on resize
        protected override void OnResize(EventArgs e)
        {
            if (IsImageLoaded)
            {
                X = X;
                Y = Y;
                this.Invalidate();
            }
            base.OnResize(e);
        }

        // update gif
        private void timerGIF_Tick(object sender, EventArgs e)
        {
            if (_currentImage == null) return;
            _gifFrame = ++_gifFrame % FrameCount;
            _currentImage.SelectActiveFrame(_frameDimension, _gifFrame);
            this.Invalidate();
        }
    }
}
