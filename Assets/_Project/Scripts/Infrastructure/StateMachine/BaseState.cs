namespace HideAndSeek
{
    /// <summary>
    /// Abstract base for FSM states. Provides empty virtual implementations
    /// so concrete states only override what they need.
    /// </summary>
    public abstract class BaseState : IState
    {
        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
    }
}
