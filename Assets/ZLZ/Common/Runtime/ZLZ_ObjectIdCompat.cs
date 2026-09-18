using UnityEngine;

// Unity 6000.3 introduced Object.GetEntityId() and later streams (Unity 6.5+) promote
// Object.GetInstanceID() to a CS0619 compile error, so neither API alone compiles on
// every version ZLZ supports (2022.3 LTS through 6.5). Every ZLZ call site only needs
// a stable int key for dictionaries / HashSets / hash signatures, which both APIs
// provide, so the version split lives here and nowhere else.
public static class ZLZ_ObjectIdCompat
{
    public static int GetStableId(this Object obj)
    {
#if UNITY_6000_3_OR_NEWER
        // Not a straight int cast : EntityId's implicit int operator is itself a CS0619
        // error on Unity 6.5+ (ids stop fitting in an int eventually). GetHashCode() is
        // the documented stable accessor and equals the old instance id while ids are
        // still int-backed, so 6000.3 behaviour is unchanged.
        return obj.GetEntityId().GetHashCode();
#else
        return obj.GetInstanceID();
#endif
    }
}
