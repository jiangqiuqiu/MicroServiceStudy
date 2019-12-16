using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using User.API.Models;

namespace User.API.Data
{
    public class UserContext:DbContext
    {
        public UserContext(DbContextOptions<UserContext> options):base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppUser>()
                .ToTable("Users")
                .HasKey(u=>u.Id);

            modelBuilder.Entity<UserProperty>()
                .Property(u => u.Key).HasMaxLength(100);//设置Key字段最长100
            modelBuilder.Entity<UserProperty>()
                .Property(u => u.Value).HasMaxLength(100);//设置Value字段最长100
            modelBuilder.Entity<UserProperty>()
                .ToTable("UserProperties")
                .HasKey(u => new { u.Key,u.AppUserId,u.Value});//组合主键
            


            modelBuilder.Entity<UserTag>()
               .Property(u => u.Tag).HasMaxLength(100);//设置Tag字段最长100
            modelBuilder.Entity<UserTag>()
                .ToTable("UserTags")
                .HasKey(u => new { u.UserId, u.Tag });//组合主键
           

            modelBuilder.Entity<BPFile>()
                .ToTable("UserBPFiles")
                .HasKey(b => b.Id);

            base.OnModelCreating(modelBuilder);

        }

        public DbSet<AppUser> Users { get; set; }
        public DbSet<UserProperty> UserProperties { get; set; }
    }
}
