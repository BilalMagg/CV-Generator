using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;


[Table("user_quotas")]
public class UserQuota
{
    [Key]
    public Guid UserId { get; set; }

    public DateOnly LastCrawlDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
}
