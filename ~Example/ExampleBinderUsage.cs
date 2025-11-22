using OSK;
using UnityEngine;
using OSK.Bindings;
using Sirenix.OdinInspector;

public class ExampleBinderUsage : MonoBehaviour
{
    // 1. From Self
    [Bind(From.Self)]
    public Rigidbody selfRigidbody;

    // 2. From Children by Name
    [Bind(From.Children, FindBy.Name, Name = "BB", IncludeInactive = true)]
    public Collider childCollider;

    // 3. From Scene by Tag
    [Bind(From.Scene, FindBy.Tag, Tag = "Player")]
    public Transform playerTransform;

    // 4. From Resources
    [Bind(From.Resources, ResourcePath = "Prefabs/Cube")]
    public GameObject resourcePrefab;

    // 5. Static Factory Create
    [Bind(From.StaticMethod, StaticType = typeof(Factory), MethodName = "CreateSphere")]
    public GameObject staticCreated;

    // 6. Instance Method Create
    [Bind(From.Method, MethodName = nameof(CreateLocalObject))]
    public GameObject instanceCreated;
    
    //7. Interface Binding
    [Bind(From.Children, FindBy.Type, IncludeInactive = true)]
    public ExampleInterface exampleInterface;


    // ------------------------- DEMO METHODS --------------------------

    // Static method for StaticMethod binding
    public static class Factory
    {
        private static GameObject go;
        public static GameObject CreateSphere()
        {
            if(go == null) go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "StaticGeneratedSphere";
            Debug.Log("[Static Factory] Created Sphere!");
            return go;
        }
    }

    private static GameObject go;
    // Instance method for Method binding
    private GameObject CreateLocalObject()
    {
        if(go == null)  go = new GameObject("LocalInstanceCreated");
        go.AddComponent<BoxCollider>();
        Debug.Log("[Instance Method] Created Local Object!");
        return go;
    }

    // -------------------------------------------

    [Button]
    void TestBind()
    {
        Binder.AutoBind(this);
        Debug.Log("<color=yellow>AutoBind executed!</color>");
    }

    void Awake()
    {
        Binder.AutoBind(this);
    } 
}
