namespace TheLivingValleyExpanded;

public sealed class ModConfig
{
    public bool EnableSveLoreInjection { get; set; } = true;
    public string LoreLocaleOverride { get; set; } = string.Empty;
    public bool IncludeFriendshipNpcsWhenSVEInstalled { get; set; } = true;
    public string AdditionalNpcNamesCsv { get; set; } = string.Empty;
}
