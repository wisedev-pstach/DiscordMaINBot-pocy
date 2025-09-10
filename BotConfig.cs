namespace DiscordMaINBot;

public class BotConfig
{
    public required string DiscordToken { get; set; }
    public required string SystemPrompt { get; set; }
    public required string Model { get; set; }
    public string? Backend { get; set; }
    public bool RandomMessagingEnabled { get; set; } = true;
    public int RandomMessageChancePercent { get; set; } = 10; 
    public int RandomMessageIntervalMinutes { get; set; } = 30; 

}