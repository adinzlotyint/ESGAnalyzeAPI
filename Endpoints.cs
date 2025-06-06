using ESGAnalyzeAPI.Models;
using ESGAnalyzeAPI.Services;
using Microsoft.AspNetCore.Mvc;

public static class Endpoints {
    public static void MapEndpoints(this IEndpointRouteBuilder app) {
        app.MapPost("/analyze/pdf", async (IFormFile file, [FromServices] IParseService parseService, [FromServices] IAnalyzer analyzer) => {
            if (file == null || Path.GetExtension(file.FileName)?.ToLower() != ".pdf")
                return Results.BadRequest("Only .pdf files are supported.");

            string text = await parseService.ExtractTextFromPDFAsync(file);
            ESGAnalysisResult result = analyzer.Analyze(text);
            return Results.Ok(result);
        }).DisableAntiforgery(); ;
    }
}
