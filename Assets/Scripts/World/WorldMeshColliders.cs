using UnityEngine;

/// <summary>
/// MarpaStudio prefabs ship without colliders. Adds a MeshCollider on every
/// mesh under this object so the CharacterController can stand on floors and
/// stop at walls.
/// </summary>
[DefaultExecutionOrder(-200)]
public class WorldMeshColliders : MonoBehaviour
{
    private void Awake()
    {
        var filters = GetComponentsInChildren<MeshFilter>(true);
        for (var i = 0; i < filters.Length; i++)
        {
            var filter = filters[i];
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            if (filter.GetComponent<Collider>() != null)
            {
                continue;
            }

            var meshCollider = filter.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = filter.sharedMesh;
        }
    }
}
