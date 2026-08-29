namespace CV_Generator.Dto;

public class CategoryNodeDto
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Path { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public bool IsSystem { get; set; }
    public Guid? UserId { get; set; }

    /// <summary>Number of the user's entities tagged under this node or any descendant.</summary>
    public int Count { get; set; }

    public List<CategoryNodeDto> Children { get; set; } = new();
}

public class CategorySearchRequest
{
    public List<Guid> NodeIds { get; set; } = new();
    public List<string>? SourceTypes { get; set; }
}

public class CategorySearchResult
{
    public Guid SourceId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int Score { get; set; }
}

public class CategoryTagRequest
{
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public List<Guid> NodeIds { get; set; } = new();
}
