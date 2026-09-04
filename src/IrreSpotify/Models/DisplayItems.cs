namespace IrreSpotify.Models;

public class TrackItem
{
    public string Uri { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;
}

public class PlaylistItem
{
    public string Uri { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public int TrackCount { get; set; }
}
