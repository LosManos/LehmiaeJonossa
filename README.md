# LehmiaeJonossa

A high-performance cross-platform desktop monitor built with **.NET 10** and **Avalonia UI** for **macOS** and **Windows**, designed to give cloud and DevOps engineers an instant situational overview of dead-lettered messages in Azure Service Bus.

![Dashboard Preview](https://raw.githubusercontent.com/Azure/azure-sdk-for-net/main/sdk/servicebus/Azure.Messaging.ServiceBus/README.md)

---

## Key Features

- **🚨 Prominent Dead Letter Alerting**:
  - Hero KPI card for total dead letters across all queues and subscriptions.
  - Dedicated **"Needs Immediate Attention" Hotlist** highlighting only problem entities.
  - Entities with dead letters are automatically sorted to the top.
- **🛡️ Managed Identity & Azure CLI Support**:
  - Uses `DefaultAzureCredential`: automatically uses Azure **Managed Identity** (System or User-Assigned) in Azure environments and **Azure CLI** (`az login`), PowerShell, or Visual Studio when running locally on macOS & Windows.
  - Explicit System-Assigned and User-Assigned Managed Identity credential modes.
- **🔍 Dedicated Desktop DLQ Inspector & Detail Windows**:
  - **DeadLetterInspectorWindow**: Proper standalone window with a tabular, row-by-row view of messages displaying Enqueued Time, Message ID, Sequence #, Delivery Attempts, and Message Body Preview.
  - **Safe Pagination**: Sequence-based pagination ("Next Page ▶" / "◀ Previous Page") strictly capped at safe batch sizes.
  - **MessageDetailWindow**: Standalone window revealing all message diagnostics, headers, delivery attempts, custom application properties, and formatted body with syntax copying.
  - **Previous & Next Navigation**: Seamlessly navigate back and forth through peeked messages directly from the detail window.
  - **Esc Key Closure**: Both inspector and detail windows close immediately on pressing `Esc`.
- **🎨 4 Interchangeable Themes**:
  - ☀️ **Light** (Default: crisp Fluent light theme)
  - 🌙 **Dark** (Sleek charcoal & slate dark mode)
  - 🧱 **Lego** (Playful primary color scheme with Lego Yellow header, Royal Blue backdrop, and Red DLQ bricks)
  - 💖 **Barbie** (Chic hot pink `#E0218A` and pastel blush aesthetic)
- **⚡ Fast Search & Live Filters**:
  - Instantly search by queue/topic name or filter by *All*, *Dead Letters Only*, or *Healthy Only*.
- **⏱️ Auto-Refresh**:
  - Configurable countdown timer with instant manual refresh button and pause/resume toggle.
- **⌨️ 100% Keyboard Navigable (Windows Standard Mnemonics & macOS / Cross-Platform Shortcuts)**:
  - Accessible via Windows standard `Alt+{letter}` mnemonics with direct typographical underlining under the exact shortcut key (`<u>N</u>amespace`, `<u>R</u>efresh`, `<u>A</u>ccount`, `<u>D</u>emo Mode`, `<u>T</u>heme`, etc.).
  - Unlabeled action buttons display compact shortcut badges (`Shift+Space` for Menu, `Alt+A` for Add namespace, `Alt+D` for Auto-discover, `Alt+T` for Timer pause/resume).
  - Quick refresh via dual `F5` and `⌘R` / `Ctrl+R`.
  - Arrow-key selection and `Cmd+Arrows` navigation throughout the Dead Letter Inspector and Message Detail windows.
  - Context-aware shortcut footers in each window, toggleable on/off directly from the menu.
- **💾 Local Configuration Persistence**:
  - Configured namespaces, preferred themes, keyboard hint preferences, and Demo Mode state saved in `~/.lehmiae-jonossa/config.json`.
- **🧪 Built-in Demo Mode**:
  - Toggle between live Azure Service Bus telemetry and realistic mock data with multi-page demo dead letters to preview workflows without live cloud resources (state persisted across application restarts).

---

## ⌨️ Keyboard Navigation & Shortcuts Reference

The entire application can be navigated without touching a mouse.

### Main Dashboard
| Shortcut | Action | Description |
|---|---|---|
| `Shift + Space` | **Toggle Menu** | Opens/closes the hamburger sidebar menu drawer |
| `F5` or `⌘R` / `Ctrl + R` or `Alt + R` | **Refresh** | Refreshes entity metrics and resets auto-refresh countdown |
| `⌘1` – `⌘9` (or `Ctrl + 1-9`) | **Inspect Hotlist** | Focuses and inspects the corresponding DLQ hotlist item #1 to #9 |
| `↑` / `↓` | **Navigate Hotlist** | Cycles selection through entities in the "Needs Immediate Attention" hotlist |
| `Alt + N` | **Focus Namespace** | Jumps focus directly to the Service Bus Namespace dropdown |
| `Alt + A` | **Add Namespace** | Opens the inline namespace creation bar (`Enter` to save, `Esc` to cancel) |
| `Alt + D` | **Discover Namespaces** | Triggers auto-discovery of Service Bus namespaces in your Azure subscription |
| `Alt + T` | **Toggle Auto-Refresh** | Pauses or resumes the automatic metric refresh timer |
| `Alt + S` | **Search Entities** | Focuses the entity search text box |
| `Alt + F` | **Filter Entities** | Focuses the entity type filter dropdown |
| `Alt + E` | **Focus Table** | Focuses the All Entities DataGrid for keyboard scrolling |
| `Esc` | **Close Overlay / Menu** | Closes any open drawer, popup, or input banner |

### Hamburger Menu Drawer
| Shortcut | Action | Description |
|---|---|---|
| `↑` / `↓` | **Navigate Items** | Moves between Account, Demo Mode, Theme, Keyboard Hints, and Close buttons |
| `Alt + A` | **Account Dialog** | Opens the Azure Account & Authentication dialog |
| `Alt + D` or `Space` | **Demo Mode** | Toggles offline mock data mode ON / OFF |
| `Alt + T` or `→` | **Expand Theme Menu** | Expands the theme switcher sub-menu (`←` collapses it) |
| `Alt + L` / `Alt + K` / `Alt + G` / `Alt + B` | **Select Theme** | Activates Light, Dark, Lego, or Barbie theme |
| `Alt + K` or `Alt + H` or `Space` | **Toggle Hints** | Turns shortcut footers and unlabeled button badges ON / OFF |
| `Esc` or `Alt + C` | **Close Menu** | Closes the drawer and restores focus to dashboard |

### Dead Letter Queue Inspector
| Shortcut | Action | Description |
|---|---|---|
| `↑` / `↓` | **Select Message** | Navigates up and down through messages in the DLQ table |
| `⌘←` / `⌘→` or `Alt + P` / `Alt + N` | **Previous / Next Page** | Navigates to the previous or next page of peeked messages |
| `Enter` or `Space` or `Alt + D` | **View Details** | Opens the standalone Message Detail window for the highlighted message |
| `F5` or `⌘R` / `Ctrl + R` or `Alt + R` | **Refresh** | Re-peeks the current DLQ and resets to page 1 |
| `Esc` or `Alt + C` | **Close** | Closes the inspector window |

### Message Detail Window
| Shortcut | Action | Description |
|---|---|---|
| `⌘←` / `⌘→` or `⌘↑` / `⌘↓` | **Previous / Next Message** | Smoothly cycles through messages |
| `Alt + P` / `Alt + N` | **Previous / Next** | Moves to the previous or next message in the inspected batch |
| `Alt + B` | **Copy Body** | Copies the formatted JSON / raw message payload to the clipboard |
| `Esc` or `Alt + C` | **Close** | Closes the detail window |

### Account & Authentication Dialog
| Shortcut | Action | Description |
|---|---|---|
| `Alt + A` | **Authentication Method** | Focuses the Authentication Method selector dropdown |
| `↑` / `↓` | **Select Auth Mode** | Cycles through DefaultAzureCredential, SystemAssigned, and UserAssigned |
| `Enter` or `Esc` or `Alt + D` | **Done / Close** | Saves and closes the dialog |

## Requirements

- [.NET 10](https://dotnet.microsoft.com/download)
- macOS (Apple Silicon or Intel) or Windows 10/11

---

## Getting Started

### 1. Clone & Build
```bash
cd LehmiaeJonossa
dotnet build
```

### 2. Run Tests
```bash
dotnet test
```

### 3. Launch the Application
```bash
dotnet run --project LehmiaeJonossa.csproj
```

---

## Azure Authentication Setup

### Local Development (macOS / Windows)
Authenticate once via the Azure CLI:
```bash
az login
```
The application will automatically pick up your credentials through `DefaultAzureCredential`. Ensure your account has the **Azure Service Bus Data Receiver** and **Azure Service Bus Data Owner** (or **Contributor**) RBAC role on the target Service Bus namespace.

### In Azure Environments (Managed Identity)
1. Assign a **System-Assigned** or **User-Assigned Managed Identity** to the VM, Container, or Host.
2. Grant the identity the **Azure Service Bus Data Receiver** RBAC role on the Service Bus namespace.
3. In the application, select **Managed Identity (System-Assigned)** or **Managed Identity (User-Assigned)** with the Client ID.

---

## Project Architecture

```
LehmiaeJonossa/
├── Models/
│   ├── AppSettings.cs              # Saved namespaces, theme, and auth configuration
│   ├── AppTheme.cs                 # Light, Dark, Lego, Barbie enum
│   ├── DeadLetterMessageDetail.cs  # Peeked DLQ message model with JSON formatter & preview
│   └── ServiceBusEntityMetric.cs   # Queue / Topic Subscription metric properties
├── Services/
│   ├── IServiceBusMonitorService.cs
│   ├── AzureServiceBusMonitorService.cs # Azure Service Bus Admin & Client integration with pagination
│   ├── IConfigurationService.cs
│   ├── ConfigurationService.cs     # Local ~/.lehmiae-jonossa/config.json
│   ├── IThemeService.cs
│   └── ThemeService.cs             # Dynamic Avalonia ResourceDictionary switcher
├── Themes/
│   ├── LightTheme.axaml            # Default Light Fluent Theme
│   ├── DarkTheme.axaml             # Dark Theme
│   ├── LegoTheme.axaml             # Lego Primary Color Theme
│   └── BarbieTheme.axaml           # Barbie Chic Pink Theme
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── MainWindowViewModel.cs      # Core dashboard orchestrator & timer
│   ├── MessageInspectorViewModel.cs# Paged DLQ message inspector orchestrator
│   └── MessageDetailViewModel.cs   # Message detail viewer with Previous/Next navigation
├── Views/
│   ├── MainWindow.axaml            # Fluent responsive dashboard layout
│   ├── MainWindow.axaml.cs
│   ├── DeadLetterInspectorWindow.axaml      # Proper DLQ inspector window
│   ├── DeadLetterInspectorWindow.axaml.cs
│   ├── MessageDetailWindow.axaml            # Proper message detail window
│   └── MessageDetailWindow.axaml.cs
└── tests/
    └── LehmiaeJonossa.Tests/
        └── MonitorTests.cs         # Automated unit tests
```

## License

License LGPLv3 + NoEvil.

License is LGPLv3+NoEvil

### LGPLv3
https://www.gnu.org/licenses/lgpl-3.0.txt

### NoEvil
https://raw.githubusercontent.com/LosManos/Ushi-Suki-KoSumoso/main/license.md

The code is not available for companies that create, buy or sell munitions.
This includes companies and organisations that are owned by companies making munitions. 
The list includes, but is not limited to Bofors, Saab and Lockheed Martin.

The code is not available for countries where capital punishment or torture is allowed or used. 
The list includes, but is not limited to, Egypt, China and USA. 

An exception to the above is where the company or organisation takes an active role in working against weapons, capital punishment or torture regardless of country. 
The list includes, but is not limited to Amnesty and Greenpeace.

The code is also not available for companies and persons dealing with unlawful things or aiding the same.