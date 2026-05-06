using System;
using System.Text.Json;

namespace TestHarnessV2.Monitoring
{
    internal enum RouletteEventType
    {
        Unknown,
        RoundResult,
        UiPing,
        SessionInitialized
    }

    internal sealed class RouletteEvent
    {
        public RouletteEvent(RouletteEventType eventType, string rawEventType, string? egmId)
        {
            EventType = eventType;
            RawEventType = rawEventType;
            EgmId = egmId;
        }

        public RouletteEventType EventType { get; }
        public string RawEventType { get; }
        public string? EgmId { get; }
    }

    internal static class RouletteEventParser
    {
        public static RouletteEvent Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            string rawEventType = GetEventType(root);
            string? egmId = GetString(root, "egmId");
            if (string.IsNullOrWhiteSpace(egmId) &&
                root.TryGetProperty("payload", out var payload) &&
                payload.ValueKind == JsonValueKind.Object)
            {
                egmId = GetString(payload, "egmId");
            }

            return new RouletteEvent(MapEventType(rawEventType), rawEventType, egmId);
        }

        private static RouletteEventType MapEventType(string rawEventType)
        {
            return rawEventType.ToLowerInvariant() switch
            {
                "round_result" => RouletteEventType.RoundResult,
                "ui_ping" => RouletteEventType.UiPing,
                "session_initialized" => RouletteEventType.SessionInitialized,
                _ => RouletteEventType.Unknown
            };
        }

        private static string GetEventType(JsonElement root)
        {
            string? eventType = GetString(root, "@event");
            if (!string.IsNullOrWhiteSpace(eventType))
                return eventType;

            eventType = GetString(root, "event");
            if (!string.IsNullOrWhiteSpace(eventType))
                return eventType;

            eventType = GetString(root, "EventType");
            return eventType ?? string.Empty;
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
                return null;

            return property.GetString();
        }
    }
}
