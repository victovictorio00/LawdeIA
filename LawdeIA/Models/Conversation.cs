using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawdeIA.Models
{
    [Table("Conversations")]
    public class Conversation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ConversationID")]
        public int ConversationID { get; set; }

        [Required]
        [Column("UserID")]
        public int UserID { get; set; }

        [StringLength(100)]
        [Column("Title")]
        public string? Title { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("LastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [StringLength(20)]
        [Column("Status")]
        public string Status { get; set; } = "Active";

        // Navigation properties
        [ForeignKey("UserID")]
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual ConversationMetadata? Metadata { get; set; }
    }
}