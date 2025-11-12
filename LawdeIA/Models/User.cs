using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawdeIA.Models
{
    [Table("Users")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("UserID")]
        public int UserID { get; set; }

        [Required]
        [StringLength(50)]
        [Column("Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [EmailAddress]
        [Column("Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        [Column("PasswordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [StringLength(100)]
        [Column("FullName")]
        public string? FullName { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("LastLogin")]
        public DateTime? LastLogin { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [StringLength(20)]
        [Column("Role")]
        public string Role { get; set; } = "User";

        // Navigation properties
        public virtual ICollection<UserSessions> Sessions { get; set; } = new List<UserSessions>();
        public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
        public virtual ICollection<RAGDocument> RAGDocuments { get; set; } = new List<RAGDocument>();
        public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}