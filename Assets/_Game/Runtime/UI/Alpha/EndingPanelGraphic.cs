using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 从一张完整结局插画中绘制一个长方形分镜，同时保留它在整张图中的原始位置。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class EndingPanelGraphic : MaskableGraphic
{
    private Texture illustrationTexture;
    private Rect panelUvRect = new Rect(0f, 0f, 1f, 1f);

    public override Texture mainTexture =>
        illustrationTexture != null ? illustrationTexture : Texture2D.whiteTexture;

    public void Configure(Texture texture, Rect normalizedPanelRect)
    {
        illustrationTexture = texture;
        panelUvRect = Clamp01(normalizedPanelRect);
        raycastTarget = false;
        SetVerticesDirty();
        SetMaterialDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (illustrationTexture == null || panelUvRect.width <= 0f || panelUvRect.height <= 0f)
            return;

        Rect fullImageRect = GetAspectFittedRect();
        float xMin = Mathf.Lerp(fullImageRect.xMin, fullImageRect.xMax, panelUvRect.xMin);
        float xMax = Mathf.Lerp(fullImageRect.xMin, fullImageRect.xMax, panelUvRect.xMax);
        float yMin = Mathf.Lerp(fullImageRect.yMin, fullImageRect.yMax, panelUvRect.yMin);
        float yMax = Mathf.Lerp(fullImageRect.yMin, fullImageRect.yMax, panelUvRect.yMax);

        AddVertex(vertexHelper, new Vector2(xMin, yMin), new Vector2(panelUvRect.xMin, panelUvRect.yMin));
        AddVertex(vertexHelper, new Vector2(xMin, yMax), new Vector2(panelUvRect.xMin, panelUvRect.yMax));
        AddVertex(vertexHelper, new Vector2(xMax, yMax), new Vector2(panelUvRect.xMax, panelUvRect.yMax));
        AddVertex(vertexHelper, new Vector2(xMax, yMin), new Vector2(panelUvRect.xMax, panelUvRect.yMin));
        vertexHelper.AddTriangle(0, 1, 2);
        vertexHelper.AddTriangle(0, 2, 3);
    }

    private void AddVertex(VertexHelper vertexHelper, Vector2 position, Vector2 uv)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = position;
        vertex.uv0 = uv;
        vertexHelper.AddVert(vertex);
    }

    private Rect GetAspectFittedRect()
    {
        Rect container = rectTransform.rect;
        float textureAspect = illustrationTexture.width / (float)illustrationTexture.height;
        float containerAspect = container.width / container.height;

        if (containerAspect > textureAspect)
        {
            float width = container.height * textureAspect;
            return new Rect(container.center.x - width * 0.5f, container.yMin, width, container.height);
        }

        float height = container.width / textureAspect;
        return new Rect(container.xMin, container.center.y - height * 0.5f, container.width, height);
    }

    private static Rect Clamp01(Rect value)
    {
        float xMin = Mathf.Clamp01(value.xMin);
        float xMax = Mathf.Clamp01(value.xMax);
        float yMin = Mathf.Clamp01(value.yMin);
        float yMax = Mathf.Clamp01(value.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}
