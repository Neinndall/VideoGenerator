using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.Parsers
{
    public interface IEventParser
    {
        bool CanParse(string folderName);
        Task<ParsedEvent> ParseAsync(string folderName, string language);
    }
}
