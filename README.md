# Unity-Posthog

Code for logging events in Unity games/apps to [PostHog](https://posthog.com/).


Originally forked from [Gamefound PostHog.Net](https://github.com/Gamefound/PostHog.NET)

## Usage example

```csharp
import UnityPosthog.AnalyticsTracker;

// Log a basic event/user action
AnalyticsTracker.Track("usage_events/app_opened");

// Log an event with associated properties
AnalyticsTracker.Track("usage_event/example_event_with_properties"),
            AnalyticsTracker.makeProperties(new Dictionary<string, object> { { "propertyKey", "propertyValue" } });


```
