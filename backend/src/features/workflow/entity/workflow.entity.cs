using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.src.features.workflow.entity
{
    [Table("workflows")]
    public class Workflow
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// JSON blob defining the sequence of nodes.
        /// Example: [{"type": "extractor"}, {"type": "search"}]
        /// </summary>
        [Required]
        [Column(TypeName = "jsonb")]
        public string DefinitionJson { get; set; } = "[]";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
