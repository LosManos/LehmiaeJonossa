# Azure Dead Letter Monitor

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
- **🔍 Non-Destructive Message Inspector**:
  - Peek dead-lettered messages directly from the dashboard without consuming, locking, or modifying them.
  - Displays `DeadLetterReason`, `DeadLetterErrorDescription`, delivery count, enqueue timestamp, and syntax-formatted JSON body.
- **🎨 4 Interchangeable Themes**:
  - ☀️ **Light** (Default: crisp Fluent light theme)
  - 🌙 **Dark** (Sleek charcoal & slate dark mode)
  - 🧱 **Lego** (Playful primary color scheme with Lego Yellow header, Royal Blue backdrop, and Red DLQ bricks)
  - 💖 **Barbie** (Chic hot pink `#E0218A` and pastel blush aesthetic)
- **⚡ Fast Search & Live Filters**:
  - Instantly search by queue/topic name or filter by *All*, *Dead Letters Only*, or *Healthy Only*.
- **⏱️ Auto-Refresh**:
  - Configurable countdown timer with instant manual refresh button and pause/resume toggle.
- **💾 Local Configuration Persistence**:
  - Configured namespaces and preferred themes saved in `~/.azure-dead-letter-monitor/config.json`.
- **🧪 Built-in Demo Mode**:
  - Toggle between live Azure Service Bus telemetry and realistic mock data to preview workflows without live cloud resources.

---

## Requirements

- [.NET 10](https://dotnet.microsoft.com/download)
- macOS (Apple Silicon or Intel) or Windows 10/11

---

## Getting Started

### 1. Clone & Build
```bash
cd AzureDeadLetterMonitor
dotnet build
```

### 2. Run Tests
```bash
dotnet test
```

### 3. Launch the Application
```bash
dotnet run --project AzureDeadLetterMonitor.csproj
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
AzureDeadLetterMonitor/
├── Models/
│   ├── AppSettings.cs              # Saved namespaces, theme, and auth configuration
│   ├── AppTheme.cs                 # Light, Dark, Lego, Barbie enum
│   ├── DeadLetterMessageDetail.cs  # Peeked DLQ message model with JSON formatter
│   └── ServiceBusEntityMetric.cs   # Queue / Topic Subscription metric properties
├── Services/
│   ├── IServiceBusMonitorService.cs
│   ├── AzureServiceBusMonitorService.cs # Azure Service Bus Admin & Client integration
│   ├── IConfigurationService.cs
│   ├── ConfigurationService.cs     # Local ~/.azure-dead-letter-monitor/config.json
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
│   └── MessageInspectorViewModel.cs# Non-destructive DLQ message peeking
├── Views/
│   ├── MainWindow.axaml            # Fluent responsive dashboard layout
│   └── MainWindow.axaml.cs
└── tests/
    └── AzureDeadLetterMonitor.Tests/
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