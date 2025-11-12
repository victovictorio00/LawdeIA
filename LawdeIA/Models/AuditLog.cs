using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawdeIA.Models
{
    [Table("Audit_Log")]
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("LogID")]
        public int LogID { get; set; }

        [Column("UserID")]
        public int? UserID { get; set; }

        [Required]
        [StringLength(100)]
        [Column("Action")]
        public string Action { get; set; } = string.Empty;

        [Column("Details", TypeName = "nvarchar(MAX)")]
        public string? Details { get; set; } // JSON string for details

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        [Column("IPAddress")]
        public string IPAddress { get; set; } = string.Empty;

        // Navigation property
        [ForeignKey("UserID")]
        public virtual User? User { get; set; }
    }
}