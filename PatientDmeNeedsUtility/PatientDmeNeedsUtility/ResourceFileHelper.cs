using Microsoft.Extensions.Logging;

namespace Synapse.PatientDmeNeedsUtility
{
    /// <summary>
    /// Provides utility methods for reading and validating resource files from the application directory.
    /// Handles common file operations with proper error checking and exception handling.
    /// </summary>
    public class ResourceFileHelper
    {
        private readonly ILogger<ResourceFileHelper> _logger;

        public ResourceFileHelper(ILogger<ResourceFileHelper> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Reads and validates the content of a resource file from the application directory.
        /// </summary>
        /// <param name="fileName">The name of the file to read (including extension).</param>
        /// <returns>The content of the file as a string.</returns>
        public string ReadResourceFile(string fileName)
        {
            try
            {
                _logger.LogDebug("Attempting to read resource file: {FileName}", fileName);
                string path = Path.Combine(AppContext.BaseDirectory, fileName);

                if (!File.Exists(path))
                {
                    _logger.LogError("File not found: {FileName} at path: {Path}", fileName, path);
                    throw new FileNotFoundException($"File not found: {fileName}", path);
                }

                var fileContent = File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    _logger.LogError("File is empty: {FileName}", fileName);
                    throw new InvalidDataException($"File '{fileName}' is empty or contains only whitespace.");
                }

                _logger.LogDebug("Successfully read file: {FileName}", fileName);
                return fileContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load file: {FileName}", fileName);
                throw;
            }
        }
    }
}
