namespace MoodyClone.Models;

public class Script
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "Untitled";
    public string Content { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.Now;
}
