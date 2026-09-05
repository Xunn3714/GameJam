using UnityEngine;

/// <summary>
/// 一局的随机种子：0 表示每局随机，否则固定。羊刷新、可破坏物散布等都从这里派生自己的随机源，
/// 这样同一个种子能复现同一张地图。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class WorldSeed : MonoBehaviour
{
    [Tooltip("0 = 每局随机；非 0 = 固定种子，可复现地图。")]
    [SerializeField] private int fixedSeed;

    private bool resolved;
    private int seed;

    public int Seed
    {
        get
        {
            Resolve();
            return seed;
        }
    }

    /// <summary>按用途派生独立的随机源，不同用途互不干扰。</summary>
    public System.Random CreateRandom(int streamIndex)
    {
        unchecked
        {
            return new System.Random(Seed + streamIndex * 7919);
        }
    }

    private void Awake()
    {
        Resolve();
    }

    private void Resolve()
    {
        if (resolved)
            return;

        resolved = true;
        seed = fixedSeed != 0 ? fixedSeed : Random.Range(1, int.MaxValue);
        Debug.Log($"本局世界种子：{seed}", this);
    }
}
