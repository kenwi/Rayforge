using DoomFire;
using DoomFire.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Rayforge;
using Rayforge.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddRayforge<DoomFireGame>(options =>
{
    options.Title = "Doom Fire";
    options.Width = 960;
    options.Height = 540;
    // The original reads GetRenderWidth/Height every frame and rebuilds the heat grid.
    options.ScaleMode = CanvasScaleMode.Fill;
    options.AssetMode = AssetMode.Bundled;
    options.EnableAudio = false;
    options.RequireUserGesture = false;
});

await builder.Build().RunAsync();
