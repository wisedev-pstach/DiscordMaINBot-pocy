using System;
using System.IO;
using DiscordMaINBot.Interfaces;
using DiscordMaINBot.Services;
using MaIN.Core;
using MaIN.Domain.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMaINBot;

public static class Extensions
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IDiscordService, DiscordService>();
        services.AddTransient<IMaInService, MaInService>();
        services.AddSingleton(configuration);
        
        return services;
    }
    
    public static IServiceCollection AddMaIn(this IServiceCollection services, IConfiguration configuration)
    {
        // services.AddMaIN(configuration, (options) =>
        // {
        //     options.BackendType = BackendType.Gemini;
        //     options.GeminiKey = "AIzaSyA9ZjnO0QKIiGVgGXyReaUmxPG2wAbbxZg";
        // });

        services.AddMaIN(configuration);
        
        var provider = services.BuildServiceProvider();
        provider.UseMaIN(); 
        
        return services;
    }
    
    public static IConfiguration RegisterConfiguration(this IServiceCollection services)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(Path.Combine(path, "appsettings.json"), optional: false, reloadOnChange: true)
            .Build();
        
        services.AddSingleton(configuration);
        
        return configuration;
    }
}