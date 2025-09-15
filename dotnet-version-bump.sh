#!/bin/bash

# bump-version.sh - Bumps version numbers across all .NET projects
#
# SYNOPSIS
#     ./bump-version.sh [major|minor|patch] [--dry-run]
#     ./bump-version.sh --version <new-version> [--dry-run]
#
# DESCRIPTION
#     This script helps manage version numbers for production releases.
#     It can bump major, minor, or patch versions across all projects.
#
# EXAMPLES
#     ./bump-version.sh patch              # Bump patch version (2.0.5 → 2.0.6)
#     ./bump-version.sh minor              # Bump minor version (2.0.5 → 2.1.0)
#     ./bump-version.sh major              # Bump major version (2.0.5 → 3.0.0)
#     ./bump-version.sh --version 2.2.0    # Set specific version
#     ./bump-version.sh patch --dry-run    # Preview changes without applying

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Default values
VERSION_TYPE=""
NEW_VERSION=""
DRY_RUN=false

# Function to print colored output
print_color() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

# Function to show usage
usage() {
    echo "Usage: $0 [major|minor|patch] [--dry-run]"
    echo "       $0 --version <new-version> [--dry-run]"
    echo ""
    echo "Examples:"
    echo "  $0 patch              # Bump patch version (2.0.5 → 2.0.6)"
    echo "  $0 minor              # Bump minor version (2.0.5 → 2.1.0)"
    echo "  $0 major              # Bump major version (2.0.5 → 3.0.0)"
    echo "  $0 --version 2.2.0    # Set specific version"
    echo "  $0 patch --dry-run    # Preview changes without applying"
    exit 1
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        major|minor|patch)
            if [[ -n "$NEW_VERSION" ]]; then
                print_color $RED "Error: Cannot specify both version type and explicit version"
                usage
            fi
            VERSION_TYPE="$1"
            shift
            ;;
        --version)
            if [[ -n "$VERSION_TYPE" ]]; then
                print_color $RED "Error: Cannot specify both version type and explicit version"
                usage
            fi
            NEW_VERSION="$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        -h|--help)
            usage
            ;;
        *)
            print_color $RED "Error: Unknown option $1"
            usage
            ;;
    esac
done

# Validate arguments
if [[ -z "$VERSION_TYPE" && -z "$NEW_VERSION" ]]; then
    print_color $RED "Error: Must specify either version type (major|minor|patch) or --version <new-version>"
    usage
fi

# Find all OElite.Restme project files
PROJECT_FILES=($(find . -name "*.csproj" -path "*/OElite.Restme*" | sort))

if [[ ${#PROJECT_FILES[@]} -eq 0 ]]; then
    print_color $RED "Error: No OElite.Restme project files found!"
    exit 1
fi

print_color $CYAN "Found ${#PROJECT_FILES[@]} project files:"
for file in "${PROJECT_FILES[@]}"; do
    print_color $GRAY "  - $file"
done
echo

# Read current version from first project
FIRST_PROJECT="${PROJECT_FILES[0]}"
if [[ ! -f "$FIRST_PROJECT" ]]; then
    print_color $RED "Error: Cannot read $FIRST_PROJECT"
    exit 1
fi

# Extract current version using grep and sed
CURRENT_VERSION=$(grep -o '<VersionPrefix>[^<]*</VersionPrefix>' "$FIRST_PROJECT" | sed 's/<VersionPrefix>\(.*\)<\/VersionPrefix>/\1/')

if [[ -z "$CURRENT_VERSION" ]]; then
    print_color $RED "Error: Could not find VersionPrefix in $FIRST_PROJECT"
    exit 1
fi

print_color $YELLOW "Current version: $CURRENT_VERSION"

# Calculate new version
if [[ -n "$NEW_VERSION" ]]; then
    NEW_VERSION_STRING="$NEW_VERSION"
elif [[ -n "$VERSION_TYPE" ]]; then
    # Split version into parts
    IFS='.' read -ra VERSION_PARTS <<< "$CURRENT_VERSION"
    
    if [[ ${#VERSION_PARTS[@]} -ne 3 ]]; then
        print_color $RED "Error: Version must be in format major.minor.patch (e.g., 2.0.5)"
        exit 1
    fi
    
    MAJOR=${VERSION_PARTS[0]}
    MINOR=${VERSION_PARTS[1]}
    PATCH=${VERSION_PARTS[2]}
    
    # Validate that parts are numbers
    if ! [[ "$MAJOR" =~ ^[0-9]+$ ]] || ! [[ "$MINOR" =~ ^[0-9]+$ ]] || ! [[ "$PATCH" =~ ^[0-9]+$ ]]; then
        print_color $RED "Error: Version parts must be numbers"
        exit 1
    fi
    
    case $VERSION_TYPE in
        major)
            MAJOR=$((MAJOR + 1))
            MINOR=0
            PATCH=0
            ;;
        minor)
            MINOR=$((MINOR + 1))
            PATCH=0
            ;;
        patch)
            PATCH=$((PATCH + 1))
            ;;
    esac
    
    NEW_VERSION_STRING="$MAJOR.$MINOR.$PATCH"
fi

print_color $GREEN "New version: $NEW_VERSION_STRING"
echo

# Validate new version format
if ! [[ "$NEW_VERSION_STRING" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    print_color $RED "Error: New version must be in format major.minor.patch (e.g., 2.0.5)"
    exit 1
fi

# Dry run check
if [[ "$DRY_RUN" == true ]]; then
    print_color $CYAN "DRY RUN - Would update the following files:"
    for file in "${PROJECT_FILES[@]}"; do
        print_color $GRAY "  - $file"
    done
    print_color $CYAN "Version change: $CURRENT_VERSION → $NEW_VERSION_STRING"
    exit 0
fi

# Confirm before proceeding
print_color $YELLOW "About to update ${#PROJECT_FILES[@]} files from $CURRENT_VERSION to $NEW_VERSION_STRING"
read -p "Continue? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    print_color $YELLOW "Cancelled."
    exit 0
fi

# Update all project files
UPDATED_COUNT=0
for file in "${PROJECT_FILES[@]}"; do
    print_color $GRAY "Updating $file..."
    
    # Create backup
    cp "$file" "$file.backup"
    
    # Update VersionPrefix and AssemblyVersion
    sed -i.tmp \
        -e "s|<VersionPrefix>[^<]*</VersionPrefix>|<VersionPrefix>$NEW_VERSION_STRING</VersionPrefix>|g" \
        -e "s|<AssemblyVersion>[^<]*</AssemblyVersion>|<AssemblyVersion>$NEW_VERSION_STRING</AssemblyVersion>|g" \
        "$file"
    
    # Remove temporary file created by sed
    rm -f "$file.tmp"
    
    # Remove backup if update was successful
    rm -f "$file.backup"
    
    UPDATED_COUNT=$((UPDATED_COUNT + 1))
done

echo
print_color $GREEN "Successfully updated $UPDATED_COUNT project files!"
echo
print_color $YELLOW "Next steps:"
print_color $GRAY "  1. Review the changes: git diff"
print_color $GRAY "  2. Commit the version bump: git add . && git commit -m \"Bump version to $NEW_VERSION_STRING\""
print_color $GRAY "  3. Push to main: git push origin main"
