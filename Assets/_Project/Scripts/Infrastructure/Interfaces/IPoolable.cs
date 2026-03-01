namespace HideAndSeek
{
    /// <summary>
    /// Implemented by objects managed by an ObjectPool (SoundEmitter, VFX, etc.).
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}
