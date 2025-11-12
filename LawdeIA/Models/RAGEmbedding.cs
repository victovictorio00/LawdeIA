using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawdeIA.Models
{
    [Table("RAG_Embeddings")]
    public class RAGEmbedding
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("EmbeddingID")]
        public int EmbeddingID { get; set; }

        [Required]
        [Column("DocumentID")]
        public int DocumentID { get; set; }

        [Column("Vector")]
        public byte[]? Vector { get; set; } // Binary data for embeddings

        [Required]
        [Column("ChunkIndex")]
        public int ChunkIndex { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(50)]
        [Column("ModelUsed")]
        public string? ModelUsed { get; set; }

        // Navigation property
        [ForeignKey("DocumentID")]
        public virtual RAGDocument Document { get; set; } = null!;
    }
}