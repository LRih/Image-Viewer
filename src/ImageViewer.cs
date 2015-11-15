using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ImageViewer
{
    public class ImageViewer : Form
    {
        //===================================================================== API
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int StrCmpLogicalW(string x, string y);

        //===================================================================== VARIABLES
        private ImageCanvas _imageCanvas = new ImageCanvas();

        // image details
        private string _currentPath;

        // form spatial data during full screen
        private FormWindowState _windowState;
        private Rectangle _windowRect;

        //===================================================================== INITIALIZE
        public ImageViewer()
        {
            _imageCanvas.Dock = DockStyle.Fill;
            _imageCanvas.DragDrop += imageCanvas_DragDrop;
            _imageCanvas.DragEnter += imageCanvas_DragEnter;
            _imageCanvas.Zoomed += imageCanvas_Zoomed;

            this.ClientSize = new Size(480, 460);
            this.Icon = new Icon(Assembly.GetCallingAssembly().GetManifestResourceStream("ImageViewer.Icon.ico"));
            this.KeyPreview = true;
            this.MinimumSize = new Size(300, 300);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Image Viewer";
            this.Controls.Add(_imageCanvas);
        }
        protected override void OnLoad(EventArgs e)
        {
            this.WindowState = FormWindowState.Maximized;
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (IsImagePath(arg))
                {
                    LoadImage(arg);
                    break;
                }
            }
            base.OnLoad(e);
        }

        //===================================================================== FUNCTIONS
        private void LoadImage(string path)
        {
            try
            {
                _imageCanvas.LoadImage(new Bitmap(path));
                _currentPath = path;
                UpdateTitle();
            }
            catch { this.Text = "Image Viewer"; }
            this.Invalidate();
        }
        private void UpdateTitle()
        {
            this.Text = string.Format("{0} - [{1}] {2}%", CurrentTitle, _imageCanvas.Quality, (int)(_imageCanvas.Zoom * 100));
        }
        private void ToggleFullscreen()
        {
            if (this.FormBorderStyle == FormBorderStyle.None)
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.WindowState = _windowState;
                this.Left = _windowRect.Left;
                this.Top = _windowRect.Top;
                this.Width = _windowRect.Width;
                this.Height = _windowRect.Height;
            }
            else
            {
                this.FormBorderStyle = FormBorderStyle.None;
                _windowState = this.WindowState;
                _windowRect = new Rectangle(Left, Top, Width, Height);
                this.Left = this.Top = 0;
                this.Width = Screen.FromControl(this).Bounds.Width;
                this.Height = Screen.FromControl(this).Bounds.Height;
            }
        }

        private void GotoPreviousImage()
        {
            string[] filePaths = GetFilePaths(CurrentDirectory);
            int index = Array.FindIndex<string>(filePaths, path => path.EndsWith(CurrentTitle));
            if (index == 0) LoadImage(filePaths.Last());
            else LoadImage(filePaths[index - 1]);
        }
        private void GotoNextImage()
        {
            string[] filePaths = GetFilePaths(CurrentDirectory);
            int index = Array.FindIndex<string>(filePaths, path => path.EndsWith(CurrentTitle));
            if (index == filePaths.GetUpperBound(0)) LoadImage(filePaths.First());
            else LoadImage(filePaths[index + 1]);
        }
        private string[] GetFilePaths(string directoryPath)
        {
            string[] filePaths = Directory.GetFiles(CurrentDirectory);
            filePaths = Array.FindAll<string>(filePaths, IsImagePath);
            Array.Sort<string>(filePaths, StrCmpLogicalW);
            return filePaths;
        }
        private bool IsImagePath(string path)
        {
            string[] validImageExtensions = new string[] { "bmp", "gif", "jpeg", "jpg", "png" };
            foreach (string extension in validImageExtensions)
            {
                if (path.ToLower().EndsWith("." + extension)) return true;
            }
            return false;
        }

        //===================================================================== PROPERTIES
        private string CurrentDirectory
        {
            get { return Path.GetDirectoryName(_currentPath); }
        }
        private string CurrentTitle
        {
            get { return Path.GetFileName(_currentPath); }
        }

        //===================================================================== EVENTS
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11) ToggleFullscreen();
            if (e.Alt && e.KeyCode == Keys.A) this.TopMost = (this.TopMost == false);
            if (_imageCanvas.IsImageLoaded)
            {
                if (e.Control)
                {
                    if (e.KeyCode == Keys.Left) GotoPreviousImage();
                    else if (e.KeyCode == Keys.Right) GotoNextImage();
                }
                else
                {
                    if (e.KeyCode == Keys.OemQuestion) this.Opacity = (this.Opacity == 0 ? 100 : 0);
                }
            }
            base.OnKeyDown(e);
        }
        private void imageCanvas_Zoomed(object sender, EventArgs e)
        {
            UpdateTitle();
        }

        // drag drop
        private void imageCanvas_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return; // only accept file
            // only accept images
            string path = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
            if (IsImagePath(path)) e.Effect = DragDropEffects.Copy;
        }
        private void imageCanvas_DragDrop(object sender, DragEventArgs e)
        {
            string path = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
            LoadImage(path);
        }
    }
}
