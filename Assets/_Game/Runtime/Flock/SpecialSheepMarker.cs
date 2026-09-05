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

        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            if (component is ISpecialSheepFeature feature) feature.OnSpecialSheepSpawned(this);
    }

    internal void NotifyRecruited(FlockController flock)
    {
        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            if (component is ISpecialSheepFeature feature) feature.OnSpecialSheepRecruited(this, flock);
    }
}
