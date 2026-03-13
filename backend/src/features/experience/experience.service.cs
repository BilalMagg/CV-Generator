public class ExperienceService : IExperienceService
{
    private readonly IExperienceRepository _repository;

    public ExperienceService(IExperienceRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ExperienceEntity>> GetAll()
    {
        return await _repository.GetAll();
    }
}
