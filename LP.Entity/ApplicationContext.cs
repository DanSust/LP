namespace LP.Entity
{
    using Microsoft.EntityFrameworkCore;
    using System.Reflection.Emit;

    public class ApplicationContext : DbContext
    {
        public DbSet<Interest> Interests { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<EmailConfirmation> EmailConfirmations { get; set; }
        public DbSet<UserInterest> UserInterests { get; set; } = null!;
        public DbSet<UserQuestion> UserQuestions { get; set; } = null!;
        public DbSet<Profile> Profiles { get; set; } = null!;
        public DbSet<Photo> Photos { get; set; } = null!;
        public DbSet<PhotoMain> PhotoMain { get; set; } = null!;
        public DbSet<City> Cities { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<Chat> Chats { get; set; } = null!;
        public DbSet<Vote> Votes { get; set; } = null!;
        public DbSet<Event> Events { get; set; } = null!;
        public DbSet<Reject> Rejects { get; set; } = null!;
        public ApplicationContext(DbContextOptions<ApplicationContext> options)
            : base(options)
        {
            Database.EnsureCreated();   // создаем базу данных при первом обращении
        }

        protected override void OnModelCreating(ModelBuilder md)
        {
            //base.OnModelCreating(md);
            md.Entity<User>(ent =>
            {
                ent.Property(o => o.Id).HasColumnOrder(0);
                ent.Property(o => o.Caption).HasColumnOrder(1);
                ent.HasOne<Profile>();
                ent.HasMany<Connectity>();
                ent.HasIndex(u => u.Email).IsUnique();
            });

            md.Entity<EmailConfirmation>()
                .HasOne(ec => ec.User)
                .WithOne(u => u.EmailConfirmation)
                .HasForeignKey<EmailConfirmation>(ec => ec.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            md.Entity<City>()
                .HasIndex(c => c.Name)           // ✅ Basic index
                .IsUnique()                      // ✅ Unique constraint (optional)
                .HasDatabaseName("IX_City_Name"); // ✅ Custom name (optional)

            md.Entity<Vote>(builder =>
            {
                builder.HasIndex(v => new {v.Owner, v.Like});
                builder.HasIndex(v => new { v.Like, v.Owner });
                builder.HasIndex(v => new { v.Like, v.IsLike });
                builder.HasIndex(v => new { v.Like, v.IsViewed });
                builder.HasIndex(v => new { v.Like, v.IsLike, v.IsViewed });
            });

            md.Entity<Message>(builder =>
            {
                builder.HasIndex(v => new {v.ChatId, v.Time }).IsDescending(false, false);
            });

            md.Entity<Reject>(entity =>
            {
                entity.HasOne<User>()  // без указания навигационного свойства
                    .WithMany()
                    .HasForeignKey(r => r.Owner)
                    .OnDelete(DeleteBehavior.Cascade);
                // Индексы для производительности
                entity.HasIndex(r => r.Owner)
                    .HasDatabaseName("IX_Rejects_Owner");

                entity.HasIndex(r => r.UserId)
                    .HasDatabaseName("IX_Rejects_UserId");

                // Составной индекс, если часто ищете по обоим полям
                entity.HasIndex(r => new { r.Owner, r.UserId })
                    .HasDatabaseName("IX_Rejects_Owner_UserId");
            });

            //md.Entity<PhotoMain>().ToTable("PhotoMains").HasKey(x=>x.PhotoId);
            //md.Entity<Photo>().ToTable("Photos").HasOne<PhotoMain>();

            //_ = Users.FirstOrDefault();
        }
    }
}
