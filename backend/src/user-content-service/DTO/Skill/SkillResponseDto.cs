namespace UserContentService.dto.Skill
{
    public class SkillResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Level { get; set; }
        public int? YearsOfExperience { get; set; }
        public Guid? UserId { get; set; }
        public string? Category { get; set; }
    }
}
