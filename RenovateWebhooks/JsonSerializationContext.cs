using System.Text.Json.Serialization;
using Docker.DotNet.Models;
using HealthChecks.UI.Core;

namespace RenovateWebhooks;

[JsonSerializable(typeof(VersionResponse))]
[JsonSerializable(typeof(UIHealthReport))]
internal partial class JsonSerializationContext : JsonSerializerContext
{
}