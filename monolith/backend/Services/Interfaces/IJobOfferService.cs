using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

public interface IJobOfferService
{
    Task<List<JobOffer>> GetAllAsync(Guid userId);
    Task<JobOffer?> GetByIdAsync(Guid id, Guid userId);
    Task<JobOffer> CreateAsync(JobOffer jobOffer);
    Task<JobOffer?> UpdateAsync(Guid id, JobOffer jobOffer);
    Task<bool> DeleteAsync(Guid id, Guid userId);
}
