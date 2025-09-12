using System.Collections.Concurrent;
using DiscordMaINBot;
using DiscordMaINBot.Interfaces;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Options;

public class RandomMessageService(IMaInService maInService, IOptions<BotConfig> config)
{
    private readonly Random _random = new();
    private readonly BotConfig _config = config.Value;
    private DiscordClient _client;
    private readonly ConcurrentDictionary<ulong, (DateTime timestamp, string message)> _recentBotMessages = new();
    private readonly ConcurrentDictionary<ulong, List<DateTime>> _channelMessageHistory = new();

    public void SetClient(DiscordClient client)
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
                .Where(c => c.Type == ChannelType.Text &&
                            c.PermissionsFor(guild.CurrentMember).HasPermission(Permissions.SendMessages))
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
        // Filter out channels where bot was the last to speak
        var availableChannels = new List<DiscordChannel>();

        foreach (var chan in channels)
        {
            try
            {
                var lastMessage = (await chan.GetMessagesAsync(1)).FirstOrDefault();
                if (lastMessage == null || !lastMessage.Author.IsBot || lastMessage.Author.Id != _client.CurrentUser.Id)
                {
                    availableChannels.Add(chan);
                }
            }
            catch
            {
                continue;
            }
        }

        if (!availableChannels.Any())
        {
            Console.WriteLine("Bot was last to speak in all channels, skipping random message");
            return;
        }

        // Apply penalty to channels that were messaged recently
        var channel = SelectChannelWithPenalty(availableChannels);
        var members = guild.Members.Values.Where(m => !m.IsBot).ToList();

        if (!members.Any()) return;

        var randomUser = members[_random.Next(members.Count)];
        var question = await GenerateRandomQuestion(channel.Id.ToString(), cache: false);

        var message = $"{randomUser.Mention} {question}";
        await channel.SendMessageAsync(message);

        _recentBotMessages[channel.Id] = (DateTime.UtcNow, question);
        RecordChannelUsage(channel.Id);
    }

    private DiscordChannel SelectChannelWithPenalty(List<DiscordChannel> channels)
    {
        var now = DateTime.UtcNow;
        var weightedChannels = new List<(DiscordChannel channel, double weight)>();

        foreach (var channel in channels)
        {
            var baseWeight = 1.0;

            // Apply activity bonus (70/30 rule)
            var isActive = channel.Users?.Count() > 0 || channel.PermissionOverwrites?.Any() == true;
            if (isActive && channels.Count > 1)
            {
                var topThird = channels.Count / 3;
                var activeChannels = channels
                    .OrderByDescending(c => c.Users?.Count() ?? 0)
                    .Take(Math.Max(1, topThird))
                    .ToList();

                if (activeChannels.Contains(channel))
                {
                    baseWeight *= 2.3; // 70/30 ratio
                }
            }

            // Apply penalty for recent usage
            if (_channelMessageHistory.TryGetValue(channel.Id, out var history))
            {
                // Clean old entries (older than 2 hours)
                history.RemoveAll(time => now - time > TimeSpan.FromHours(2));

                var recentMessages = history.Count;
                if (recentMessages > 0)
                {
                    // Penalty increases exponentially: 50% for 1st repeat, 25% for 2nd, 12.5% for 3rd, etc.
                    var penalty = Math.Pow(0.5, recentMessages);
                    baseWeight *= penalty;

                    Console.WriteLine(
                        $"Channel {channel.Name}: {recentMessages} recent messages, penalty: {penalty:F2}, final weight: {baseWeight:F2}");
                }
            }

            weightedChannels.Add((channel, baseWeight));
        }

        // Select based on weighted random
        return SelectWeightedRandom(weightedChannels);
    }

    private DiscordChannel SelectWeightedRandom(List<(DiscordChannel channel, double weight)> weightedChannels)
    {
        var totalWeight = weightedChannels.Sum(x => x.weight);
        var randomValue = _random.NextDouble() * totalWeight;

        var currentWeight = 0.0;
        foreach (var (channel, weight) in weightedChannels)
        {
            currentWeight += weight;
            if (randomValue <= currentWeight)
            {
                return channel;
            }
        }

        // Fallback to last channel if something goes wrong
        return weightedChannels.Last().channel;
    }

    private void RecordChannelUsage(ulong channelId)
    {
        var now = DateTime.UtcNow;
        _channelMessageHistory.AddOrUpdate(
            channelId,
            new List<DateTime> { now },
            (key, existing) =>
            {
                // Clean old entries
                existing.RemoveAll(time => now - time > TimeSpan.FromHours(2));
                existing.Add(now);
                return existing;
            });
    }

    private DiscordChannel SelectChannelByActivity(List<DiscordChannel> channels)
    {
        // 70% chance for most active channels, 30% for any channel
        if (_random.Next(100) < 70)
        {
            // Get channels sorted by member count (approximated by permissions)
            var activeChannels = channels
                .Where(c => c.Users?.Count() > 0 || c.PermissionOverwrites?.Any() == true)
                .OrderByDescending(c => c.Users?.Count() ?? 0)
                .Take(Math.Max(1, channels.Count / 3)) // Top 1/3 of channels
                .ToList();

            if (activeChannels.Any())
                return activeChannels[_random.Next(activeChannels.Count)];
        }

        // Fallback to any random channel
        return channels[_random.Next(channels.Count)];
    }

    public bool HasRecentMessage(ulong channelId, out string recentMessage)
    {
        recentMessage = "";
        if (!_recentBotMessages.TryGetValue(channelId, out var data))
            return false;

        // Consider messages from last 10 minutes as "recent"
        if (DateTime.UtcNow - data.timestamp > TimeSpan.FromMinutes(10))
        {
            _recentBotMessages.TryRemove(channelId, out _);
            return false;
        }

        recentMessage = data.message;
        return true;
    }

    public void ClearRecentMessage(ulong channelId)
    {
        _recentBotMessages.TryRemove(channelId, out _);
    }

    private async Task SendRandomDM(DiscordGuild guild)
    {
        var members = guild.Members.Values.Where(m => !m.IsBot).ToList();
        if (!members.Any()) return;

        var randomUser = members[_random.Next(members.Count)];

        try
        {
            var dmChannel = await randomUser.CreateDmChannelAsync();
            var question = await GenerateRandomQuestion(dmChannel.Id.ToString(), cache: true);
            await dmChannel.SendMessageAsync(question);
        }
        catch
        {
            // User might have DMs disabled, silently fail
        }
    }

    private async Task<string> GenerateRandomQuestion(string channelId, bool cache)
    {
        var prompts = new[]
        {
            // Philosophical & Deep Questions
            "Generate a mind-bending philosophical question that will make people question reality",
            "Create a deep question about technology that developers would actually debate",
            "Make up a thought-provoking question about the future of AI and programming",
            "Generate a question that explores the ethics of modern software development",
            "Create a philosophical question about digital consciousness and reality",

            // Would You Rather - Tech Edition
            "Generate a would-you-rather question specifically for programmers and developers",
            "Create a would-you-rather about choosing between different programming languages or frameworks",
            "Make up a would-you-rather question about tech career choices",
            "Generate a would-you-rather about different coding approaches or methodologies",
            "Create a would-you-rather question about development tools and environments",

            // Hypothetical Scenarios
            "Create an interesting hypothetical scenario about working in tech",
            "Generate a 'what if' question about a world where programming works differently",
            "Make up a hypothetical question about debugging in impossible situations",
            "Create a scenario where interns have to choose between different project approaches",
            "Generate a hypothetical about encountering weird legacy code",

            // Personal & Introspective
            "Generate a question that makes people reflect on their coding journey",
            "Create a question about the most satisfying moment in programming",
            "Make up a question about overcoming imposter syndrome in tech",
            "Generate a question about balancing perfectionism with shipping code",
            "Create a question about finding motivation during difficult projects",

            // Fun & Creative
            "Generate a creative question about naming things in code",
            "Create a fun question about the personality of different programming languages",
            "Make up a question about if programming concepts were people",
            "Generate a silly but engaging question about developer stereotypes",
            "Create a question about the secret life of variables and functions",

            // Conversation Starters
            "Generate an icebreaker question perfect for developer team bonding",
            "Create a question that gets people talking about their coding preferences",
            "Make up a question about the weirdest bug someone has encountered",
            "Generate a conversation starter about productivity and coding habits",
            "Create a question about memorable coding failures and lessons learned",

            // Debate & Opinion
            "Generate a controversial but fun programming opinion question",
            "Create a question that will start a friendly debate about best practices",
            "Make up a question about choosing between popular development approaches",
            "Generate a question about the trade-offs in software architecture",
            "Create a debate starter about modern vs traditional development methods",

            // Nostalgia & Stories
            "Generate a question about the evolution of programming and technology",
            "Create a question that brings up coding nostalgia and old technologies",
            "Make up a question about learning to code and early programming memories",
            "Generate a question about how development has changed over the years",
            "Create a storytelling prompt about memorable coding experiences",

            // Problem Solving
            "Generate a creative coding challenge or brain teaser question",
            "Create a question about approaching complex problems differently",
            "Make up a question about debugging strategies and methodologies",
            "Generate a question about learning new technologies effectively",
            "Create a problem-solving scenario that developers can relate to",

            // Industry & Future
            "Generate a question about where the tech industry is heading",
            "Create a question about emerging technologies and their impact",
            "Make up a question about the future role of developers",
            "Generate a question about adapting to constant technological change",
            "Create a forward-thinking question about the evolution of coding",

            // Meta & Self-Aware
            "Generate a self-aware question about being an AI talking to developers",
            "Create a meta question about the nature of asking questions",
            "Make up a question about the relationship between humans and AI in coding",
            "Generate a question about automation and the future of programming work",
            "Create a thought experiment about AI consciousness in development tools"
        };

        var randomPrompt = prompts[_random.Next(prompts.Length)];
        return await maInService.ConverseAsync(channelId, randomPrompt, cache);
    }
}