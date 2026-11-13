# FiveM Vehicle Builder

A comprehensive tool for managing, organizing, and building vehicle resources for FiveM servers. Import vehicles from various sources (folders, RPF archives), organize them into packs, and export ready-to-use FiveM resources.

## Features

- **Import Vehicles**: Import from folders or directly from RPF archives (DLC.rpf, vehicles.rpf)
- **Smart Detection**: Automatically detects vehicles from meta files or YFT filenames
- **Template Generation**: Creates missing vehicles.meta and handling.meta files
- **Pack Management**: Organize vehicles into packs for different purposes
- **Variant System**: Create multiple spawn variants from a single vehicle model
- **Build System**: Generate ready-to-use FiveM resources with proper structure
- **Export Formats**: Export vehicle data as JSON, Lua, or SQL
- **Validation**: Check for issues like duplicate siren IDs, missing files, invalid XML
- **Auto-Fix**: Automatic repair of common issues

## Getting Started

### Prerequisites

- Windows 10/11
- .NET 9.0 Runtime (included in self-contained builds)

### Installation

Download the latest release from the [Releases](https://github.com/YOUR_USERNAME/FiveMVehicleBuilder/releases) page.

Or build from source:

```bash
git clone https://github.com/YOUR_USERNAME/FiveMVehicleBuilder.git
cd FiveMVehicleBuilder
dotnet build
```

### First Run

1. Launch `FiveMVehicleBuilder.App.exe`
2. The setup wizard will guide you through creating or selecting a library folder
3. Your library is where all imported vehicles and packs are stored

## Usage

### Importing Vehicles

**From Folder:**
1. Click "Import Folder" in the Records view
2. Select a folder containing vehicle files
3. Choose a category and optionally rename the vehicle
4. The tool will detect all vehicle files and copy them to your library

**From RPF:**
1. Click "Import RPF"
2. Select a DLC.rpf or vehicles.rpf file
3. Browse and select vehicles from the RPF
4. Selected vehicles are extracted and imported

### Managing Records

Each imported vehicle becomes a "Record" containing:
- Original model files (.yft, .ytd, etc.)
- Meta files (vehicles.meta, handling.meta, etc.)
- Audio files (.rel, .awc)

You can:
- Rename records
- Change categories
- Add/remove variants
- Set siren IDs for emergency vehicles
- Configure vehicle class and price

### Creating Packs

1. Go to "Packs" view
2. Click "New Pack"
3. Assign records to the pack from the Records view
4. Build the pack to generate a FiveM resource

### Building Resources

1. Select a pack
2. Click "Build"
3. The tool generates:
   - `fxmanifest.lua`
   - Combined `vehicles.meta`, `handling.meta`, etc.
   - `stream/` folder with all model files
   - Properly renamed files for each variant

### Exporting Data

Export vehicle data for external systems:
- **JSON**: General-purpose data export
- **Lua**: For Lua-based frameworks
- **SQL**: For database imports

## CLI Usage

A command-line interface is also available:

```bash
# Show help
fvb --help

# List all records
fvb record list

# Import a vehicle
fvb import folder "C:\Path\To\Vehicle"

# Build a pack
fvb pack build mypack

# Check for issues
fvb check all
```

## Project Structure

```
library/
├── records/                 # Imported vehicles
│   └── vehicle_name/
│       └── original/
│           ├── data/        # Meta files
│           └── stream/      # Model files
├── packs/                   # Pack configurations
├── build/                   # Built resources
└── library.json             # Library configuration
```

## Extending with Custom Providers

FiveM Vehicle Builder supports custom providers for:
- **Organization Types**: Define custom org types (police, medical, etc.)
- **Export Formats**: Add custom export format generators

Create your own provider by implementing `IOrganizationTypeProvider` or `IExportFormatProvider` interfaces. See the [Custom Providers Guide](docs/custom-providers.md) for details.

## Building from Source

```bash
# Clone the repository
git clone https://github.com/YOUR_USERNAME/FiveMVehicleBuilder.git
cd FiveMVehicleBuilder

# Build
dotnet build

# Run tests
dotnet test

# Create release build
dotnet publish src/App/FiveMVehicleBuilder.App/FiveMVehicleBuilder.App.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  --output ./publish
```

## Forking for Custom Use

Want to add your own custom organization types, export formats, or modifications?

1. **Fork this repository**
2. Create a `src/Custom` folder for your customizations
3. Implement your own providers:
   ```csharp
   public class MyOrgTypeProvider : DefaultOrganizationTypeProvider
   {
       public override IReadOnlyList<string> GetOrganizationTypes() =>
           ["police", "medical", "fire", "custom_org"];
   }
   ```
4. Initialize your providers in `App.xaml.cs`:
   ```csharp
   Customization.SetOrganizationTypeProvider(new MyOrgTypeProvider());
   ```

This allows you to maintain project-specific configurations while still benefiting from upstream updates.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

This project was initially developed with the assistance of [Claude](https://claude.ai), Anthropic's AI assistant. The entire codebase, including architecture decisions, implementation, and documentation, was created through collaborative AI-assisted development.

We welcome contributions from the community to extend and improve this tool. Whether you want to fix bugs, add features, or create custom providers for your server - your contributions are appreciated!

---

**Note**: This tool is designed for legitimate FiveM server development. Please ensure you have the rights to use any vehicle models you import.
