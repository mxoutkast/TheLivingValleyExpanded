namespace TheLivingValleyExpanded;

public sealed class SveLoreFile
{
    public Dictionary<string, SveNpcLoreEntry> Npcs { get; set; } = new();
    public Dictionary<string, string> Locations { get; set; } = new();
}

public sealed class SveNpcLoreEntry
{
    public string Role { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public string Speech { get; set; } = string.Empty;
    public string Ties { get; set; } = string.Empty;
    public string Boundaries { get; set; } = string.Empty;
}
