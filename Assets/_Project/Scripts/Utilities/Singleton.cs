using UnityEngine;

namespace Utilities
{
    public class Singleton<T> : MonoBehaviour where T : Component
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
                    instance = FindAnyObjectByType<T>();
                    if (!instance)
                    {
                        var go = new GameObject(typeof(T).Name + " Auto-Generated");
                        instance = go.AddComponent<T>();
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