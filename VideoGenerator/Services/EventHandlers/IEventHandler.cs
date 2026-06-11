using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.EventHandlers
{
    public interface IEventHandler
    {
        bool CanHandle(string folderName);
        Task<ParsedEvent> HandleAsync(string folderName, string language);
    }
}
