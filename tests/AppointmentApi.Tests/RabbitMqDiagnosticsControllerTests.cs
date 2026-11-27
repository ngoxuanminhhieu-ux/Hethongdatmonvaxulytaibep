using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using AppointmentApi.Controllers;
using AppointmentApi.Services;
using Xunit;

namespace AppointmentApi.Tests;

public class RabbitMqDiagnosticsControllerTests
{
    private readonly Mock<IRabbitMqPersistentConnection> _mockConnection;
    private readonly Mock<IRabbitMqProducer> _mockProducer;
    private readonly Mock<ILogger<RabbitMqDiagnosticsController>> _mockLogger;
    private readonly RabbitMqDiagnosticsController _controller;

    public RabbitMqDiagnosticsControllerTests()
    {
        _mockConnection = new Mock<IRabbitMqPersistentConnection>();
        _mockProducer = new Mock<IRabbitMqProducer>();
        _mockLogger = new Mock<ILogger<RabbitMqDiagnosticsController>>();

        _mockConnection.Setup(c => c.HostName).Returns("localhost");
        _mockConnection.Setup(c => c.Port).Returns(5672);
        _mockConnection.Setup(c => c.VirtualHost).Returns("/");

        _controller = new RabbitMqDiagnosticsController(
            _mockConnection.Object,
            _mockProducer.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void GetHealth_WhenConnected_ReturnsHealthy()
    {
        // Arrange
        _mockConnection.Setup(c => c.IsConnected).Returns(true);

        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public void GetHealth_WhenDisconnected_ReturnsUnhealthy()
    {
        // Arrange
        _mockConnection.Setup(c => c.IsConnected).Returns(false);
        _mockConnection.Setup(c => c.TryConnect()).Returns(false);

        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("Unhealthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public void GetTopology_ReturnsCompleteTopologyConfiguration()
    {
        // Act
        var result = _controller.GetTopology();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void PublishTestMessage_WhenSuccessful_ReturnsOk()
    {
        // Arrange
        _mockProducer.Setup(p => p.PublishTestMessage(It.IsAny<string>(), It.IsAny<object>()))
                     .Returns(true);

        var req = new TestPublishRequest { RoutingKey = "appointment.requested" };

        // Act
        var result = _controller.PublishTestMessage(req);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void PublishTestMessage_WhenBrokerDown_Returns503ServiceUnavailable()
    {
        // Arrange
        _mockProducer.Setup(p => p.PublishTestMessage(It.IsAny<string>(), It.IsAny<object>()))
                     .Returns(false);

        var req = new TestPublishRequest { RoutingKey = "appointment.requested" };

        // Act
        var result = _controller.PublishTestMessage(req);

        // Assert
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objResult.StatusCode);
    }
}
