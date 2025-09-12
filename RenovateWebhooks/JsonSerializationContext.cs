using System.Text.Json.Serialization;
using Docker.DotNet.Models;

namespace RenovateWebhooks;

[JsonSerializable(typeof(VersionResponse))]
internal partial class JsonSerializationContext : JsonSerializerContext
{
}