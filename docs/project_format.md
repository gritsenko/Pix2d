# Pix2d Project Format (.pix2d)

The Pix2d project format (.pix2d) is a zip-based container format that stores sprite and animation data. The main project data is stored in a `project.json` file within the zip archive.

## Container Structure

A `.pix2d` file is essentially a ZIP archive containing:
- `project.json` - The main project data file containing serialized node hierarchies
- Associated resources (if any)

## Project JSON Structure

The project.json file contains a serialized tree of nodes, with all nodes inheriting from the base `SKNode` class. The primary node types are:

### Base Node Properties (SKNode)

All nodes in the hierarchy inherit these base properties:
```json
{
  "Name": "Node name",
  "Id": "GUID",
  "Position": { "X": 0, "Y": 0 },
  "PivotPosition": { "X": 0, "Y": 0 },
  "Size": { "Width": 100, "Height": 100 },
  "Rotation": 0,
  "Opacity": 1.0,
  "BlendMode": "SrcOver",
  "Effects": [],
  "IsVisible": true,
  "DesignerState": {
    "ShowChildrenInTree": true,
    "IsLocked": false
  },
  "Nodes": [] // Child nodes array
}
```

### Pix2dSprite Node

The main sprite container node that holds layers and animation data:

```json
{
  // Inherits SKNode properties
  "FrameRate": 15,
  "SelectedLayerIndex": 0,
  "OnionSkinSettings": {
    "IsEnabled": false
  }
}
```

### Layer Node

Each layer within a Pix2dSprite contains frame data and sprite information:

```json
{
  // Inherits SKNode properties
  "LockTransparentPixels": false,
  "Frames": [
    {
      "NodeIndex": 0,
      "NodeId": "GUID",
      "IsKeyFrame": true
    }
  ]
}
```

## Frame Structure

Each frame in a layer can be either:
- A unique sprite (has its own bitmap data)
- A reference to another frame's sprite (shares bitmap data)

The `LayerFrameMeta` structure determines this:
```json
{
  "i": 0,        // NodeIndex
  "fid": "GUID", // NodeId
  "k": false     // IsKeyFrame
}
```

## Special Features

1. **Transparency Locking**: Layers can lock transparent pixels to prevent drawing on them
2. **Onion Skinning**: Supports animation preview with previous/next frames
3. **Frame Sharing**: Multiple frames can reference the same sprite data to save memory
4. **Effects**: Nodes can have visual effects applied to them
5. **Blend Modes**: Supports various blend modes for layer compositing

## Example Project Structure

```json
{
  "Name": "MySprite",
  "Size": { "Width": 64, "Height": 64 },
  "Nodes": [
    {
      "Name": "MainSprite",
      "Type": "Pix2dSprite",
      "FrameRate": 15,
      "Nodes": [
        {
          "Name": "Layer 1",
          "Type": "Layer",
          "Frames": [
            {
              "NodeId": "frame1-guid",
              "IsKeyFrame": true
            }
          ]
        }
      ]
    }
  ]
}
```

## Image Data Storage

The project file stores image data separately from the JSON structure for better performance and compression:

### Image References in ZIP

- Image data is stored as separate entries in the ZIP container
- Images are stored in PNG format without compression (CompressionLevel.NoCompression)
- Each image entry has a unique key that matches its reference in the project.json
- A project thumbnail is stored as "__project_thumbnail.jpg" in JPEG format (75% quality)

### Serialization Process

The image serialization process:

1. During node serialization:
   - Images (SKBitmap objects) are extracted from nodes
   - Each image gets a unique key in the data storage
   - JSON contains references to these keys instead of raw image data

2. During project saving:
   ```json
   {
     "spriteNode": {
       "bitmap": "reference_key_123"  // References image in ZIP
     }
   }
   ```

3. The ZIP structure for images:
   ```
   project.zip/
   ├── project.json           // Main project data
   ├── reference_key_123.png  // Image data in PNG format
   ├── reference_key_456.png  // Another image
   └── __project_thumbnail.jpg// Project preview
   ```

### Image Data Handling

- Images are stored uncompressed in PNG format within the ZIP for:
  - Faster loading (no PNG decompression needed)
  - Better overall compression (ZIP handles similar data better)
- Project thumbnail is compressed as JPEG for smaller file size
- Image references use the SimpleDictionaryStorage system for efficient lookup
- During deserialization, the images are loaded from ZIP entries and reconnected with their node references

### Folder-based Projects

When saving as an uncompressed folder structure:

```
project_folder/
├── project.pix2d.json     // Main project data
└── Resources/            // Image resources folder
    ├── image_001.png
    ├── image_002.png
    └── ...
```

This format is useful for:
- Version control systems
- Manual editing
- External tool processing
- Development and debugging

## File Size Optimization

- Frames can share sprite data through NodeId references
- Empty frames don't store bitmap data
- The ZIP container provides basic compression