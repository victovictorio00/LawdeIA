using Microsoft.EntityFrameworkCore;
using LawdeIA.Models;

namespace LawdeIA.Data
{
    public class LawdeIAContext : DbContext
    {
        public LawdeIAContext(DbContextOptions<LawdeIAContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserSessions> UserSessions { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<ConversationMetadata> ConversationMetadata { get; set; }
        public DbSet<RAGDocument> RAGDocuments { get; set; }
        public DbSet<RAGEmbedding> RAGEmbeddings { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurar relaciones
            modelBuilder.Entity<User>()
                .HasMany(u => u.Sessions)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Conversations)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.RAGDocuments)
                .WithOne(d => d.User)
                .HasForeignKey(d => d.UserID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<User>()
                .HasMany(u => u.AuditLogs)
                .WithOne(a => a.User)
                .HasForeignKey(a => a.UserID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Conversation>()
                .HasMany(c => c.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.Metadata)
                .WithOne(m => m.Conversation)
                .HasForeignKey<ConversationMetadata>(m => m.ConversationID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.ParentMessage)
                .WithMany(m => m.Replies)
                .HasForeignKey(m => m.ParentMessageID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RAGDocument>()
                .HasMany(d => d.Embeddings)
                .WithOne(e => e.Document)
                .HasForeignKey(e => e.DocumentID)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar índices
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<UserSessions>()
                .HasIndex(s => s.UserID);

            modelBuilder.Entity<Conversation>()
                .HasIndex(c => c.UserID);

            modelBuilder.Entity<Message>()
                .HasIndex(m => m.ConversationID);
        }
    }
}