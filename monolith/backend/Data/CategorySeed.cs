using CV_Generator.Models;
using Microsoft.EntityFrameworkCore;

namespace CV_Generator.Data;

/// <summary>
/// Idempotent seeder for the curated category taxonomy. Runs once at startup when CategoryNodes is empty.
/// Trees are per entity-scope; the top-level node name doubles as the Domain.
/// </summary>
public static class CategorySeed
{
    private class SeedNode
    {
        public string Name { get; init; } = string.Empty;
        public List<string> Keywords { get; init; } = new();
        public List<SeedNode> Children { get; init; } = new();
    }

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.CategoryNodes.AnyAsync())
            return;

        var all = new List<(string Scope, List<SeedNode> Roots)>
        {
            ("projects", Projects()),
            ("experiences", Experiences()),
            ("educations", Educations()),
            ("certifications", Certifications()),
            ("skills", Skills()),
            ("languages", Languages()),
            ("hackathons", Hackathons()),
            ("interests", Interests()),
            ("academicactivities", AcademicActivities()),
        };

        foreach (var (scope, roots) in all)
            Plant(db, scope, roots);

        await db.SaveChangesAsync();
    }

    private static void Plant(AppDbContext db, string scope, List<SeedNode> roots, Guid? parentId = null, string? domain = null, int level = 0, string path = "")
    {
        foreach (var node in roots)
        {
            var nodeDomain = level == 0 ? node.Name : domain!;
            var nodePath = string.IsNullOrEmpty(path) ? "/" + Slug(node.Name) : path + "/" + Slug(node.Name);
            var entity = new CategoryNode
            {
                Id = Guid.NewGuid(),
                Scope = scope,
                ParentId = parentId,
                Name = node.Name,
                Domain = nodeDomain,
                Level = level,
                Path = nodePath,
                KeywordsJson = System.Text.Json.JsonSerializer.Serialize(node.Keywords),
                IsSystem = true,
                UserId = null,
            };
            db.CategoryNodes.Add(entity);
            if (node.Children.Count > 0)
                Plant(db, scope, node.Children, entity.Id, nodeDomain, level + 1, nodePath);
        }
    }

    private static string Slug(string s) => s.ToLowerInvariant().Replace("/", " ").Replace(" ", "-").Replace("&", "and");

    // ── Projects ──────────────────────────────────────────────────────────────
    private static List<SeedNode> Projects() => new()
    {
        new SeedNode { Name = "Web", Children = new()
        {
            new SeedNode { Name = "Full Stack", Keywords = new(){"fullstack","full-stack"} },
            new SeedNode { Name = "Frontend", Keywords = new(){"front-end","ui","spa"} },
            new SeedNode { Name = "Backend", Keywords = new(){"back-end","server"} },
            new SeedNode { Name = "API", Keywords = new(){"rest","graphql","web api"} },
            new SeedNode { Name = "Microservices" },
        }},
        new SeedNode { Name = "AI / ML", Children = new()
        {
            new SeedNode { Name = "Machine Learning", Children = new()
            {
                new SeedNode { Name = "Classification" },
                new SeedNode { Name = "Regression" },
                new SeedNode { Name = "Clustering" },
            }},
            new SeedNode { Name = "Deep Learning", Children = new()
            {
                new SeedNode { Name = "Computer Vision", Keywords = new(){"cv","image"}, Children = new()
                {
                    new SeedNode { Name = "Classification" },
                    new SeedNode { Name = "Segmentation" },
                    new SeedNode { Name = "Detection", Keywords = new(){"object detection"} },
                    new SeedNode { Name = "Image Processing" },
                }},
                new SeedNode { Name = "NLP", Keywords = new(){"natural language"} },
            }},
            new SeedNode { Name = "Generative AI", Children = new()
            {
                new SeedNode { Name = "LLM", Keywords = new(){"large language model","gpt"} },
                new SeedNode { Name = "RAG", Keywords = new(){"retrieval augmented generation"} },
                new SeedNode { Name = "AI Agents", Keywords = new(){"agent","agentic"} },
            }},
            new SeedNode { Name = "Data Science" },
        }},
        new SeedNode { Name = "DevOps", Children = new()
        {
            new SeedNode { Name = "Docker", Keywords = new(){"container"} },
            new SeedNode { Name = "CI/CD", Keywords = new(){"cicd","pipeline"} },
            new SeedNode { Name = "Infrastructure", Keywords = new(){"iac","terraform"} },
            new SeedNode { Name = "Monitoring" },
            new SeedNode { Name = "Automation" },
        }},
        new SeedNode { Name = "Data", Children = new()
        {
            new SeedNode { Name = "Databases", Keywords = new(){"sql","nosql"} },
            new SeedNode { Name = "Data Engineering" },
            new SeedNode { Name = "Data Pipelines" },
            new SeedNode { Name = "Data Visualization" },
        }},
        new SeedNode { Name = "Distributed Systems", Children = new()
        {
            new SeedNode { Name = "Microservices" },
            new SeedNode { Name = "Event Driven", Keywords = new(){"event-driven","kafka"} },
            new SeedNode { Name = "Messaging" },
            new SeedNode { Name = "Service Discovery" },
        }},
        new SeedNode { Name = "Academic", Children = new()
        {
            new SeedNode { Name = "Research" },
            new SeedNode { Name = "PFA" },
            new SeedNode { Name = "PFE" },
            new SeedNode { Name = "Coursework" },
        }},
        new SeedNode { Name = "Hackathons", Children = new()
        {
            new SeedNode { Name = "AI" },
            new SeedNode { Name = "Web" },
            new SeedNode { Name = "Data" },
            new SeedNode { Name = "Other" },
        }},
        new SeedNode { Name = "Learning", Children = new()
        {
            new SeedNode { Name = "Tutorials" },
            new SeedNode { Name = "Experiments" },
            new SeedNode { Name = "Proof of Concepts", Keywords = new(){"poc"} },
        }},
    };

    // ── Experiences ───────────────────────────────────────────────────────────
    private static List<SeedNode> Experiences() => new()
    {
        new SeedNode { Name = "Industry", Children = new()
        {
            new SeedNode { Name = "Web" },
            new SeedNode { Name = "AI / ML" },
            new SeedNode { Name = "Data" },
            new SeedNode { Name = "DevOps" },
            new SeedNode { Name = "Distributed Systems" },
            new SeedNode { Name = "Finance" },
            new SeedNode { Name = "Healthcare" },
            new SeedNode { Name = "E-commerce" },
            new SeedNode { Name = "Education" },
        }},
        new SeedNode { Name = "Role", Children = new()
        {
            new SeedNode { Name = "Software Engineer" },
            new SeedNode { Name = "Backend Engineer" },
            new SeedNode { Name = "Frontend Engineer" },
            new SeedNode { Name = "Full Stack Engineer" },
            new SeedNode { Name = "Data Engineer" },
            new SeedNode { Name = "ML Engineer" },
            new SeedNode { Name = "DevOps Engineer" },
            new SeedNode { Name = "Intern" },
        }},
        new SeedNode { Name = "Level", Children = new()
        {
            new SeedNode { Name = "Intern" },
            new SeedNode { Name = "Junior" },
            new SeedNode { Name = "Mid" },
            new SeedNode { Name = "Senior" },
            new SeedNode { Name = "Lead" },
        }},
    };

    // ── Educations ────────────────────────────────────────────────────────────
    private static List<SeedNode> Educations() => new()
    {
        new SeedNode { Name = "Level", Children = new()
        {
            new SeedNode { Name = "Bachelor" },
            new SeedNode { Name = "Master" },
            new SeedNode { Name = "PhD" },
            new SeedNode { Name = "Engineering Degree" },
        }},
        new SeedNode { Name = "Field", Children = new()
        {
            new SeedNode { Name = "Computer Science" },
            new SeedNode { Name = "Software Engineering" },
            new SeedNode { Name = "Data Science" },
            new SeedNode { Name = "Artificial Intelligence" },
            new SeedNode { Name = "Mathematics" },
            new SeedNode { Name = "Networking" },
        }},
    };

    // ── Certifications ────────────────────────────────────────────────────────
    private static List<SeedNode> Certifications() => new()
    {
        new SeedNode { Name = "Vendor", Children = new()
        {
            new SeedNode { Name = "AWS", Keywords = new(){"amazon web services"} },
            new SeedNode { Name = "Microsoft" },
            new SeedNode { Name = "Google" },
            new SeedNode { Name = "Oracle" },
            new SeedNode { Name = "Cisco" },
            new SeedNode { Name = "HashiCorp" },
            new SeedNode { Name = "Other" },
        }},
        new SeedNode { Name = "Domain", Children = new()
        {
            new SeedNode { Name = "Cloud" },
            new SeedNode { Name = "Security" },
            new SeedNode { Name = "Data" },
            new SeedNode { Name = "DevOps" },
            new SeedNode { Name = "Networking" },
        }},
    };

    // ── Skills ────────────────────────────────────────────────────────────────
    private static List<SeedNode> Skills() => new()
    {
        new SeedNode { Name = "Type", Children = new()
        {
            new SeedNode { Name = "Programming Language" },
            new SeedNode { Name = "Framework" },
            new SeedNode { Name = "Library" },
            new SeedNode { Name = "Tool" },
            new SeedNode { Name = "Database" },
            new SeedNode { Name = "Soft Skill" },
            new SeedNode { Name = "Methodology" },
        }},
        new SeedNode { Name = "Technical", Children = new()
        {
            new SeedNode { Name = "Frontend" },
            new SeedNode { Name = "Backend" },
            new SeedNode { Name = "DevOps" },
            new SeedNode { Name = "Data" },
            new SeedNode { Name = "AI / ML" },
        }},
    };

    // ── Languages ─────────────────────────────────────────────────────────────
    private static List<SeedNode> Languages() => new()
    {
        new SeedNode { Name = "Proficiency", Children = new()
        {
            new SeedNode { Name = "Native" },
            new SeedNode { Name = "Fluent" },
            new SeedNode { Name = "Intermediate" },
            new SeedNode { Name = "Beginner" },
        }},
    };

    // ── Hackathons ────────────────────────────────────────────────────────────
    private static List<SeedNode> Hackathons() => new()
    {
        new SeedNode { Name = "Theme", Children = new()
        {
            new SeedNode { Name = "AI" },
            new SeedNode { Name = "Web" },
            new SeedNode { Name = "Data" },
            new SeedNode { Name = "Other" },
        }},
    };

    // ── Interests ─────────────────────────────────────────────────────────────
    private static List<SeedNode> Interests() => new()
    {
        new SeedNode { Name = "Theme", Children = new()
        {
            new SeedNode { Name = "Technology" },
            new SeedNode { Name = "Sports" },
            new SeedNode { Name = "Arts" },
            new SeedNode { Name = "Science" },
            new SeedNode { Name = "Other" },
        }},
    };

    // ── Academic Activities ───────────────────────────────────────────────────
    private static List<SeedNode> AcademicActivities() => new()
    {
        new SeedNode { Name = "Type", Children = new()
        {
            new SeedNode { Name = "Research" },
            new SeedNode { Name = "PFA" },
            new SeedNode { Name = "PFE" },
            new SeedNode { Name = "Coursework" },
        }},
    };
}
