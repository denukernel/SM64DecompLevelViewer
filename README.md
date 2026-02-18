# SM64 Decomp Level Viewer

A powerful and modern C#/.NET 8 level viewer for Super Mario 64 Decompilation projects. This tool allows you to see levels, collision data, and object placement (including macro and special objects) directly from your decomp repo.

![Feature Preview](https://github.com/user-attachments/assets/b6de54b7-9f83-4f9e-a948-274193dbdd51)

## Key Features

- **3D Level Rendering**: High-performance visualization of level geometry using OpenTK.
- **Collision Mesh Support**: Parse and view `collision.inc.c` data with vertex and triangle counts.
- **Full Object Support**:
    - **Standard Objects**: Parsed from level scripts (`script.c`).
    - **Macro Objects**: Automatic parsing of `macro.inc.c` and preset resolution.
    - **Special Objects**: Extraction of trees, signs, and other modular pieces directly from collision data.
- **Dynamic Project Selection**: 100% portable. Select any SM64 decomp root folder and the viewer handles the rest.
- **Modern UI**: Clean WPF-based interface with glassmorphism aesthetics and dark mode support.

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Super Mario 64 Decompilation repository.

### Installation & Usage

1. Clone the repository:
   ```bash
   git clone https://github.com/denukernel/Sm64DecompLevelViewer.git
   ```
2. Build and run the project using Visual Studio or the .NET CLI:
   ```bash
   dotnet run
   ```
3. Click on **"Select SM64 Project Folder"** and navigate to your SM64 decomp root.
4. Select a level from the list on the left to view its details.
5. Click **"View 3D"** to launch the interactive viewer.

## Controls (3D Viewer)

- **WASD**: Move camera
- **Mouse**: Rotate camera
- **Left Click**: Select objects for detailed information
- More on the 3d viewer.
## Technical Stack

- **C# / .NET 8**
- **WPF** (Windows Presentation Foundation)
- **OpenTK** (OpenGL bindings)
- **YamlDotNet** (Level metadata parsing)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request or open an Issue.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Known Bugs
- Some levels like Inside Castle has no visual at all.
- Sometimes the GUI doesn't close.
- 3D Viewer has some screen bugs sometimes
- Some things might be unfinished.
