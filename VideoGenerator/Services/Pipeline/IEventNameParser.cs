using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services;

public interface IEventNameParser
{
    Task<ParsedEvent> ParseFolderNameAsync(string folderName, string language);
}
