using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32.SafeHandles;
using Size = System.Windows.Size;

namespace LaboratoireProgrammation.Project.Helpers;

public class CursorHelper {
    [DllImport("user32.dll")]
    private static extern IntPtr CreateIconIndirect(ref IconInfo icon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);


    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static Cursor InternalCreateCursor(Bitmap bmp, int xHotSpot, int yHotSpot) {
        var tmp = new IconInfo();
        var hIcon = bmp.GetHicon();
        GetIconInfo(hIcon, ref tmp);
        DestroyIcon(hIcon);
        tmp.xHotspot = xHotSpot;
        tmp.yHotspot = yHotSpot;
        tmp.fIcon = false;

        var ptr = CreateIconIndirect(ref tmp);
        var handle = new SafeFileHandle(ptr, true);
        return CursorInteropHelper.Create(handle);
    }

    public static Cursor CreateCursor(UIElement element, int xHotSpot, int yHotSpot) {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        element.Arrange(new Rect(0, 0, element.DesiredSize.Width,
            element.DesiredSize.Height));

        var rtb = new RenderTargetBitmap((int)element.DesiredSize.Width,
            (int)element.DesiredSize.Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        var ms = new MemoryStream();
        encoder.Save(ms);

        var bmp = new Bitmap(ms);

        ms.Close();
        ms.Dispose();

        var cur = InternalCreateCursor(bmp, xHotSpot, yHotSpot);

        bmp.Dispose();

        return cur;
    }

    private struct IconInfo {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }
}