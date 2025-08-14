namespace Synapse.PatientDmeNeedsUtility
{
    /// <summary>
    /// Provides utility methods for reading and validating resource files from the application directory.
    /// Handles common file operations with proper error checking and exception handling.
    /// </summary>
    public static class ResourceFileHelper
    {
        /// <summary>
        /// Reads and validates the content of a resource file from the application directory.
        /// </summary>
        /// <param name="fileName">The name of the file to read (including extension).</param>
        /// <returns>The content of the file as a string.</returns>
        public static string ReadResourceFile(string fileName)
        {
            string fileContent;

            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, fileName);

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"File not found: {fileName}", path);
                }

                fileContent = File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    throw new InvalidDataException($"File '{fileName}' is empty or contains only whitespace.");
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to load file '{fileName}'.", ex);
            }

            return fileContent;
        }
    }
}
