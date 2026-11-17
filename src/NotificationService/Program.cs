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
        Title = "Notification Realtime Service",
        Version = "v1",
        Description = "SignalR Realtime Hub & RabbitMQ Consumer - Đề tài 04 môn PTUDPT"
    });
});

// Configure SignalR
builder.Services.AddSignalR();

// Configure CORS for Web Client SignalR connections
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
app.MapHub<OrderNotificationHub>("/hubs/order-notification");

app.MapGet("/", () => Results.Ok(new
{
    service = "Notification Service",
    status = "Running",
    hubUrl = "/hubs/order-notification",
    swaggerUrl = "/swagger"
}));

app.Run();
