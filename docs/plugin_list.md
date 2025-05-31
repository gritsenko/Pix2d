# Pix2d Plugins List

This document lists all available plugins in Pix2d and their primary functions.

## Core Plugins

### Drawing Plugin
Located in: `Plugins/Drawing/DrawingPlugin.cs`
The core drawing plugin that provides essential drawing tools and functionality including:
- Drawing tools: Brush, Line, Rectangle, Oval, Triangle
- Selection tools: Rectangle selection, Lasso selection, Color selection
- Utility tools: Eraser, Fill tool, Eyedropper
- Implements brush settings and drawing commands

### Sprite Plugin
Located in: `Core/Plugins/Sprite/SpritePlugin.cs`
Provides sprite editing and animation functionality:
- Sprite editing tools and commands
- Animation frame management
- Cut/Copy/Paste functionality for sprite elements

## Format Support Plugins

### PSD Plugin
Located in: `Plugins/FormatSupport/Psd/PsdPlugin.cs`
Provides support for Adobe Photoshop (.psd) file format:
- PSD file import capability
- Layer and blend mode support
- Color mode handling (RGB, CMYK, LAB, etc.)

### PXM Plugin
Located in: `Plugins/FormatSupport/Pxm/PxmPlugin.cs`
Handles Pix2d's native .pxm file format:
- Native file format import/export
- Layer and frame data management
- Animation state preservation

### TMX Plugin
Located in: `Plugins/FormatSupport/Tmx/TileMapPlugin.cs`
Provides support for Tiled Map Editor (.tmx) files:
- TMX file import capability
- Tilemap rendering and management
- Layer and tileset handling

### Spine Plugin
Located in: `Plugins/FormatSupport/Spine/SpinePlugin.cs`
Adds support for Spine animation files:
- Spine JSON file import capability
- Skeleton animation data support

## Effect Plugins

### Base Effects Plugin
Located in: `Plugins/Effects/BaseEffectsSettings/BaseEffectsPlugin.cs`
Provides fundamental image effects:
- Color overlay
- Shadow effects
- Blur effects
- Glow effects
- Outline effects
- Grayscale conversion
- Image adjustment tools

## AI and Advanced Features

### AI Plugin
Located in: `Plugins/Ai/AiPlugin.cs`
Provides AI-powered features:
- Object extraction tools
- Image generation capabilities
- Uses embedded neural network model (u2netp.onnx)

## Utility Plugins

### OpenGL Plugin
Located in: `Plugins/OpenGl/OpenGlPlugin.cs`
Provides OpenGL rendering support:
- OpenGL-based view implementation
- Graphics acceleration features

### Simple Plugin
Located in: `Plugins/SimplePlugin/SimplePlugin.cs`
Basic plugin template and simple tools

### Pixel Text Plugin
Located in: `Plugins/PixelText/PixelTextPlugin.cs`
Text tools for pixel art:
- Pixel font rendering
- Text manipulation tools

### PNG Compress Plugin
Located in: `Plugins/PngCompress/PngCompressPlugin.cs`
PNG optimization features:
- PNG file compression
- Image optimization tools

### HTTP Host Plugin
Located in: `Plugins/HttpHost/HttpHostPlugin.cs`
Network functionality:
- Web server capabilities
- Remote access features