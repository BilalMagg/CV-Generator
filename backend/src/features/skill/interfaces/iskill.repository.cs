public interface ISkillRepository
{
    Task<List<SkillEntity>> GetAll();
}
