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
