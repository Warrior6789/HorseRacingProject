using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Models;

public partial class HorseRacingDataContext : DbContext
{
    public HorseRacingDataContext()
    {
    }

    public HorseRacingDataContext(DbContextOptions<HorseRacingDataContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<Bet> Bets { get; set; }

    public virtual DbSet<ConversionRate> ConversionRates { get; set; }

    public virtual DbSet<Horse> Horses { get; set; }

    public virtual DbSet<JockeyProfile> JockeyProfiles { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Prize> Prizes { get; set; }

    public virtual DbSet<Race> Races { get; set; }

    public virtual DbSet<RaceResult> RaceResults { get; set; }

    public virtual DbSet<Racecourse> Racecourses { get; set; }

    public virtual DbSet<RefereeReport> RefereeReports { get; set; }

    public virtual DbSet<Registration> Registrations { get; set; }

    public virtual DbSet<Tournament> Tournaments { get; set; }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Server=ep-young-poetry-aoaklwv8.c-2.ap-southeast-1.aws.neon.tech;Database=neondb;User Id=neondb_owner;Password=npg_7CBlyeraZN6q;SSL Mode=Require;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Account_pkey");

            entity.ToTable("Account");

            entity.HasIndex(e => e.Email, "Account_Email_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ID");
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasConversion<string>();
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasConversion<string>();
            entity.Property(e => e.CreateAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Role).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Bet>(entity =>
        {
            entity.HasKey(e => e.BetId).HasName("Bets_pkey");

            entity.Property(e => e.BetId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("BetID");
            entity.Property(e => e.BetAmount).HasPrecision(18, 2);
            entity.Property(e => e.BetType).HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.RegistrationId).HasColumnName("RegistrationID");
            entity.Property(e => e.SpectatorId).HasColumnName("SpectatorID");
            entity.Property(e => e.Status).HasMaxLength(20);

            entity.HasOne(d => d.Registration).WithMany(p => p.Bets)
                .HasForeignKey(d => d.RegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bets_Registrations");

            entity.HasOne(d => d.Spectator).WithMany(p => p.Bets)
                .HasForeignKey(d => d.SpectatorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bets_Account");
        });

        modelBuilder.Entity<ConversionRate>(entity =>
        {
            entity.HasKey(e => e.ConversionRateId).HasName("ConversionRate_pkey");

            entity.ToTable("ConversionRate");

            entity.Property(e => e.ConversionRateId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ConversionRateID");
            entity.Property(e => e.ConversionRate1).HasColumnName("ConversionRate");
            entity.Property(e => e.Status).HasMaxLength(20);
        });

        modelBuilder.Entity<Horse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Horses_pkey");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ID");
            entity.Property(e => e.Breed).HasMaxLength(50);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.CreateAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.HorseName).HasMaxLength(100);
            entity.Property(e => e.OwnerId).HasColumnName("OwnerID");
            entity.Property(e => e.RecordWins).HasDefaultValue(0);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Owner).WithMany(p => p.Horses)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Horses_Account");
        });

        modelBuilder.Entity<JockeyProfile>(entity =>
        {
            entity.HasKey(e => e.JockeyProfileId).HasName("JockeyProfile_pkey");

            entity.ToTable("JockeyProfile");

            entity.Property(e => e.JockeyProfileId)
                .ValueGeneratedNever()
                .HasColumnName("JockeyProfileID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.CreateAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.JockeyRating).HasPrecision(3, 2);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Account).WithMany(p => p.JockeyProfiles)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_JockeyProfile_Account");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("Payment_pkey");

            entity.ToTable("Payment");

            entity.Property(e => e.PaymentId)
                .ValueGeneratedNever()
                .HasColumnName("PaymentID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.ConversionRateId).HasColumnName("ConversionRateID");
            entity.Property(e => e.CreateAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasMaxLength(20);

            entity.HasOne(d => d.ConversionRate).WithMany(p => p.Payments)
                .HasForeignKey(d => d.ConversionRateId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payment_ConversionRate");
        });

        modelBuilder.Entity<Prize>(entity =>
        {
            entity.HasKey(e => e.PrizeId).HasName("Prizes_pkey");

            entity.Property(e => e.PrizeId)
                .ValueGeneratedNever()
                .HasColumnName("PrizeID");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.PrizeType).HasMaxLength(50);
            entity.Property(e => e.RegistrationId).HasColumnName("RegistrationID");

            entity.HasOne(d => d.Registration).WithMany(p => p.Prizes)
                .HasForeignKey(d => d.RegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Prizes_Registrations");
        });

        modelBuilder.Entity<Race>(entity =>
        {
            entity.HasKey(e => e.RaceId).HasName("Races_pkey");

            entity.Property(e => e.RaceId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("RaceID");
            entity.Property(e => e.CreateAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.RacecourseId).HasColumnName("RacecourseID");
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");

            entity.HasOne(d => d.Racecourse).WithMany(p => p.Races)
                .HasForeignKey(d => d.RacecourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Races_Racecourses");

            entity.HasOne(d => d.Tournament).WithMany(p => p.Races)
                .HasForeignKey(d => d.TournamentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Races_Tournaments");
        });

        modelBuilder.Entity<RaceResult>(entity =>
        {
            entity.HasKey(e => e.ResultId).HasName("RaceResults_pkey");

            entity.Property(e => e.ResultId)
                .ValueGeneratedNever()
                .HasColumnName("ResultID");
            entity.Property(e => e.CreateAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsDisqualified).HasDefaultValue(false);
            entity.Property(e => e.RegistrationId).HasColumnName("RegistrationID");

            entity.HasOne(d => d.Registration).WithMany(p => p.RaceResults)
                .HasForeignKey(d => d.RegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RaceResults_Registrations");
        });

        modelBuilder.Entity<Racecourse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Racecourses_pkey");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("ID");
            entity.Property(e => e.Location).HasMaxLength(255);
            entity.Property(e => e.RacecourseName).HasMaxLength(150);
            entity.Property(e => e.TrackType).HasMaxLength(50);
        });

        modelBuilder.Entity<RefereeReport>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("RefereeReports_pkey");

            entity.Property(e => e.ReportId)
                .ValueGeneratedNever()
                .HasColumnName("ReportID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.PenaltyApplied).HasMaxLength(255);
            entity.Property(e => e.RaceId).HasColumnName("RaceID");
            entity.Property(e => e.RefereeId).HasColumnName("RefereeID");

            entity.HasOne(d => d.Race).WithMany(p => p.RefereeReports)
                .HasForeignKey(d => d.RaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RefereeReports_Races");

            entity.HasOne(d => d.Referee).WithMany(p => p.RefereeReports)
                .HasForeignKey(d => d.RefereeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RefereeReports_Account");
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(e => e.RegistrationId).HasName("Registrations_pkey");

            entity.Property(e => e.RegistrationId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("RegistrationID");
            entity.Property(e => e.CreateAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.HorseId).HasColumnName("HorseID");
            entity.Property(e => e.JockeyConfirmation).HasDefaultValue(false);
            entity.Property(e => e.JockeyId).HasColumnName("JockeyID");
            entity.Property(e => e.OwnerConfirmation).HasDefaultValue(false);
            entity.Property(e => e.RaceId).HasColumnName("RaceID");
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Horse).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.HorseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Registrations_Horses");

            entity.HasOne(d => d.Jockey).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.JockeyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Registrations_Account");

            entity.HasOne(d => d.Race).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.RaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Registrations_Races");
        });

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Tournaments_pkey");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("ID");
            entity.Property(e => e.CreateAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.TournamentName).HasMaxLength(150);
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.ProfileId).HasName("UserProfiles_pkey");

            entity.Property(e => e.ProfileId)
                .ValueGeneratedNever()
                .HasColumnName("ProfileID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.Balance).HasDefaultValue(0L);
            entity.Property(e => e.CreateAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.Phone).HasMaxLength(15);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Account).WithMany(p => p.UserProfiles)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserProfiles_Account");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
