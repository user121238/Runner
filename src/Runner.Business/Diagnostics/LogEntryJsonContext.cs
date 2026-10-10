using System.Text.Json.Serialization;

namespace Runner.Business.Diagnostics;

/// <summary>源生成的序列化上下文：iOS Release 走 Mono 解释器加裁剪，不依赖反射序列化。</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LogEntry[]))]
internal partial class LogEntryJsonContext : JsonSerializerContext
{
}
