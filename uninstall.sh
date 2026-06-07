#!/bin/bash
# cxshell Uninstaller
# Removes cxshell binary
# Copyright (c) Nikolaos Protopapas. All rights reserved.
# Licensed under the MIT License.

INSTALL_DIR="$HOME/.local/bin"

echo "cxshell Uninstaller"
echo ""

# Remove binary
if [ -f "$INSTALL_DIR/cxshell" ]; then
    rm "$INSTALL_DIR/cxshell"
    echo "✓ Removed $INSTALL_DIR/cxshell"
else
    echo "  Binary not found at $INSTALL_DIR/cxshell"
fi

# Remove uninstaller
if [ -f "$INSTALL_DIR/cxshell-uninstall.sh" ]; then
    rm "$INSTALL_DIR/cxshell-uninstall.sh"
fi

# Clean PATH from shell config
for RC in "$HOME/.bashrc" "$HOME/.zshrc"; do
    if [ -f "$RC" ] && grep -q "$INSTALL_DIR" "$RC" 2>/dev/null; then
        sed -i "\|$INSTALL_DIR|d" "$RC"
        echo ""
        echo "✓ Removed PATH entry from $RC"
    fi
done

echo ""
echo "✓ cxshell uninstalled."
