using Microsoft.EntityFrameworkCore;
using OrderApi.Data;
using OrderApi.Middleware;
using OrderApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Food Order API - Đề tài 04",
        Version = "v1",
        Description = "API Đặt món, Quản lý thực đơn và Tích hợp RabbitMQ chuyên sâu (Tuần 4: Message Durability, Resilient Connection, Publisher Confirms)"
    });
});

// Configure CORS for Web Client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Database (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// Register RabbitMQ Persistent Connection (Singleton)
builder.Services.AddSingleton<IRabbitMqPersistentConnection, RabbitMqPersistentConnection>();

// Register RabbitMQ Topology Auto-Initializer (Hosted Service)
builder.Services.AddHostedService<RabbitMqTopologyInitializer>();

// Register RabbitMQ Producer (Singleton)
builder.Services.AddSingleton<IRabbitMqProducer, RabbitMqProducer>();

var app = builder.Build();

// Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Food Order API v1");
        c.RoutePrefix = string.Empty; // Mở Swagger ngay tại root URL: http://localhost:5001
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
