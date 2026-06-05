using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobOfferService.Entities;

[Table("search_job_matches")]
public class SearchJobMatch
{
    [Key]
    public Guid Id { get; set; }

    public Guid SearchId { get; set; }

    public Guid JobId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
