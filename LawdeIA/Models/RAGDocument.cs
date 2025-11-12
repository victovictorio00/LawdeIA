using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawdeIA.Models
{
    [Table("RAG_Documents")]
    public class RAGDocument
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("DocumentID")]
        public int DocumentID { get; set; }

        [Column("UserID")]
        public int? UserID { get; set; }

        [Required]
        [StringLength(100)]
        [Column("Title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Column("Content", TypeName = "nvarchar(MAX)")]
        public string Content { get; set; } = string.Empty;

        [StringLength(200)]
        [Column("Source")]
        public string? Source { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("LastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [StringLength(20)]
        [Column("AccessLevel")]
        public string AccessLevel { get; set; } = "Private";

        // Navigation properties
        [ForeignKey("UserID")]
        public virtual User? User { get; set; }
        public virtual ICollection<RAGEmbedding> Embeddings { get; set; } = new List<RAGEmbedding>();
    }
}