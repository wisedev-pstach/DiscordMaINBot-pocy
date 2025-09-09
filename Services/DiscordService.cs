using System;
using System.Threading;
using System.Threading.Tasks;
using DiscordMaINBot.Interfaces;
using DSharpPlus;
using DSharpPlus.AsyncEvents;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DiscordMaINBot.Services;

public class DiscordService(
    IOptions<BotConfig> options,
    IMaInService maInService) : IDiscordService
{
    public async Task StartAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var discordToken = options.Value.DiscordToken;
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
        client.MessageCreated += HandleNewMessage;
        //Setup random message sends
        
        await client.ConnectAsync();
        await Task.Delay(-1, cancellationToken);
    }

    private async Task HandleNewMessage(DiscordClient client, MessageCreateEventArgs args)
    {
        if (!args.Author.IsBot)
        {
            if(args.Channel.IsPrivate)
            {
                await args.Channel.TriggerTypingAsync();
                var conversationResult =
                    await maInService.ConverseAsync(
                        args.Channel.Id.ToString(), 
                        args.Message.Content);
                
                await args.Channel.SendMessageAsync(conversationResult);
            }
        }
    }
}