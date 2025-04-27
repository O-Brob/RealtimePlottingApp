using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace RealtimePlottingApp.Services.ConfigHandler;

/// <summary>
/// A concrete Singleton implementation of IConfigService that
/// uses JSON to define the config files. It uses System.Text.Json
/// to allow a dynamic structure, allowing the user to add key-valye
/// pairs as they please to the configuration file.
/// </summary>
public class ConfigService : IConfigService
{
    // ========== Instance variables ========== //
    private readonly JsonArray _jsonArray = new JsonArray();

    // ========== Constructors ========== //
    public static ConfigService Instance { get; } = new ConfigService(); // Singleton!

    // ========== API Methods ========== //
    public void AddToConfig(string key, object value)
    {
        // Find old entry at given key if it already exists and remove it
        JsonObject? oldKeyVal = _jsonArray
            .OfType<JsonObject>()
            .FirstOrDefault(o => o["Key"]?.GetValue<string>() == key);
        
        if (oldKeyVal != null)
            _jsonArray.Remove(oldKeyVal);

        // Wrap the incoming object in JsonValue
        JsonNode? node = JsonValue.Create(value);

        // Create new entry with the key and value, then add it
        JsonObject entry = new JsonObject
        {
            ["Key"]   = key,
            ["Value"] = node
        };
        _jsonArray.Add(entry);
    }

    public void ExportConfig(string path)
    {
        // Export as file at the given path.
        File.WriteAllText(path, _jsonArray.ToString());
    }
    
    public T? LoadConfig<T>(string path, string key)
    {
        // Load the configuration and search for the provided key.
        JsonObject? entry = Instance
            // Loads JsonArray from file.
            .LoadFullConfig(path)
            // Make enumerator over all json objects.
            .OfType<JsonObject>()
            // Get the first Json Object with the given key.
            .FirstOrDefault(o => o["Key"]?.GetValue<string>() == key);

        // If the entry exists and has a value as expected, return value.
        if (entry?["Value"] is JsonValue v)
            return v.GetValue<T>();

        // Could not get value. Return default of type T.
        return default;
    }
    
    // ========== Private Helpers ========== //
    // Loads a FULL configuration file as a JSON Array
    private JsonArray LoadFullConfig(string path)
    {
        // Check the file to load from exists.
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file not found: {path}");

        // Read whole json file, and try to parse everything as the root json object
        string text = File.ReadAllText(path);
        JsonNode root = JsonNode.Parse(text)
                        ?? throw new InvalidDataException("Empty or invalid JSON");
        
        // Check that the json has expected root structure (Json Array Root)
        if (root is not JsonArray arr) 
            throw new InvalidDataException("Config file root must be a JSON array");
        
        // Clear in-memory json array and add a deep clone of each json node into it
        _jsonArray.Clear();
        foreach (var item in arr)
        {
            // ReSharper disable once NullableWarningSuppressionIsUsed <-- Rider IDE comment, ignore.
            JsonNode clone = item!.DeepClone();  
            _jsonArray.Add(clone);
        }
        
        // Return the entire local state json array populated from the file.
        return _jsonArray;
    }

}