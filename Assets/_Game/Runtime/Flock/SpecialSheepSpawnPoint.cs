using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpecialSheepSpawnPoint : MonoBehaviour
{
    public Vector2 Position => transform.position;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.65f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.75f);
        Gizmos.DrawLine(transform.position + Vector3.left, transform.position + Vector3.right);
        Gizmos.DrawLine(transform.position + Vector3.down, transform.position + Vector3.up);
    }
}
