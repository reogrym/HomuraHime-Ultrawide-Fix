# Homura Hime Ultrawide Fix

A BepInEx 6 plugin that enables native ultrawide support for Homura Hime. No plans to update this any further at this time.

## Features
* **Tested on the Steam version of the game on a 21:9 monitor running the game at 3440x1440.**
* **Auto-Resolution**: Automatically detects and applies your monitor's native resolution.
* **UI Scaling and Pillarboxing**: Prevents character portraits from stretching, UI should be pillarboxed to 16:9 for the most part.
* **Resolution Override**: Includes a config file to manually set custom resolutions.

## Installation
1. Extract the latest version of [BepInEx 6](https://builds.bepinex.dev/projects/bepinex_be) into the game's root directory where the executable is located `ie. (...steamapps\common\Homura Hime\)`
2. Download the latest release of this mod.
3. Extract the `BepInEx` folder from the zip into your game's root directory where the executable is located `(ie. ...steamapps\common\Homura Hime\)`
4. Alternatively, download the latest release of the mod that contains BepInEx (as denoted by mod version and `-bepinex-included` in the release name) and extract into the game's root directory where the executable is located `(ie. ...steamapps\common\Homura Hime\)`
5. Run the game!

## Configuration
After the first launch, a config file will be generated at `...steamapps\common\Homura Hime\BepInEx\config\himeuw.cfg`. You can edit this file to disable Auto-Detect and set a manual resolution.

## Known Issues
* Results screen is anchored to the left.
