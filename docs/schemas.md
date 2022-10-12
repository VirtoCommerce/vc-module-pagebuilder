# Schemas

## Overview
TODO:

## Theme Structure
Page Builder reads meta-data from theme. Developers can extend or customize page builder behaviour by theme repository.

```text
├── client-app                    // The main folder for the application.
|   ├── shared                    // Assets needed to be precompiled during building.
|   |   └── static-content
|   |      └── components         // Vue Components for rendering Page Builder elements. 
├── config                        
|   |   └── schemas               // All Page Builder meta information are stored here.
|   |      ├── blocks             // Block definitions.
|   |      |   └──...
|   |      ├── objects            // Object definitions.
|   |      |   └──...
|   |      ├── sections           // Sections definitions.
|   |      |   └──...
|   |      ├── shared             // Global settings folder.
|   |      |   └── _blocks.json   // Global settings for blocks.
|   |      |   └── _sections.json // Global settings for sections.
|   |      └── templates          
|   |          └── page.json      // Page Builder configuration for static pages.
|   └── settings_schema.json      // Theme settings schema file. Page Builder uses it for Theme and Preset editor.
|   └── settings_data.json        // Theme config file.
```

## Type of Json Files
TODO:

### Blocks
TODO:

### Objects
TODO:

### Sections
TODO:

### Shared
TODO:

### Templates
TODO:

## Elements
TODO:

## Block Samples