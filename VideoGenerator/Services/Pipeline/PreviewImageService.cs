using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoGenerator.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Produces frozen WPF preview images from parsed events without mutating UI state.
    /// </summary>
    public sealed class PreviewImageService
    {
        private readonly ImageGenerator _imageGenerator;
        private readonly AppSettings _settings;

        public PreviewImageService(ImageGenerator imageGenerator, AppSettings settings)
        {
            _imageGenerator = imageGenerator;
            _settings = settings;
        }

        public async Task<ImageSource> CreateAsync(ParsedEvent parsedEvent, CancellationToken cancellationToken)
        {
            if (parsedEvent == null)
            {
                return null;
            }

            byte[] bytes = await _imageGenerator.CreateImageBytesAsync(
                parsedEvent,
                _settings.SelectedFontName,
                _settings.CustomBackgroundPath,
                _settings.TextVerticalOffset,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (bytes == null)
            {
                return null;
            }

            var bitmap = new BitmapImage();
            using var stream = new MemoryStream(bytes);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
