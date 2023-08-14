// See https://aka.ms/new-console-template for more information
using ExcludeFromCodeCoverageRoslynAnalyzer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<MySolutionAnalyzer>();
    });

await builder.RunConsoleAsync();