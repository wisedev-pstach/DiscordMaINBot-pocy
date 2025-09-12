namespace DiscordMaINBot;

public class BotConfig
{
    public required string DiscordToken { get; set; }
    public required string SystemPrompt { get; set; }
    public required BotPersonality Personality { get; set; }
    public required string Model { get; set; }
    public string? Backend { get; set; }
    public bool RandomMessagingEnabled { get; set; } = true;
    public int RandomMessageChancePercent { get; set; } = 10; 
    public int RandomMessageIntervalMinutes { get; set; } = 30; 
}

public class BotPersonality
{
    public required string Mood { get; set; }
    public required string Trait { get; set; }
    public string? Backstory { get; set; }
    public string[]? Quirks { get; set; }
}