using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class IllustrationFragmentGraphic : MaskableGraphic
{
    private const float DiagonalOffset = 0.03f;
    private const float SplitIntersectionX = 0.53f;
    private const float SecondSplitBottomX = 0.65f;

    private static readonly Vector2[][] FragmentPolygons =
    {
        // 第一块：主对角线以上。
        new[]
        {
            new Vector2(DiagonalOffset, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f - DiagonalOffset)
        },

        // 第二块：主对角线与右下分割线之间。
        new[]
        {
            new Vector2(DiagonalOffset, 0f),
            new Vector2(SecondSplitBottomX, 0f),
            new Vector2(
                SplitIntersectionX,
                SplitIntersectionX - DiagonalOffset)
        },

        // 第三块：剩余的右下区域。
        new[]
        {
            new Vector2(SecondSplitBottomX, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f - DiagonalOffset),
            new Vector2(
                SplitIntersectionX,
                SplitIntersectionX - DiagonalOffset)
        }
    };

    private Texture illustrationTexture;
    private int fragmentIndex;

    public override Texture mainTexture =>
        illustrationTexture != null
            ? illustrationTexture
            : Texture2D.whiteTexture;

    public void Configure(Texture texture, int index)
    {
        illustrationTexture = texture;
        fragmentIndex = Mathf.Clamp(index, 0, FragmentPolygons.Length - 1);
        raycastTarget = false;

        SetVerticesDirty();
        SetMaterialDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        if (illustrationTexture == null)
            return;

        Vector2[] polygon = FragmentPolygons[fragmentIndex];
        Rect drawRect = GetAspectFittedRect();

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 uv = polygon[i];
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = new Vector3(
                Mathf.Lerp(drawRect.xMin, drawRect.xMax, uv.x),
                Mathf.Lerp(drawRect.yMin, drawRect.yMax, uv.y));
            vertex.uv0 = uv;

            vertexHelper.AddVert(vertex);
        }

        for (int i = 1; i < polygon.Length - 1; i++)
            vertexHelper.AddTriangle(0, i, i + 1);
    }

    private Rect GetAspectFittedRect()
    {
        Rect container = rectTransform.rect;
        float textureAspect = illustrationTexture.width / (float)illustrationTexture.height;
        float containerAspect = container.width / container.height;

        if (containerAspect > textureAspect)
        {
            float width = container.height * textureAspect;
            return new Rect(
                container.center.x - width * 0.5f,
                container.yMin,
                width,
                container.height);
        }

        float height = container.width / textureAspect;
        return new Rect(
            container.xMin,
            container.center.y - height * 0.5f,
            container.width,
            height);
    }
}
