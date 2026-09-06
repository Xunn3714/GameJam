using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpecialSheepMarker : MonoBehaviour
{
    [SerializeField] private string sheepTypeId;
    [SerializeField] private string typeName;
    [SerializeField] private SheepQuality quality;
    [SerializeField] private string visualEffectId;
    [SerializeField] private string abilityId;

    public string SheepTypeId => sheepTypeId;
    public string TypeName => typeName;
    public SheepQuality Quality => quality;
    public string VisualEffectId => visualEffectId;
    public string AbilityId => abilityId;

    public void Configure(SpecialSheepSelection selection)
    {
        Configure(
            selection.TypeId,
            selection.TypeName,
            selection.Quality,
            selection.VisualEffectId,
            selection.AbilityId);
    }

    public void Configure(
        string typeId,
        string displayName,
        SheepQuality sheepQuality,
        string effectId = "",
        string featureId = "")
    {
        sheepTypeId = typeId?.Trim();
        typeName = displayName?.Trim();
        quality = sheepQuality;
        visualEffectId = effectId?.Trim();
        abilityId = featureId?.Trim();

        SheepIdentity identity = GetComponent<SheepIdentity>();
        identity ??= gameObject.AddComponent<SheepIdentity>();
        identity.AssignType(sheepTypeId);

        // 特效初始化不能阻断特殊羊生成。这里只挂载轻量组件；粒子在获得时才创建。
        try
        {
            SpecialSheepAcquisitionVfx.Ensure(gameObject);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
        {
            if (component is not ISpecialSheepFeature feature)
                continue;

            try
            {
                feature.OnSpecialSheepSpawned(this);
            }
            catch (System.Exception exception)
            {
                // 单个表现或未来技能失效时，羊本身仍必须正常生成。
                Debug.LogException(exception, component);
            }
        }
    }

    internal void NotifyRecruited(FlockController flock)
    {
        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
        {
            if (component is not ISpecialSheepFeature feature)
                continue;

            try
            {
                feature.OnSpecialSheepRecruited(this, flock);
            }
            catch (System.Exception exception)
            {
                // 招募已经提交，不允许表现层异常留下半完成的羊群状态。
                Debug.LogException(exception, component);
            }
        }
    }
}
