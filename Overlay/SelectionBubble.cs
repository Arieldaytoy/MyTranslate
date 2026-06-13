namespace MyTranslate.Overlay;

/// <summary>
/// 划词翻译气泡图标 — 选中文字后在旁边显示的小图标，点击触发翻译
/// </summary>
public class SelectionBubble : OverlayForm
{
    /// <summary>图标被点击时触发</summary>
    public event EventHandler? BubbleClicked;

    public SelectionBubble()
    {
        Size = new Size(28, 28);
        BackColor = Color.FromArgb(60, 120, 216);  // 蓝色圆形图标
        CornerRadius = 14;  // 完全圆角
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // 绘制翻译图标 "译" 字
        using var brush = new SolidBrush(Color.White);
        using var font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        var textSize = e.Graphics.MeasureString("译", font);
        var x = (Width - textSize.Width) / 2;
        var y = (Height - textSize.Height) / 2;
        e.Graphics.DrawString("译", font, brush, x, y);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        BubbleClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>在指定位置附近显示气泡</summary>
    public void ShowAt(Point screenPoint)
    {
        PositionNear(screenPoint, offsetX: 10, offsetY: 10);
        if (!Visible) Show();
    }
}
