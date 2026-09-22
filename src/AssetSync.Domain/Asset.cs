namespace AssetSync.Domain;

public class Asset
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Location { get; set; }
}
