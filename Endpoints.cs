using ESGAnalyzeAPI.Models;
using ESGAnalyzeAPI.Services;
using Microsoft.AspNetCore.Mvc;

public static class Endpoints {
    public static void MapEndpoints(this IEndpointRouteBuilder app) {
        app.MapPost("/analyze/pdf", async (IFormFile file, [FromServices] IParseService parseService,
                    [FromServices] IAnalyzerService analyzer, [FromServices] ILogger<Program> logger) => {
                        if (file == null || Path.GetExtension(file.FileName)?.ToLower() != ".pdf") {
                            logger.LogWarning("Invalid file uploaded: {FileName}", file?.FileName);
                            return Results.BadRequest("Only .pdf files are supported.");
                        }

                        string text = await parseService.ExtractTextFromPDFAsync(file);
                        ESGAnalysisResult result = analyzer.Analyze(text);
                        return Results.Ok(result);
                    }).DisableAntiforgery();

    }
}
