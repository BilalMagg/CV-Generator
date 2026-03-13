public interface IExperienceRepository
{
    Task<List<ExperienceEntity>> GetAll();
}
