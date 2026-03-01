using UnityEngine;

namespace Utilities
{
    /// <summary>
    /// Scene-bound singleton: will NOT auto-create an instance.
    /// Use this for managers that require serialized references set in the scene (e.g., UI bindings).
    /// </summary>
    public class SceneSingleton<T> : MonoBehaviour where T : Component
    {
        protected static T instance;

        public static bool HasInstance => instance != null;
        public static T TryGetInstance() => HasInstance ? instance : null;

        public static T Instance
        {
            get
            {
                if (!instance)
                {
                    instance = Object.FindAnyObjectByType<T>();
                    if (!instance)
                    {
                        Debug.LogError($"[SceneSingleton] No instance of {typeof(T).Name} found in the scene. " +
                                       "This component must be present in the scene with its serialized references assigned.");
                    }
                }

                return instance;
            }
        }

        protected virtual void OnEnable()
        {
            InitializeSingleton();
        }

        protected virtual void InitializeSingleton()
        {
            if (!Application.isPlaying) return;

            if (instance == null)
            {
                instance = this as T;
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }
    }
}
