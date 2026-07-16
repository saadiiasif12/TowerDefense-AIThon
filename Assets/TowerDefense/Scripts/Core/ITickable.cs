namespace RoyalSiege.Core
{
    /// <summary>Receives the fixed 10 Hz logic tick. Register via <see cref="ITicker"/>.</summary>
    public interface ITickable
    {
        void Tick(float dt);
    }

    /// <summary>Registry for logic-tick participants.</summary>
    public interface ITicker
    {
        void Register(ITickable tickable);
        void Unregister(ITickable tickable);
    }
}
