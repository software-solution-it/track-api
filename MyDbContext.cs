using Microsoft.EntityFrameworkCore;
using track_api.Models;

public class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Order { get; set; }

    public DbSet<WpPost> WpPost { get; set; }

    public DbSet<WpPostMeta> WpPostMeta { get; set; }

    public DbSet<ValidationPost> ValidationPost { get; set; }

    public DbSet<ValidationGetDto> ValidationGetDto { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().HasNoKey();
        modelBuilder.Entity<ValidationPost>().HasNoKey();
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ValidationPost>()
        .HasKey(vp => vp.Id);

        modelBuilder.Entity<ValidationPost>()
            .Property(vp => vp.Id)
            .ValueGeneratedOnAdd();
    }
}
