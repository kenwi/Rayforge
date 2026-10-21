using HelloWorld;
using HelloWorld.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Rayforge;
using Rayforge.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddRayforge<HelloWorldGame>(options =>
{
    options.Title = "Hello, Rayforge!";
    options.Width = 1280;
    options.Height = 720;
    options.ScaleMode = CanvasScaleMode.Fit;
    options.AssetMode = AssetMode.Bundled;
    options.EnableAudio = true;
    options.RequireUserGesture = true;
});

await builder.Build().RunAsync();
