using NotificationService.Hubs;
using NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Clinic Queue Notification Service - Đề tài 09",
        Version = "v1",
        Description = "SignalR Realtime Hub & RabbitMQ Consumer - Cập nhật tiến độ hàng đợi phòng khám theo thời gian thực"
    });
});

// Configure SignalR
builder.Services.AddSignalR();

// Configure CORS for Web Client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Background RabbitMQ consumer
builder.Services.AddHostedService<RabbitMqNotificationConsumer>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowClient");
app.UseAuthorization();

app.MapControllers();
app.MapHub<QueueNotificationHub>("/hubs/queue-notification");

app.MapGet("/", () => Results.Ok(new
{
    service = "Clinic Queue Notification Service",
    status = "Running",
    hubUrl = "/hubs/queue-notification",
    swaggerUrl = "/swagger"
}));

app.Run();
