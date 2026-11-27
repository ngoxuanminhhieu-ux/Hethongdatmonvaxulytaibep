using Microsoft.EntityFrameworkCore;
using AppointmentApi.Data;
using AppointmentApi.Middleware;
using AppointmentApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Clinic Appointment & Queue API - Đề tài 09",
        Version = "v1",
        Description = "Hệ thống Đặt lịch khám và Quản lý hàng đợi phòng khám phân tán (RabbitMQ + SignalR)"
    });
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// RabbitMQ Persistent Connection & Hosted Topology Initializer
builder.Services.AddSingleton<IRabbitMqPersistentConnection, RabbitMqPersistentConnection>();
builder.Services.AddHostedService<RabbitMqTopologyInitializer>();
builder.Services.AddSingleton<IRabbitMqProducer, RabbitMqProducer>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Clinic Appointment API v1");
        c.RoutePrefix = string.Empty; // Swagger tại root http://localhost:5001
    });
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Khởi tạo Database và Seed Data tự động khi khởi động
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbInitializer.InitializeAsync(context, logger);
}

app.Run();
