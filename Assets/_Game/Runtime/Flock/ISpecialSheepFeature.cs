/// <summary>
/// 特殊羊特效和技能的运行时扩展点。实现组件应挂在对应特殊羊 Prefab 上。
/// </summary>
public interface ISpecialSheepFeature
{
    void OnSpecialSheepSpawned(SpecialSheepMarker sheep);

    void OnSpecialSheepRecruited(SpecialSheepMarker sheep, FlockController flock);
}
