using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using WorkflowService.Entity;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/vectors")]
public class VectorSearchController : ControllerBase
{
    private readonly WorkflowDbContext _db;

    public VectorSearchController(WorkflowDbContext db)
    {
        _db = db;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncVectors([FromBody] SyncVectorsRequest req)
    {
        // Delete existing vectors for this user to do a fresh sync
        var existing = await _db.AgentDocumentChunks
            .Where(x => x.UserId == req.UserId)
            .ToListAsync();
            
        _db.AgentDocumentChunks.RemoveRange(existing);

        var newChunks = req.Chunks.Select(c => new AgentDocumentChunk
        {
            UserId = req.UserId,
            SourceType = c.SourceType,
            SourceId = c.SourceId,
            Content = c.Content,
            Embedding = new Vector(c.Embedding)
        });

        _db.AgentDocumentChunks.AddRange(newChunks);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] VectorSearchRequest req)
    {
        var queryVector = new Vector(req.QueryVector);
        
        // BMO 70/30 Hybrid Search combining semantic similarity and exact keyword match.
        // It uses FromSqlRaw to execute the PostgreSQL specific operations securely.
        var sqlQuery = @"
            SELECT *
            FROM ""AgentDocumentChunks""
            WHERE ""UserId"" = {2}
            ORDER BY 
                (COALESCE((1 - (""Embedding"" <=> {0})), 0) * 0.7) +
                (COALESCE(ts_rank_cd(""SearchVector"", plainto_tsquery('english', {1})), 0) * 0.3) DESC
            LIMIT {3}
        ";

        var results = await _db.AgentDocumentChunks
            .FromSqlRaw(sqlQuery, queryVector, req.QueryText, req.UserId, req.Limit)
            .Select(x => new VectorSearchResult(x.SourceId, x.SourceType))
            .ToListAsync();

        return Ok(ApiResponse<List<VectorSearchResult>>.Ok(results));
    }

    [HttpGet("status/{userId}")]
    public async Task<IActionResult> GetStatus(Guid userId)
    {
        var hasVectors = await _db.AgentDocumentChunks.AnyAsync(x => x.UserId == userId);
        return Ok(ApiResponse<bool>.Ok(hasVectors));
    }
}

public record SyncVectorsRequest(Guid UserId, List<DocumentChunkDto> Chunks);
public record DocumentChunkDto(string SourceType, Guid SourceId, string Content, float[] Embedding);

public record VectorSearchRequest(Guid UserId, string QueryText, float[] QueryVector, int Limit = 15);
public record VectorSearchResult(Guid SourceId, string SourceType);
