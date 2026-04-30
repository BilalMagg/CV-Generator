cat <<EOF > "$FEATURE_PATH/repository/$FEATURE.repository.cs"
using Microsoft.EntityFrameworkCore;
using $BASE_NAMESPACE.entity;
using $BASE_NAMESPACE.interfaces;
using backend.src.shared.exceptions;

namespace $BASE_NAMESPACE.repository;

public class ${FEATURE_CAP}Repository : I${FEATURE_CAP}Repository
{
    private readonly AppDbContext _context;

    public ${FEATURE_CAP}Repository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<${FEATURE_CAP}>> GetAll()
    {
        return await _context.Set<${FEATURE_CAP}>().ToListAsync();
    }

    public async Task<${FEATURE_CAP}> GetById(Guid id)
    {
        var entity = await _context.Set<${FEATURE_CAP}>().FindAsync(id);
        
        if (entity == null)
            throw new NotFoundException($"${FEATURE_CAP} with ID {id} not found");
            
        return entity;
    }

    public async Task<${FEATURE_CAP}> Create(${FEATURE_CAP} entity)
    {
        _context.Set<${FEATURE_CAP}>().Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<${FEATURE_CAP}> Update(${FEATURE_CAP} entity)
    {
        _context.Set<${FEATURE_CAP}>().Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task Delete(Guid id)
    {
        var entity = await GetById(id);

        _context.Set<${FEATURE_CAP}>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}
EOF
