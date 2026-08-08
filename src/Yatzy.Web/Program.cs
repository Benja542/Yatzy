using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Yatzy.Core.Game;
using Yatzy.Core.Probability;
using Yatzy.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Spillet og den løste sandsynlighedsmodel deles på tværs af siderne, så man kan
// skifte til sandsynlighedstabellen midt i en tur uden at miste terningerne.
builder.Services.AddSingleton(_ => OutcomeTreeSolver.Instance);
builder.Services.AddSingleton(_ => new GameEngine());

await builder.Build().RunAsync();
