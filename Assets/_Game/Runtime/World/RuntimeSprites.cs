using UnityEngine;

/// <summary>美术未到位时的占位贴图：纯色块、路面、箭头。全部在运行时生成，不产生资产。</summary>
public static class RuntimeSprites
{
    private static Sprite solid;
    private static Sprite road;

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
