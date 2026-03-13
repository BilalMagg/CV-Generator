public class SkillService : ISkillService
{
    private readonly ISkillRepository _repository;

    public SkillService(ISkillRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<SkillEntity>> GetAll()
    {
        return await _repository.GetAll();
    }
}
