using UnityEngine;

/// <summary>美术未到位时的占位贴图：纯色块、路面、箭头。全部在运行时生成，不产生资产。</summary>
public static class RuntimeSprites
{
    private static Sprite solid;
    private static Sprite road;
    private static Sprite arrow;
    private static Sprite truck;

    public static Sprite Solid()
    {
        if (solid == null)
            solid = Create(Fill(4, 4, (x, y) => Color.white), 4f);
        return solid;
    }

    /// <summary>灰色路面 + 中央黄色虚线，沿 y 平铺。一块 = 4×4 单位。</summary>
    public static Sprite Road()
    {
        if (road == null)
        {
            road = Create(Fill(32, 32, (x, y) =>
            {
                bool dash = x >= 14 && x < 18 && (y % 16) < 8;
                bool edge = x < 2 || x >= 30;
                return dash ? new Color(0.95f, 0.85f, 0.35f) : edge ? new Color(0.35f, 0.33f, 0.3f) : new Color(0.5f, 0.48f, 0.45f);
            }), 8f);
        }

        return road;
    }

    /// <summary>朝 +X 的三角箭头，1×1 单位。</summary>
    public static Sprite Arrow()
    {
        if (arrow == null)
        {
            arrow = Create(Fill(32, 32, (x, y) =>
            {
                int half = Mathf.Abs(y - 16);
                bool head = x >= 12 && half <= (31 - x) * 16 / 19;
                bool shaft = x < 12 && half <= 5;
                return head || shaft ? Color.white : Color.clear;
            }), 32f);
        }

        return arrow;
    }

    /// <summary>占位卡车：4×2 单位的深色车身 + 车头。</summary>
    public static Sprite Truck()
    {
        if (truck == null)
        {
            truck = Create(Fill(64, 32, (x, y) =>
            {
                bool cab = x >= 48 && y >= 6 && y < 26;
                bool box = x < 48 && y >= 4 && y < 30;
                bool wheel = (y < 6) && ((x >= 8 && x < 18) || (x >= 50 && x < 60));
                return wheel ? new Color(0.1f, 0.1f, 0.1f) : cab ? new Color(0.85f, 0.25f, 0.2f) : box ? new Color(0.55f, 0.35f, 0.2f) : Color.clear;
            }), 16f);
        }

        return truck;
    }

    private static Texture2D Fill(int width, int height, System.Func<int, int, Color> pixel)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave,
        };
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            pixels[y * width + x] = pixel(x, y);
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Sprite Create(Texture2D texture, float pixelsPerUnit)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
