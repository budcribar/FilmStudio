namespace PageToMovie.Core.Models;

public sealed class CreatorProfileDto
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public int MoviesPublished { get; set; }
    public int TotalUpvotes { get; set; }
    public int ForksSpawned { get; set; }
    public List<CreatorBadgeDto> Badges { get; set; } = new();
}

public sealed class CreatorBadgeDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Description { get; set; } = "";
}
