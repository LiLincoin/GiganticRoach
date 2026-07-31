using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new RoachContext());
    }
}

internal sealed class RoachContext : ApplicationContext
{
    private readonly RoachForm roach;
    private readonly NotifyIcon tray;

    public RoachContext()
    {
        roach = new RoachForm();
        roach.ExitRequested += Exit;

        ContextMenu menu = new ContextMenu();
        menu.MenuItems.Add("退出蟑螂桌宠", delegate { Exit(); });
        tray = new NotifyIcon();
        tray.Icon = SystemIcons.Application;
        tray.Text = "巨大化（Ctrl+Shift+Q 退出）";
        tray.ContextMenu = menu;
        tray.Visible = true;

        roach.Show();
    }

    private void Exit()
    {
        tray.Visible = false;
        tray.Dispose();
        roach.Close();
        ExitThread();
    }
}

internal sealed class RoachForm : Form
{
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int ULW_ALPHA = 0x00000002;
    private const int AC_SRC_OVER = 0x00;
    private const int AC_SRC_ALPHA = 0x01;
    private const int HOTKEY_ID = 0x524F;
    private const int MOD_SHIFT = 0x0004;
    private const int MOD_CONTROL = 0x0002;
    private const int WM_HOTKEY = 0x0312;

    private readonly Timer timer;
    private readonly Random random = new Random();
    private readonly Bitmap sprite;
    private double x;
    private double y;
    private double direction;
    private double speed;
    private double turnVelocity;
    private int decisionTicks;
    private int pauseTicks;
    private bool exiting;

    public event Action ExitRequested;

    public RoachForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        Text = "巨大化";
        StartPosition = FormStartPosition.Manual;

        sprite = LoadSprite();
        int canvasSize = (int)Math.Ceiling(Math.Sqrt(sprite.Width * sprite.Width + sprite.Height * sprite.Height)) + 24;
        Width = canvasSize;
        Height = canvasSize;

        Rectangle area = SystemInformation.VirtualScreen;
        x = area.Left + random.Next(Math.Max(1, area.Width - Width));
        y = area.Top + random.Next(Math.Max(1, area.Height - Height));
        direction = random.NextDouble() * Math.PI * 2.0;
        speed = 3.2 + random.NextDouble() * 2.8;

        timer = new Timer();
        timer.Interval = 20;
        timer.Tick += TickMovement;
        timer.Start();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, (int)Keys.Q);
        RenderSprite();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            RequestExit();
            return;
        }
        base.WndProc(ref m);
    }

    private static Bitmap LoadSprite()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        Stream stream = assembly.GetManifestResourceStream("xiaoqiang.png");
        if (stream == null) throw new InvalidOperationException("找不到内置的蟑螂图片。");
        using (stream)
        using (Image original = Image.FromStream(stream))
        {
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            double aspectRatio = original.Width / (double)original.Height;
            double targetArea = screen.Width * (double)screen.Height / 8.0;
            int targetWidth = (int)Math.Round(Math.Sqrt(targetArea * aspectRatio));
            int targetHeight = (int)Math.Round(targetWidth / aspectRatio);
            Bitmap resized = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(resized))
            {
                g.Clear(Color.Transparent);
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(original, new Rectangle(0, 0, resized.Width, resized.Height));
            }
            return resized;
        }
    }

    private void TickMovement(object sender, EventArgs e)
    {
        if (exiting) return;

        if (pauseTicks > 0)
        {
            pauseTicks--;
            if ((pauseTicks % 5) == 0) direction += (random.NextDouble() - 0.5) * 0.08;
            RenderSprite();
            return;
        }

        if (--decisionTicks <= 0)
        {
            decisionTicks = random.Next(18, 75);
            turnVelocity = (random.NextDouble() - 0.5) * 0.075;
            speed = 2.8 + random.NextDouble() * 3.8;
            if (random.Next(9) == 0) pauseTicks = random.Next(8, 35);
        }

        direction += turnVelocity + (random.NextDouble() - 0.5) * 0.018;
        x += Math.Cos(direction) * speed;
        y += Math.Sin(direction) * speed;

        Rectangle area = SystemInformation.VirtualScreen;
        double margin = 18.0;
        if (x < area.Left - margin)
        {
            x = area.Left - margin;
            direction = Math.PI - direction;
            turnVelocity = -turnVelocity;
        }
        else if (x + Width > area.Right + margin)
        {
            x = area.Right + margin - Width;
            direction = Math.PI - direction;
            turnVelocity = -turnVelocity;
        }
        if (y < area.Top - margin)
        {
            y = area.Top - margin;
            direction = -direction;
            turnVelocity = -turnVelocity;
        }
        else if (y + Height > area.Bottom + margin)
        {
            y = area.Bottom + margin - Height;
            direction = -direction;
            turnVelocity = -turnVelocity;
        }

        RenderSprite();
    }

    private void RenderSprite()
    {
        using (Bitmap canvas = new Bitmap(Width, Height, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(canvas))
        {
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceOver;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            g.TranslateTransform(Width / 2f, Height / 2f);
            float degrees = (float)(direction * 180.0 / Math.PI + 135.0);
            g.RotateTransform(degrees);
            float bob = (float)(Math.Sin(Environment.TickCount / 45.0) * 1.2);
            g.DrawImage(sprite, -sprite.Width / 2f, -sprite.Height / 2f + bob, sprite.Width, sprite.Height);
            g.ResetTransform();

            SetBitmap(canvas, (int)Math.Round(x), (int)Math.Round(y));
        }
    }

    private void SetBitmap(Bitmap bitmap, int left, int top)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memoryDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;
        try
        {
            hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            oldBitmap = SelectObject(memoryDc, hBitmap);
            NativePoint destination = new NativePoint(left, top);
            NativeSize size = new NativeSize(bitmap.Width, bitmap.Height);
            NativePoint source = new NativePoint(0, 0);
            BlendFunction blend = new BlendFunction();
            blend.BlendOp = AC_SRC_OVER;
            blend.SourceConstantAlpha = 255;
            blend.AlphaFormat = AC_SRC_ALPHA;
            UpdateLayeredWindow(Handle, screenDc, ref destination, ref size, memoryDc, ref source, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero) SelectObject(memoryDc, oldBitmap);
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private void RequestExit()
    {
        if (exiting) return;
        exiting = true;
        if (ExitRequested != null) ExitRequested();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop();
            timer.Dispose();
            sprite.Dispose();
        }
        if (IsHandleCreated) UnregisterHotKey(Handle, HOTKEY_ID);
        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
        public NativePoint(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
        public NativeSize(int width, int height) { Width = width; Height = height; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int modifiers, int key);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDc);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDc);
    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDc, IntPtr hObject);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr hdcDst, ref NativePoint pptDst, ref NativeSize psize, IntPtr hdcSrc, ref NativePoint pptSrc, int crKey, ref BlendFunction pblend, int flags);
}
