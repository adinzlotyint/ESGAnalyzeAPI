using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ESGAnalyzeAPI.Services {
    public interface IParseService {
        Task<string> ExtractTextFromPDFAsync(IFormFile file);
    }
    public class ParseService : IParseService {
        public async Task<string> ExtractTextFromPDFAsync(IFormFile file) {
            using var stream = file.OpenReadStream();
            var pdfBytes = await Task.Run(() => {
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            });

            using var pdf = PdfDocument.Open(pdfBytes);

            var builder = new StringBuilder();
            foreach (Page page in pdf.GetPages()) {
                builder.AppendLine(page.Text);
            }

            return builder.ToString();
        }
    }
}
