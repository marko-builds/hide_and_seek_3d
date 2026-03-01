namespace HideAndSeek
{
    /// <summary>
    /// Contract for all FSM states. Enter is called once on transition in,
    /// Tick is called every Update, Exit is called once on transition out.
    /// </summary>
    public interface IState
    {
        void Enter();
        void Tick();
        void Exit();
    }
}
