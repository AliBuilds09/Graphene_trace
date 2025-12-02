using System;
using System.IO;
using Xunit;
using MyProject.Controllers;
using MyProject.Models;

namespace MyProject.Tests;

public class CsvImportControllerTests
{
    [Fact]
    public void LoadFramesFromFiles_Parses_32x32_Matrix()
    {
        // Ensure non-admin session to pass clinical access check
        Session.CurrentUser = new User { Username = "tester", Role = "Clinician", IsActive = true };

        var tmp = Path.GetTempFileName();
        var csv = string.Join(',', System.Linq.Enumerable.Repeat("5", 1024));
        File.WriteAllText(tmp + ".csv", csv);
        var path = tmp + ".csv";

        var frames = CsvImportController.LoadFramesFromFiles(new[] { path });
        Assert.Single(frames);
        var f = frames[0];
        Assert.Equal(32, f.Matrix.GetLength(0));
        Assert.Equal(32, f.Matrix.GetLength(1));
        Assert.Equal(5, f.Matrix[0,0]);

        // cleanup
        File.Delete(path);
    }
}