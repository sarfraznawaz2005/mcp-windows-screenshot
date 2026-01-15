# RegionSnip

A Windows screenshot capture utility written in C# that provides both programmatic full-screen capture and interactive region selection. Designed as a helper executable for the MCP Windows Screenshots server.

## Features

- **Full-screen capture**: Capture entire monitors or all monitors programmatically
- **Interactive region selection**: Drag-to-select screen areas with visual feedback
- **Multi-monitor support**: Handle complex multi-monitor setups
- **JSON output**: Structured output for easy integration with other tools
- **Command-line interface**: Simple argument-based operation
- **Error handling**: Comprehensive error reporting and graceful failure handling

## Prerequisites

- **Windows OS** (Windows Forms required)
- **.NET 8.0 SDK** or later
- **Visual Studio 2022** (recommended) or .NET CLI

## Installation

### Option 1: Build from Source

1. Ensure you have .NET 8.0 SDK installed:
```bash
dotnet --version
```

2. Navigate to the RegionSnip directory:
```bash
cd RegionSnip
```

3. Build the project:
```bash
dotnet build --configuration Release
```

4. The executable will be created at `bin/Release/net8.0-windows/RegionSnip.exe`

### Option 2: Use Pre-built Binary

The compiled executable is already available at `../bin/RegionSnip.exe` (relative to this directory) and includes all necessary dependencies.

## Usage

RegionSnip is designed to be called programmatically and outputs JSON results to stdout.

### Command Line Arguments

| Argument | Description | Example |
|----------|-------------|---------|
| `--mode <mode>` | Capture mode: `full` or `region` (default: region) | `--mode full` |
| `--out <path>` | Output file path (required). Use `.jpg` or `.jpeg` for JPEG compression. | `--out screenshot.jpg` |
| `--all` | Capture all monitors (full mode only) | `--all` |
| `--monitor <n>` | Specific monitor index (0-based, full mode only) | `--monitor 1` |
| `--prompt <text>` | Custom prompt text for region selection | `--prompt "Select area"` |
| `--quality <n>` | JPEG quality level (1-100, default: 80) | `--quality 80` |
| `--scale <n>` | Image scaling factor (0.1-1.0, default: 0.75 for full, 1.0 for region) | `--scale 0.5` |

### Examples

#### Full-screen capture of primary monitor:
```bash
RegionSnip.exe --mode full --out screenshot.png
```

#### Full-screen capture of all monitors:
```bash
RegionSnip.exe --mode full --out screenshot.png --all
```

#### Full-screen capture of specific monitor:
```bash
RegionSnip.exe --mode full --out screenshot.png --monitor 1
```

#### Interactive region selection:
```bash
RegionSnip.exe --mode region --out screenshot.png
```

#### Interactive region selection with custom prompt:
```bash
RegionSnip.exe --mode region --out screenshot.png --prompt "Drag to select the area to capture"
```

#### JPEG Capture with Compression and Scaling:
```bash
# Save as JPEG with 80% quality and 50% size
RegionSnip.exe --mode full --out screenshot.jpg --quality 80 --scale 0.5
```

## Output Format

RegionSnip outputs JSON to stdout with the following structure:

### Success Response (Full Mode):
```json
{
  "ok": true,
  "path": "screenshot.png",
  "mode": "full",
  "monitorIndex": 0,
  "all": false,
  "rect": {
    "x": 0,
    "y": 0,
    "width": 1920,
    "height": 1080
  },
  "width": 1920,
  "height": 1080
}
```

### Success Response (Region Mode):
```json
{
  "ok": true,
  "path": "screenshot.png",
  "mode": "region",
  "rect": {
    "x": 100,
    "y": 100,
    "width": 800,
    "height": 600
  },
  "width": 800,
  "height": 600
}
```

### Error Response:
```json
{
  "ok": false,
  "error": "Description of what went wrong",
  "mode": "region"
}
```

### Cancellation Response (Region Mode):
```json
{
  "ok": false,
  "cancelled": true,
  "mode": "region"
}
```

### Building and Running

#### Using Visual Studio:
1. Open `RegionSnip.csproj` in Visual Studio
2. Build the solution (F6)
3. Run/debug directly from the IDE

#### Using .NET CLI:
```bash
# Build
dotnet publish -c Release -r win-x64 --self-contained false

# Run with arguments
dotnet run -- --mode region --out test.png
```

