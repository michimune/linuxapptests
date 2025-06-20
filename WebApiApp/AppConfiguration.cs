namespace WebApiApp;

/// <summary>
/// Static class to hold the APP_VALUE environment variable
/// </summary>
public static class AppConfiguration
{
    /// <summary>
    /// The value of the APP_VALUE environment variable
    /// </summary>
    public static string AppValue { get; private set; } = string.Empty;

    /// <summary>
    /// Initialize the configuration from environment variables
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when APP_VALUE environment variable is not found</exception>
    public static void Initialize()
    {
        var appValue = Environment.GetEnvironmentVariable("APP_VALUE");
        
        if (string.IsNullOrEmpty(appValue))
        {
            throw new InvalidOperationException("APP_VALUE environment variable is required but not found");
        }

        AppValue = appValue;
    }
}
