using System.Text.Json.Serialization;
using HealthChecks.UI.Core;

namespace RenovateWebhooks;

[JsonSerializable(typeof(DockerVersion))]
[JsonSerializable(typeof(UIHealthReport))]
internal partial class JsonSerializationContext : JsonSerializerContext
{
}