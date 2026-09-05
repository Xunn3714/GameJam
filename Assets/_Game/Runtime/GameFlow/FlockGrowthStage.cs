using System;
using UnityEngine;

[Serializable]
public sealed class FlockGrowthStage
{
    [SerializeField] private string displayName;
    [SerializeField, Min(1)] private int minimumFlockSize = 1;
    [SerializeField, Min(1)] private int minimumBatchSize = 1;
    [SerializeField, Min(1)] private int maximumBatchSize = 1;
    [SerializeField, Min(0.1f)] private float cameraSize = 5f;

    public FlockGrowthStage(
        string displayName,
        int minimumFlockSize,
        int minimumBatchSize,
        int maximumBatchSize,
        float cameraSize)
    {
        this.displayName = displayName;
        this.minimumFlockSize = minimumFlockSize;
        this.minimumBatchSize = minimumBatchSize;
        this.maximumBatchSize = maximumBatchSize;
        this.cameraSize = cameraSize;
    }

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "扩张" : displayName;
    public int MinimumFlockSize => Mathf.Max(1, minimumFlockSize);
    public int MinimumBatchSize => Mathf.Max(1, minimumBatchSize);
    public int MaximumBatchSize => Mathf.Max(MinimumBatchSize, maximumBatchSize);
    public float CameraSize => Mathf.Max(0.1f, cameraSize);
}
