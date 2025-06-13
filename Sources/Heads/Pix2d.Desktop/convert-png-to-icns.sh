#!/bin/bash
# Usage: ./convert-png-to-icns.sh <input-png> <output-icns>
set -e
INPUT_PNG="$1"
OUTPUT_ICNS="$2"
TMP_ICONSET="tmp.iconset"

rm -rf "$TMP_ICONSET"
mkdir "$TMP_ICONSET"

# Generate iconset at various sizes
sips -z 16 16     "$INPUT_PNG" --out "$TMP_ICONSET/icon_16x16.png"
sips -z 32 32     "$INPUT_PNG" --out "$TMP_ICONSET/icon_16x16@2x.png"
sips -z 32 32     "$INPUT_PNG" --out "$TMP_ICONSET/icon_32x32.png"
sips -z 64 64     "$INPUT_PNG" --out "$TMP_ICONSET/icon_32x32@2x.png"
sips -z 128 128   "$INPUT_PNG" --out "$TMP_ICONSET/icon_128x128.png"
sips -z 256 256   "$INPUT_PNG" --out "$TMP_ICONSET/icon_128x128@2x.png"
sips -z 256 256   "$INPUT_PNG" --out "$TMP_ICONSET/icon_256x256.png"
sips -z 512 512   "$INPUT_PNG" --out "$TMP_ICONSET/icon_256x256@2x.png"
sips -z 512 512   "$INPUT_PNG" --out "$TMP_ICONSET/icon_512x512.png"
sips -z 1024 1024 "$INPUT_PNG" --out "$TMP_ICONSET/icon_512x512@2x.png"

iconutil -c icns "$TMP_ICONSET" -o "$OUTPUT_ICNS"
rm -rf "$TMP_ICONSET"
echo "Created $OUTPUT_ICNS from $INPUT_PNG"
