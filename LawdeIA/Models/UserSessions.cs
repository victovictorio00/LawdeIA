using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawdeIA.Models
{
    [Table("User_Sessions")]
    public class UserSessions
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("SessionID")]
        public int SessionID { get; set; }

        [Required]
        [Column("UserID")]
        public int UserID { get; set; }

        [Required]
        [StringLength(256)]
        [Column("Token")]
        public string Token { get; set; } = string.Empty;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        [Column("ExpiresAt")]
        public DateTime ExpiresAt { get; set; }

        [Column("IsValid")]
        public bool IsValid { get; set; } = true;

        // Navigation property
        [ForeignKey("UserID")]
        public virtual User User { get; set; } = null!;
    }
}