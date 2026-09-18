# AI Agent Instructions & Guidelines

## Core Principles & Performance Guardrails

### 1. Strict Pagination & Query Limits (Cost & Resource Protection)
- **Never Loop Unbounded**: There must **never** be a loop or operation that attempts to retrieve or iterate through all messages in an entity, queue, topic, or dead-letter queue (DLQ).
- **Mandatory Paging / Batch Capping**: Any message retrieval or inspection must be strictly paged and capped at small, safe batch sizes (e.g., maximum 20 to 50 messages per call).
- **Use Broker Metadata for Counts**: Never enumerate or peek messages to calculate entity metrics or dead-letter counts. Always use broker-level runtime property APIs (`GetQueuesRuntimePropertiesAsync`, `GetSubscriptionsRuntimePropertiesAsync`) which retrieve integer counters directly from management metadata without touching message storage.
- **Non-Destructive Reads**: Dead-letter inspection must always use non-destructive operations (such as `PeekMessagesAsync`). Never receive/dequeue or alter message delivery counts during monitoring.

### 2. Architecture & Patterns
- **MVVM Framework**: Avalonia UI using CommunityToolkit.Mvvm.
- **Service Layer**: Keep Azure SDK interaction isolated in `Services/` (e.g., `AzureServiceBusMonitorService`).
- **Authentication**: Rely on `Azure.Identity` with priority given to Azure CLI and developer credentials locally, and Managed Identity when deployed.

### 3. Keyboard Navigation & Accessibility
- **100% Keyboard Navigable**: All features, dialogs, drawers, and inspection tools must be fully operable by keyboard alone.
- **Access Key Mnemonics (Windows Standard)**: Key actions and form labels must provide `Alt+{letter}` access shortcuts. The exact shortcut letter must be visibly underlined (using typographical `<Run TextDecorations="Underline">` — never literal underscore characters in text).
- **Shortcut Hints & Overlays**: Unlabeled icon buttons must provide compact shortcut hint badges when keyboard hints are enabled. Windows must display context-appropriate shortcut footers, controllable via the user toggle.
- **Cross-Platform Parity**: Shortcut handling must intercept tunneling key events to guarantee identical behavior across macOS (handling Option/Cmd properly) and Windows (handling Alt/Ctrl).

