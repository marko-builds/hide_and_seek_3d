using System.Collections;
using UnityEngine;
using UnityEngine.Pool;
using Utilities;

namespace HideAndSeek
{
    /// <summary>
    /// Pooled AudioSource component. Plays a SoundData clip then returns itself
    /// to the pool when finished. Implements IPoolable for pool lifecycle hooks.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SoundEmitter : MonoBehaviour, IPoolable
    {
        AudioSource _source;
        IObjectPool<SoundEmitter> _pool;

        void Awake() => _source = GetComponent<AudioSource>();

        public void Play(SoundData data, IObjectPool<SoundEmitter> pool)
        {
            _pool = pool;
            _source.clip = data.clip;
            _source.volume = data.volume + Random.Range(-data.volumeVariance, data.volumeVariance);
            _source.pitch = data.pitch + Random.Range(-data.pitchVariance, data.pitchVariance);
            _source.Play();
            StartCoroutine(ReturnWhenFinished());
        }

        IEnumerator ReturnWhenFinished()
        {
            yield return WaitFor.Seconds(_source.clip.length / _source.pitch);
            _pool.Release(this);
        }

        public void OnSpawn() => gameObject.SetActive(true);
        public void OnDespawn() => gameObject.SetActive(false);
    }
}
