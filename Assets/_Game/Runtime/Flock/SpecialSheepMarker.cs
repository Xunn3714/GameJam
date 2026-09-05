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
        sheepTypeId = selection.TypeId;
        typeName = selection.TypeName;
        quality = selection.Quality;
        visualEffectId = selection.VisualEffectId;
        abilityId = selection.AbilityId;

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
