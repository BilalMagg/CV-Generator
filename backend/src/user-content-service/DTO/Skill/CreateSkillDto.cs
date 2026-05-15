using System.ComponentModel.DataAnnotations;

namespace UserContentService.dto.Skill{

    public class CreateSkillDto {


    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(20)]
    public string? Level { get; set; }

    public int? YearsOfExperience { get; set; }


    [MaxLength(50)]
    public string? Category { get; set; }
    }
}