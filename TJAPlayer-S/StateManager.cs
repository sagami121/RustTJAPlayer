namespace TjaPlayer;

public enum AppStateEnum
{
    SongSelect,
    Playing
}

public interface IAppState
{
    void Update();
    void Render();
    AppStateEnum State { get; }
}

public class StateManager
{
    public IAppState CurrentState { get; private set; }
    
    public StateManager(IAppState initialState)
    {
        CurrentState = initialState;
    }

    public void ChangeState(IAppState newState)
    {
        CurrentState = newState;
    }

    public void Update()
    {
        CurrentState.Update();
    }

    public void Render()
    {
        CurrentState.Render();
    }
}
