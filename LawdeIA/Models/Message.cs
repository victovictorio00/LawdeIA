using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawdeIA.Models
{
    [Table("Messages")]
    public class Message
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("MessageID")]
        public int MessageID { get; set; }

        [Required]
        [Column("ConversationID")]
        public int ConversationID { get; set; }

        [Required]
        [StringLength(20)]
        [Column("SenderType")]
        public string SenderType { get; set; } = string.Empty; // "User" or "AI"

        [Required]
        [Column("Content", TypeName = "nvarchar(MAX)")]
        public string Content { get; set; } = string.Empty;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("IsEdited")]
        public bool IsEdited { get; set; } = false;

        [Column("ParentMessageID")]
        public int? ParentMessageID { get; set; }

        // Navigation properties
        [ForeignKey("ConversationID")]
        public virtual Conversation Conversation { get; set; } = null!;

        [ForeignKey("ParentMessageID")]
        public virtual Message? ParentMessage { get; set; }

        public virtual ICollection<Message> Replies { get; set; } = new List<Message>();
    }
}