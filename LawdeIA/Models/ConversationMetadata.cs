using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawdeIA.Models
{
    [Table("Conversation_Metadata")]
    public class ConversationMetadata
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("MetadataID")]
        public int MetadataID { get; set; }

        [Required]
        [Column("ConversationID")]
        public int ConversationID { get; set; }

        [StringLength(50)]
        [Column("ModelUsed")]
        public string? ModelUsed { get; set; }

        [Column("Parameters", TypeName = "nvarchar(MAX)")]
        public string? Parameters { get; set; } // JSON string for configurations

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("ConversationID")]
        public virtual Conversation Conversation { get; set; } = null!;
    }
}