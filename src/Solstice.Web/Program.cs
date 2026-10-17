using Flare.Abstractions;
using Flare.Abstractions.Tokens;
using Flare.Extensions;
using Flare.Theme.MaterialDesign3;
using Flare.Theme.MaterialDesign3.Tokens;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Solstice.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Flare：组件库本身不带主题，MD3 由独立包提供。
builder.Services.AddFlare(opts =>
{
    opts.DefaultTheme = new MaterialDesign3Theme();
    opts.DefaultPalette = Md3Palettes.Violet;
    opts.DefaultMode = ThemeMode.Auto;
});

await builder.Build().RunAsync();
