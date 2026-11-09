using System.Text.Json;
using System.Text.Json.Serialization;

namespace Credentials.Models.Models.Zeebe
{
    /// <summary>
    /// One matching row of the environment variables DMN.
    /// </summary>
    public class CredentialsCamundaOutput
    {
        /// <summary>The environment variable name given to the executor.</summary>
        public string env { get; set; } = string.Empty;

        /// <summary>
        /// The plain value. Always present, but ignored when isSecret is Y - the real
        /// value is then read from vaultPath instead.
        /// </summary>
        public string value { get; set; } = string.Empty;

        /// <summary>Groups related variables so the right handler provisions them, e.g. postgres, trino, tre, s3.</summary>
        public string tag { get; set; } = string.Empty;

        /// <summary>
        /// The image codes this variable applies to. Empty means every image.
        /// Accepts a FEEL list or a single string, since an admin may type either.
        /// </summary>
        [JsonConverter(typeof(ImageCodeListConverter))]
        public List<string> imageCode { get; set; } = new();

        /// <summary>"Y" when the value is a secret to be read from vaultPath rather than used directly.</summary>
        public string? isSecret { get; set; }

        /// <summary>Where in Vault the real value lives, when isSecret is Y.</summary>
        public string? vaultPath { get; set; }
    }

    /// <summary>
    /// Reads the imageCode column whichever way it was written.
    ///
    /// A DMN output cell is a FEEL expression, so an admin can type ["a","b"], "a", or
    /// leave it empty, and Zeebe returns a list, a string or null accordingly. Accepting
    /// all three means one mistyped cell cannot make a row unreadable.
    /// </summary>
    public class ImageCodeListConverter : JsonConverter<List<string>>
    {
        // Without this the serializer handles a JSON null itself and assigns null to the
        // property, never calling Read - so an empty DMN cell would give a null list and
        // throw downstream rather than meaning "every image".
        public override bool HandleNull => true;

        public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return new List<string>();

                case JsonTokenType.String:
                    var single = reader.GetString();
                    return string.IsNullOrWhiteSpace(single)
                        ? new List<string>()
                        : new List<string> { single.Trim() };

                case JsonTokenType.StartArray:
                    var codes = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType != JsonTokenType.String)
                        {
                            continue;
                        }

                        var code = reader.GetString();
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            codes.Add(code.Trim());
                        }
                    }

                    return codes;

                default:
                    reader.Skip();
                    return new List<string>();
            }
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var code in value)
            {
                writer.WriteStringValue(code);
            }

            writer.WriteEndArray();
        }
    }
}
