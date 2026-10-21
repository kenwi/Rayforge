using System.Numerics;
using Xunit;

namespace Rayforge.Tests;

public class GameBaseTests
{
    [Fact]
    public void Initialize_sets_the_context_and_calls_the_hook()
    {
        var game = new RecordingGame();
        GameContext context = TestGameContext();

        game.Initialize(context);

        Assert.Same(context, game.ExposedContext);
        Assert.True(game.InitializedCalled);
    }

    [Fact]
    public void Initialize_twice_throws_because_a_raylib_context_cannot_be_reused()
    {
        var game = new RecordingGame();
        GameContext context = TestGameContext();

        game.Initialize(context);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => game.Initialize(context));
        Assert.Contains("initialized twice", exception.Message);
    }

    [Fact]
    public void Initialize_rejects_a_null_context()
    {
        var game = new RecordingGame();

        Assert.Throws<ArgumentNullException>(() => game.Initialize(null!));
        Assert.False(game.InitializedCalled);
    }

    [Fact]
    public void OnResize_is_optional()
    {
        var game = new RecordingGame();
        game.Initialize(TestGameContext());

        game.OnResize(1920, 1080);
    }

    private static GameContext TestGameContext() => new()
    {
        Width = 1280,
        Height = 720,
        IsBrowser = false,
        AudioAvailable = false,
        Runtime = new NullRuntimeHost(),
        MouseCapture = new NullMouseCapture(),
        Log = NullGameLog.Instance,
        Diagnostics = new GameDiagnostics(),
    };

    private sealed class RecordingGame : GameBase
    {
        public bool InitializedCalled { get; private set; }

        public GameContext ExposedContext => Context;

        protected override void OnInitialize() => InitializedCalled = true;

        public override void Update(float deltaTime)
        {
        }

        public override void Draw()
        {
        }
    }

    private sealed class NullRuntimeHost : IRuntimeHost
    {
        public bool IsFullscreen => false;

        public void RequestQuit()
        {
        }

        public void ToggleFullscreen()
        {
        }
    }

    private sealed class NullMouseCapture : IMouseCapture
    {
        public bool IsCaptured => false;

        public void Request()
        {
        }

        public void Release()
        {
        }

        public Vector2 ConsumeDelta() => Vector2.Zero;
    }

    private sealed class NullGameLog : IGameLog
    {
        public static readonly NullGameLog Instance = new();

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
