using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobOfferService.Entities;

[Table("search_caches")]
public class SearchCache
{
    [Key]
    public Guid SearchId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Keyword { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    public DateOnly CrawledDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public SearchStatus Status { get; set; } = SearchStatus.Pending;
    public int ExpectedCount { get; set; } = 0;
    public int ProcessedCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
