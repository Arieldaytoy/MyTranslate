namespace MyTranslate.Capture;

/// <summary>
/// 屏幕截图辅助 — 截取屏幕指定区域的图像（供 OCR 使用）
/// </summary>
public static class ScreenCaptureHelper
{
    /// <summary>
    /// 截取屏幕指定区域的图像
    /// </summary>
    /// <param name="area">屏幕区域（像素坐标）</param>
    /// <returns>截取的图像</returns>
    public static Bitmap CaptureRegion(Rectangle area)
    {
        var bitmap = new Bitmap(area.Width, area.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(area.Location, Point.Empty, area.Size);
        return bitmap;
    }

    /// <summary>
    /// 截取鼠标周围指定半径的区域
    /// </summary>
    /// <param name="center">鼠标屏幕坐标</param>
    /// <param name="radius">截取半径（像素），默认 100</param>
    /// <returns>截取的图像</returns>
    public static Bitmap CaptureAroundPoint(Point center, int radius = 100)
    {
        var area = new Rectangle(
            Math.Max(0, center.X - radius),
            Math.Max(0, center.Y - radius),
            radius * 2,
            radius * 2);

        // 确保不超出屏幕边界
        var screen = Screen.FromPoint(center).Bounds;
        area = Rectangle.Intersect(area, screen);

        return CaptureRegion(area);
    }
}
