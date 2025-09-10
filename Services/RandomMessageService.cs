
using DiscordMaINBot.Interfaces;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Options;

namespace DiscordMaINBot.Services;

public class RandomMessageService(IMaInService maInService, IOptions<BotConfig> config)
{
    private readonly Random _random = new();
    private readonly BotConfig _config = config.Value;
    private DiscordClient? _client;

    public void SetClient(DiscordClient? client)
    {
        _client = client;
    }

    public async Task TryRandomMessage()
    {
        if (!_config.RandomMessagingEnabled) return;
        
        var chance = _random.Next(1, 101);
        if (chance > _config.RandomMessageChancePercent) return;

        try
        {
            var guild = _client.Guilds.Values.FirstOrDefault();
            if (guild == null) return;

            var channels = guild.Channels.Values
                .Where(c => c.Type == ChannelType.Text && c.PermissionsFor(guild.CurrentMember).HasPermission(Permissions.SendMessages))
                .ToList();

            if (!channels.Any()) return;

            // 50% chance for channel message, 50% for DM
            if (_random.Next(2) == 0)
            {
                await SendRandomChannelMessage(guild, channels);
            }
            else
            {
                await SendRandomDM(guild);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Random message error: {ex.Message}");
        }
    }

    private async Task SendRandomChannelMessage(DiscordGuild guild, List<DiscordChannel> channels)
    {
        var channel = SelectChannelByActivity(channels);
        var members = guild.Members.Values.Where(m => !m.IsBot).ToList();
        
        if (!members.Any()) return;

        var randomUser = members[_random.Next(members.Count)];
        var question = await GenerateRandomQuestion();
        
        var message = $"{randomUser.Mention} {question}";
        await channel.SendMessageAsync(message);
    }

    private DiscordChannel SelectChannelByActivity(List<DiscordChannel> channels)
    {
        if (_random.Next(100) < 70)
        {
            var activeChannels = channels
                .Where(c => c.Users?.Count() > 0 || c.PermissionOverwrites?.Any() == true)
                .OrderByDescending(c => c.Users?.Count() ?? 0)
                .Take(Math.Max(1, channels.Count / 3))
                .ToList();
                
            if (activeChannels.Any())
                return activeChannels[_random.Next(activeChannels.Count)];
        }
        
        return channels[_random.Next(channels.Count)];
    }

    private async Task SendRandomDM(DiscordGuild guild)
    {
        var members = guild.Members.Values.Where(m => !m.IsBot).ToList();
        if (!members.Any()) return;

        var randomUser = members[_random.Next(members.Count)];
        
        try
        {
            var dmChannel = await randomUser.CreateDmChannelAsync();
            var question = await GenerateRandomQuestion();
            await dmChannel.SendMessageAsync(question);
        }
        catch
        {
            // User might have DMs disabled, silently fail
        }
    }

    private async Task<string> GenerateRandomQuestion()
    {
        var prompts = new[]
        {
            "Generate a random interesting question to ask someone",
            "Create a fun conversation starter question",
            "Make up a thought-provoking question",
            "Generate a random would-you-rather question",
            "Create an interesting hypothetical question"
        };

        var randomPrompt = prompts[_random.Next(prompts.Length)];
        return await maInService.AskQuestionAsync(randomPrompt);
    }
}