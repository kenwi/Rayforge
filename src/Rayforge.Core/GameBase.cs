namespace Rayforge;

/// <summary>
/// Convenience base for <see cref="IGame"/>. Stores the context and exposes lifecycle hooks.
/// Optional: a game may implement <see cref="IGame"/> directly.
/// </summary>
public abstract class GameBase : IGame
{
    /// <summary>
    /// Platform services for this run. Valid from <see cref="OnInitialize"/> until <see cref="Dispose"/>.
    /// Never wrap raylib rendering in here - drawing stays in <see cref="Draw"/>.
    /// </summary>
    protected GameContext Context { get; private set; } = null!;

    /// <inheritdoc />
    public void Initialize(GameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Context is not null)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} was initialized twice. A raylib context cannot be reused; reload the page.");
        }

        Context = context;
        OnInitialize();
    }

    /// <summary>Game setup: load textures/sounds, create state. The window and audio device already exist.</summary>
    protected abstract void OnInitialize();

    /// <inheritdoc />
    public abstract void Update(float deltaTime);

    /// <inheritdoc />
    public abstract void Draw();

    /// <inheritdoc />
    public virtual void OnResize(int width, int height)
    {
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
    }
}
