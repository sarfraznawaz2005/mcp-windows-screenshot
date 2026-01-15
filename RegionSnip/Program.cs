using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        string mode = "region"; // region | full
        string? outPath = null;
        string prompt = "Drag to select an area. Press Esc to cancel.";
        int monitorIndex = 0;   // 0-based
        bool captureAll = false;

        // Simple arg parsing
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];

            if (a == "--mode" && i + 1 < args.Length) mode = (args[++i] ?? "region").Trim().ToLowerInvariant();
            else if (a == "--out" && i + 1 < args.Length) outPath = args[++i];
            else if (a == "--prompt" && i + 1 < args.Length) prompt = args[++i];
            else if (a == "--monitor" && i + 1 < args.Length && int.TryParse(args[++i], out var mi)) monitorIndex = mi;
            else if (a == "--all") captureAll = true;
        }

        if (string.IsNullOrWhiteSpace(outPath))
        {
            var msg = "Missing --out <path>";
            ShowNotification("RegionSnip Error", msg, ToolTipIcon.Error);
            WriteJson(new { ok = false, error = msg });
            Environment.Exit(2);
            return;
        }

        try
        {
            EnsureDirectoryExists(outPath);

            if (mode == "full")
            {
                var result = CaptureFull(outPath!, captureAll, monitorIndex);
                WriteJson(result);
                // Notification handled inside CaptureFull? 
                // Let's move it here for consistency.
                ShowNotification("RegionSnip", "Screenshot saved successfully.", ToolTipIcon.Info);
                return;
            }

            // region mode (interactive)
            ApplicationConfiguration.Initialize();
            using var form = new SnipOverlayForm(prompt, outPath!);
            Application.Run(form);
            
            // Handle result from form
            if (form.ResultObject != null)
            {
                WriteJson(form.ResultObject);
            }

            if (form.ShowSuccessNotification)
            {
                ShowNotification("RegionSnip", "Screenshot saved successfully.", ToolTipIcon.Info);
            }
            else if (!string.IsNullOrEmpty(form.ErrorMessage))
            {
                ShowNotification("RegionSnip Error", form.ErrorMessage, ToolTipIcon.Error);
            }
        }
        catch (Exception ex)
        {
            ShowNotification("RegionSnip Error", ex.Message, ToolTipIcon.Error);
            WriteJson(new { ok = false, error = ex.Message });
            Environment.Exit(1);
        }
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private static object CaptureFull(string outPath, bool all, int monitorIndex)
    {
        Rectangle bounds;

        if (all)
        {
            bounds = SystemInformation.VirtualScreen;
        }
        else
        {
            var screens = Screen.AllScreens;
            if (screens.Length == 0) bounds = SystemInformation.VirtualScreen;
            else
            {
                if (monitorIndex < 0 || monitorIndex >= screens.Length) monitorIndex = 0;
                bounds = screens[monitorIndex].Bounds;
            }
        }

        using var bmp = new Bitmap(bounds.Width, bounds.Height);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bmp.Size);
        bmp.Save(outPath, ImageFormat.Png);

        return new
        {
            ok = true,
            path = outPath,
            mode = "full",
            monitorIndex = all ? (int?)null : monitorIndex,
            all = all,
            rect = new { x = bounds.Left, y = bounds.Top, width = bounds.Width, height = bounds.Height },
            width = bounds.Width,
            height = bounds.Height
        };
    }

    private static void WriteJson(object obj)
    {
        Console.WriteLine(JsonSerializer.Serialize(obj));
        Console.Out.Flush();
    }

    internal static void ShowNotification(string title, string text, ToolTipIcon icon)
    {
        try
        {
            // We need a thread with a message pump for NotifyIcon to work reliable.
            // Since we might be running this after the main Application loop closed,
            // or in a console context, we should ensure the icon processes messages.
            
            using var notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Application;
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(3000, title, text, icon);
            
            // Pump messages for a short duration to ensure the notification is shown/handled
            int elapsed = 0;
            while (elapsed < 2000) 
            {
                Application.DoEvents();
                Thread.Sleep(50);
                elapsed += 50;
            }
        }
        catch 
        { 
            // Notifications are non-critical; ignore failures.
        }
    }
}

public sealed class SnipOverlayForm : Form
{
    private readonly string _prompt;
    private readonly string _outPath;

    private bool _dragging;
    private Point _start;
    private Point _current;
    private Rectangle _selectionRect;

    private readonly Label _hint;

    // Results to pass back to Main
    public object? ResultObject { get; private set; }
    public bool ShowSuccessNotification { get; private set; }
    public string? ErrorMessage { get; private set; }

    public SnipOverlayForm(string prompt, string outPath)
    {
        _prompt = prompt;
        _outPath = outPath;

        var vs = SystemInformation.VirtualScreen;
        Bounds = vs;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;

        BackColor = Color.Black;
        Opacity = 0.25;

        Cursor = Cursors.Cross;

        _hint = new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Text = _prompt,
            Location = new Point(20, 20)
        };
        Controls.Add(_hint);

        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        KeyDown += OnKeyDown;
        Paint += OnPaint;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            ResultObject = new { ok = false, cancelled = true, mode = "region" };
            Close();
        }
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        _start = e.Location;
        _current = e.Location;
        _selectionRect = Rectangle.Empty;
        Invalidate();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        _current = e.Location;
        _selectionRect = MakeRect(_start, _current);
        Invalidate();
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_dragging || e.Button != MouseButtons.Left) return;

        _dragging = false;
        _current = e.Location;
        _selectionRect = MakeRect(_start, _current);
        Invalidate();

        if (_selectionRect.Width < 5 || _selectionRect.Height < 5)
        {
            ErrorMessage = "Selection too small.";
            ResultObject = new { ok = false, error = ErrorMessage, mode = "region" };
            Close();
            return;
        }

        try
        {
            CaptureSelection(_selectionRect);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ResultObject = new { ok = false, error = ex.Message, mode = "region" };
        }
        finally
        {
            Close();
        }
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
        {
            using var pen = new Pen(Color.Lime, 2);
            e.Graphics.DrawRectangle(pen, _selectionRect);

            using var brush = new SolidBrush(Color.FromArgb(40, Color.Lime));
            e.Graphics.FillRectangle(brush, _selectionRect);
        }
    }

    private void CaptureSelection(Rectangle rectOnForm)
    {
        var vs = SystemInformation.VirtualScreen;
        var absX = vs.Left + rectOnForm.Left;
        var absY = vs.Top + rectOnForm.Top;

        // Ensure directory is created in Main or here? 
        // Main calls EnsureDirectoryExists, but just to be safe if reused:
        // Directory.CreateDirectory(Path.GetDirectoryName(_outPath)!);
        // Assuming Main handled it or we handle it here. 
        // Let's rely on Main having called EnsureDirectoryExists(outPath) before running the form.

        using var bmp = new Bitmap(rectOnForm.Width, rectOnForm.Height);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(absX, absY, 0, 0, bmp.Size);
        bmp.Save(_outPath, ImageFormat.Png);

        ShowSuccessNotification = true;
        ResultObject = new
        {
            ok = true,
            path = _outPath,
            mode = "region",
            rect = new { x = absX, y = absY, width = rectOnForm.Width, height = rectOnForm.Height },
            width = rectOnForm.Width,
            height = rectOnForm.Height
        };
    }

    private static Rectangle MakeRect(Point a, Point b)
    {
        int x1 = Math.Min(a.X, b.X);
        int y1 = Math.Min(a.Y, b.Y);
        int x2 = Math.Max(a.X, b.X);
        int y2 = Math.Max(a.Y, b.Y);
        return new Rectangle(x1, y1, x2 - x1, y2 - y1);
    }
}