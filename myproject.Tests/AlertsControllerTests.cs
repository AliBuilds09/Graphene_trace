using Xunit;
using MyProject.Controllers;

namespace MyProject.Tests;

public class AlertsControllerTests
{
    [Fact]
    public void AddAlert_Adds_Alert_With_Expected_Message()
    {
        AlertsController.Clear();
        AlertsController.AddAlert(255, 10, "UnitTest");
        Assert.Single(AlertsController.Alerts);
        var a = AlertsController.Alerts[0];
        Assert.Equal(255, a.PeakPressure);
        Assert.Equal(10, a.Threshold);
        Assert.Equal("UnitTest", a.Source);
        Assert.Equal("High Peak Pressure 255 (> 10)", a.Message);
    }
}