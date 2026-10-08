using UnityEngine;

/// <summary>
/// Swings this leaf around one vertical edge of its own mesh.
/// Set Hinge Side and Open Angle in the Inspector; do not edit DoorHinge Transform rotation.
/// </summary>
public class HingedProp : MonoBehaviour
{
    public enum HingeSide
    {
        NegativeX,
        PositiveX,
    }

    [SerializeField] private Transform hinge;
    [Tooltip("Which vertical edge of this mesh is the jamb. Switch this if it pivots at the handle.")]
    [SerializeField] private HingeSide hingeSide = HingeSide.NegativeX;
    [Tooltip("How far it swings. Use -90 if it opens into the wall.")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float seconds = 0.35f;

    private Quaternion closedLocalRotation;
    private float currentAngle;
    private float targetAngle;
    private bool open;

    private void Awake()
    {
        BindHingeToLocalEdge();
        closedLocalRotation = hinge.localRotation;
    }

    public void Toggle()
    {
        open = !open;
        targetAngle = open ? openAngle : 0f;
    }

    private void Update()
    {
        if (hinge == null)
        {
            return;
        }

        var speed = Mathf.Abs(openAngle) / Mathf.Max(0.05f, seconds);
        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, speed * Time.deltaTime);
        hinge.localRotation = closedLocalRotation * Quaternion.AngleAxis(currentAngle, Vector3.up);
    }

    private void BindHingeToLocalEdge()
    {
        var filter = GetComponent<MeshFilter>();
        var bounds = filter != null && filter.sharedMesh != null
            ? filter.sharedMesh.bounds
            : new Bounds(Vector3.zero, Vector3.one);

        var edgeLocal = hingeSide == HingeSide.PositiveX
            ? new Vector3(bounds.max.x, bounds.center.y, bounds.center.z)
            : new Vector3(bounds.min.x, bounds.center.y, bounds.center.z);
        var hingeWorld = transform.TransformPoint(edgeLocal);

        if (hinge == null && transform.parent != null
            && transform.parent.name.IndexOf("Hinge", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            hinge = transform.parent;
        }

        if (hinge == null)
        {
            hinge = new GameObject(name + " Hinge").transform;
            hinge.SetParent(transform.parent, false);
        }

        transform.SetParent(null, true);
        hinge.position = hingeWorld;
        hinge.rotation = transform.rotation;
        transform.SetParent(hinge, true);
    }
}
