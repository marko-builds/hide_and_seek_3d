using UnityEngine;
using UnityEngine.Pool;
using Utilities;

namespace HideAndSeek
{
    /// <summary>
    /// Cross-scene Singleton. Plays sounds by SoundID using an ObjectPool of SoundEmitters.
    /// Callers never reference AudioSource or ResourcesLoad directly.
    /// </summary>
    public class AudioManager : Singleton<AudioManager>
    {
        [SerializeField] private SoundLibrary _library;
        [SerializeField] private SoundEmitter _emitterPrefab;

        private ObjectPool<SoundEmitter> _pool;

        protected override void OnEnable()
        {
            base.OnEnable();
            _pool = new ObjectPool<SoundEmitter>(
                createFunc: () => Instantiate(_emitterPrefab, transform),
                actionOnGet: e => e.OnSpawn(),
                actionOnRelease: e => e.OnDespawn(),
                actionOnDestroy: e => Destroy(e.gameObject),
                defaultCapacity: 10,
                maxSize: 30
            );
        }

        public void Play(SoundID id)
        {
            SoundData data = _library.Get(id);
            if (data == null) return;

            SoundEmitter emitter = _pool.Get();
            emitter.Play(data, _pool);
        }

        public void PlayAt(SoundID id, Vector3 position)
        {
            SoundData data = _library.Get(id);
            if (data == null) return;

            SoundEmitter emitter = _pool.Get();
            emitter.transform.position = position;
            emitter.Play(data, _pool);
        }
    }
}
