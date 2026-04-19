# Jellyfin Media Tags Plugin
<p align="center">
  <img src="Images/logo.png" alt="Example" width="400">
</p>

## About
LanguageTags helps handling multi-language content in a multi-lingual household, allowing administrators to deliver a per-user experience depending on the user spoken language(s).</br>
Another use case is to be able to retrieve a list of content that contains audio or subtitles in a specific language on the fly, essencialy enhancing the Jellyfin library's filter abilities.  
Currently, these are features that Jellyfin is not natively supporting and that are, otherwise, very difficult (or not at all possible) to achieve without this plugin.

This plugin is a fork of the [Jellyfin Language Tags](https://) plugin that offers the same functionality for language information.

## Details
The MediaTags plugin adds tags to the items contained in your Jellyfin personal collection based on the resolution and color range of the video tracks. This plugin will never modify the tags present in your actual media files, it will only create new tags which will be applied to the relevant "item" present in the Jellyfin's internal database. It uses Jellyfin’s MediaStreams API to read stream metadata directly (no FFmpeg), delivering a fast and reliable scan.  

## Features
- Tagging & Display
  - Configurable tag prefixes for resolution and range tags (with validation)
  - Visual reoslution selector for easier configuration
  - Tagging for non-media items (e.g., actors, studios) with toggles*
  - TV Show Tagging: optionally restrict tagging to the root Series only (skip Seasons and Episodes)

- Operations
  - Automatic scheduled scan (default: 24h)
  - Works with movies, series (seasons/episodes), and collections
  - Asynchronous mode for speed; synchronous mode for low-end devices
  - Force refresh options when files are replaced or for troubleshooting
 
- Performance & Architecture
  - Direct extraction of resolution/hdr tags from metadata

## Example Usage
Restrict content via user Parental Controls using the "Allow items with tags" rule in combination with LanguageTags:
```
res_4K
range_DV
```
This shows only items that with either 4K resolution or Dolby Vision HDR content.


### Parental Control screenshot
These are the possible Parental Control settings you could use for an Non-4K and non-HDR only user:
<p align="center"><img src="Images/parental.jpg" alt="Example" width="650"></p>

## Configuration
- Tag prefixes
  - Resolution (default): res_
  - Video range (default): range_
  - Validation ensures safe characters
- TV Show Tagging
  - Disabled (default): Episodes, Seasons, and Series all receive media tags
  - Enabled (Tag Series only): only the root Series item is tagged; media data is still read from episodes/seasons and aggregated upward. Prevents Jellyfin's tag view from being flooded with hundreds of individual episode entries.
  - Note: enabling this does not remove previously applied tags from episodes/seasons. Run "Remove ALL media tags" first for a clean state.
- Non-media tagging
  - Enable tagging for actors, studios etc. if needed
- Scan mode
  - Asynchronous (default) or synchronous for low-end devices
- Schedule
  - Configure periodic scans (default every 24h)

## Installation
Add this repository in Jellyfin: Plugins -> Catalog -> Add Repository:
```
https://raw.githubusercontent.com/Thertzlor/jellyfin-plugin-mediaTags/main/manifest.json
```

## Build (only needed for development!)
1. Clone or download the repository
2. Install the .NET SDK >= 9.0
3. Build:
```sh
dotnet publish --configuration Release
```
4. Copy the resulting output to the Jellyfin plugins folder

## What’s New

### v0.0.1.0

* Initial fork from LanguageTags.


---
## *NON-MEDIA ITEMS - Why would you want this?
Tagging non-media items (actors, directors, studios but, also, photo, photo-albums, album artists, music albums, etc.) is necessary in some cases.
If you want to use the Jellyfin "Allow" Parental Control rule, then you need to make sure that **everything** is properly tagged, otherwise Jellyfin will hide large parts of your library.

EXAMPLE:  
Say that you want a user to be able to explore only the part of the catalgue that contains 4K video or HDR content, then you might be tempted to use only the following "Allow" rules:
```
res_4K
range_HDR
```
But, by doing this, the 4K user will not be able to see the People's pages (director, actors, other cast members, etc.). Even more so, if your catalogue contains music, books or photos, these will be obscured, too. For this reason, MediaTags implements something called "non-media items" which, essentially, bulk tags everything with the tag "item" (tag can be modified) that you select in the settings page. When running the daily scheduled task, MediaTags will take care of all the newly created non-media items too. So it is a set-and-forget feature that you'll only need to setup once.

Then in the Parental Control Allow rules, make sure to add the following:
```
res_4K
range_HDR
item
```