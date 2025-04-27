namespace RealtimePlottingApp.Services.ConfigHandler;

/// <summary>
/// An IConfigService permits saving configuration parameters
/// as key-value pairs to a configuration file.
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// Adds a key-value pair to the local representation of a configuration.
    /// </summary>
    /// <param name="key">The key representing the stored value</param>
    /// <param name="value">The value acoompanied with the key</param>
    void AddToConfig(string key, object value);

    /// <summary>
    /// Try to load a value of type T from from the config
    /// file at the given path using the provided key.
    /// </summary>
    /// <param name="path">The path to load the config value from</param>
    /// <param name="key">The key representing the value to load</param>
    /// <typeparam name="T">The expected type of the value accompanying `key`</typeparam>
    /// <returns>The value accompanying they provided key, in the file at the provided path.</returns>
    T? LoadConfig<T>(string path, string key);
    
    /// <summary>
    /// Exports the current local representation
    /// of a configuration as a file.
    /// </summary>
    /// <param name="path">The path to export the configuration file</param>
    void ExportConfig(string path);
}