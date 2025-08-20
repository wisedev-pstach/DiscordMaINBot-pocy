using System;
using System.Threading;
using System.Threading.Tasks;
using DiscordMaINBot.Interfaces;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Configuration;

namespace DiscordMaINBot.Services;

public class DiscordService(IConfiguration configuration) : IDiscordService
{
    public async Task StartAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var discordToken = configuration["DiscordToken"];
        var discordConfiguration = new DiscordConfiguration
        {
            Intents = DiscordIntents.All,
            Token = discordToken,
            TokenType = TokenType.Bot,
            AutoReconnect = true
        };
        var client = new DiscordClient(discordConfiguration);

        var slash = client.UseSlashCommands(new SlashCommandsConfiguration
        {
            Services = serviceProvider
        });
        
        slash.RegisterCommands<Commands.Commands>();
        
        await client.ConnectAsync();
        await Task.Delay(-1, cancellationToken);
    }
}