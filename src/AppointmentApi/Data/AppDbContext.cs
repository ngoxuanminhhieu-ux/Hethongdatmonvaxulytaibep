using Microsoft.EntityFrameworkCore;
using AppointmentApi.Data.Entities;

namespace AppointmentApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<QueueLog> QueueLogs => Set<QueueLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TimeSlot>()
            .HasOne(t => t.Doctor)
            .WithMany(d => d.TimeSlots)
            .HasForeignKey(t => t.DoctorId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Patient)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Doctor)
            .WithMany(d => d.Appointments)
            .HasForeignKey(a => a.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.TimeSlot)
            .WithMany(t => t.Appointments)
            .HasForeignKey(a => a.TimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<QueueLog>()
            .HasOne(q => q.Doctor)
            .WithMany()
            .HasForeignKey(q => q.DoctorId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<QueueLog>()
            .HasOne(q => q.Appointment)
            .WithMany(a => a.QueueLogs)
            .HasForeignKey(q => q.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
