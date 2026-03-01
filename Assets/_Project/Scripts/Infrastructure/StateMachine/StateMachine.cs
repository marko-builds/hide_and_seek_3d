namespace HideAndSeek
{
    /// <summary>
    /// Generic finite state machine. Owns the current IState and drives transitions.
    /// Call Tick() from the owning MonoBehaviour's Update.
    /// </summary>
    public class StateMachine
    {
        private IState _currentState;

        public IState CurrentState => _currentState;

        public void ChangeState(IState newState)
        {
            _currentState?.Exit();
            _currentState = newState;
            _currentState?.Enter();
        }

        public void Tick()
        {
            _currentState?.Tick();
        }
    }
}
