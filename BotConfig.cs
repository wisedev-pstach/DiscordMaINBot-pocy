namespace DiscordMaINBot;

public class BotConfig
{
    public required string DiscordToken { get; set; }
    public required string SystemPrompt { get; set; }
    public required string Model { get; set; }
    public string? Backend { get; set; }
}