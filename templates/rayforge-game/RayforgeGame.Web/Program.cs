using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Rayforge;
using Rayforge.Web;
using RayforgeGame;
using RayforgeGame.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The web host is only configuration. Game rules, assets and rendering belong in RayforgeGame.Game.
builder.Services.AddRayforge<ExampleGame>(options =>
{
    options.Title = "Rayforge Game";
    options.Width = 1280;
    options.Height = 720;
    options.ScaleMode = CanvasScaleMode.Fit;
    options.AssetMode = AssetMode.Bundled;

    // No audio asset in the template: start immediately, without a click-to-play gesture.
    options.EnableAudio = false;
    options.RequireUserGesture = false;
});

await builder.Build().RunAsync();
