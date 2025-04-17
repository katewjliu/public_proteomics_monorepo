using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Linq;
using RawFileReaderLib; // Reference your library

using ThermoFisher.CommonCore.Data;
using ThermoFisher.CommonCore.Data.Business;
using ThermoFisher.CommonCore.Data.FilterEnums;
using ThermoFisher.CommonCore.Data.Interfaces;
using ThermoFisher.CommonCore.MassPrecisionEstimator;
using ThermoFisher.CommonCore.RawFileReader;
using System.Diagnostics.CodeAnalysis;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// (Optional) Enable serving static files from wwwroot if needed
app.UseStaticFiles();

// Root endpoint: Return index.html from the current directory
app.MapGet("/", async context =>
{
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "index.html");
    if (File.Exists(filePath))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(filePath);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("html not found");
    }
});

// File serving endpoint: Serve the requested file if it exists
app.MapGet("/files/{filename}", async (HttpContext context, string filename) =>
{
    Console.WriteLine("Requested file: " + filename);
    // Combine the current directory with the requested filename
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), filename);
    if (File.Exists(filePath))
    {
        await context.Response.SendFileAsync(filePath);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("File not found");
    }
});

// API endpoint: List all .raw files in the current directory as JSON
app.MapGet("/api/files", () =>
{
    var currentDir = Directory.GetCurrentDirectory();
    // Get only the file names (not the full paths)
    var files = Directory.GetFiles(currentDir, "*.raw")
                         .Select(Path.GetFileName)
                         .ToList();
    Console.WriteLine("Detected .raw files: " + string.Join(", ", files));
    return Results.Json(files);
});

// API endpoint to get scan range for a given RAW file
app.MapGet("/api/scanrange", (string filePath) =>
{
    Console.WriteLine("scan range file: " + filePath);
    try
    {
        // Validate the file path (for example, check if it exists in a specific folder)
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Results.BadRequest("A valid RAW file path must be provided.");
        }

        var readerService = new RawFileReaderService();
        var scanRange = readerService.GetScanRange(filePath);
        return Results.Json(scanRange);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// API Endpoint to get Scan Details for a user selected scan number
app.MapGet("/api/scandetails", (string filePath, int scanNumber) =>
{
    Console.WriteLine($"Loading scan details for file: {filePath}, ScanNumber: {scanNumber}");
    try
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Results.BadRequest("A valid RAW file path must be provided.");
        }

        var readerService = new RawFileReaderService();
        readerService.OpenRawFile(filePath);
        var scanDetails = readerService.GetScanDetails(scanNumber);
        return Results.Json(new { ScanDetails = scanDetails });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});


// API Endpoint to Get Spectrum Data
app.MapGet("/api/spectrum", (string filePath, int scanNumber) =>
{
    Console.WriteLine($"Loading spectrum for file: {filePath}, ScanNumber: {scanNumber}");
    try
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Results.BadRequest("A valid RAW file path must be provided.");
        }
        
        var readerService = new RawFileReaderService();
        readerService.OpenRawFile(filePath);
        var spectrumData = readerService.GetSpectrum(scanNumber);
        return Results.Json(spectrumData);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// Run the application on all network interfaces at port 8000
app.Run("http://0.0.0.0:8000");
