# unity-hackatime

A [Hackatime](https://hackatime.hackclub.com/) plugin for [Unity](https://unity.com).

![image](https://github.com/user-attachments/assets/80f56962-f006-4f65-a48f-bfafec4f4d13)


## About

Existing solutions didn't work for me (https://github.com/vladfaust/unity-wakatime, and other solutions lack an option for sending heartbeats to different API URLs, such as to Hackatime), so I decided to fork vladfaust's solution to support Hackatime, Wakatime and others using the API URL.

## Installation using the Unity Package Manager (Unity 2018.1+)

The [Unity Package Manager](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@1.8/manual/index.html) (UPM) keeps package contents separate from your main project files.

1. In the Unity Package Manager, click on the plus sign in the top left corner
2. Select "Install package from git URL..."
3. Enter the following line:
   ```
    https://github.com/daniel-geo/unity-hackatime.git#package
    ```
4. Click on the install button

![image](https://github.com/user-attachments/assets/73ef6434-1164-40fe-8c33-c8365f426382)





## Installation using the Unity Package Manager manifest.json (Unity 2018.1+)

1. Modify your project's `Packages/manifest.json` file by adding this line:

    ```json
    "com.daniel-geo.unityhackatime": "https://github.com/daniel-geo/unity-hackatime.git#package"
    ```

    Make sure it's a valid JSON file. For example:

    ```json
    {
        "dependencies": {
            "com.unity.ads": "2.0.8",
            "com.daniel-geo.unityhackatime": "https://github.com/daniel-geo/unity-hackatime.git#package"
        }
    }
    ```

## Installation (all other Unity versions)

If you don't use the Unity Package Manager, you may copy the `Editor` folder from inside `Assets/com.daniel-geo.unityhackatime` into your project's `Assets` folder.

## Setup

1. Run the Unity editor, go to `Window/HackaTime`, and insert your API key (grab one from https://hackatime.hackclub.com/my/wakatime_setup)
2. Press `Save Preferences`
3. Enjoy!

## Usage

The plugin will automatically send heartbeats to HackaTime after the following events:

* DidReloadScripts
* EditorApplication.playModeStateChanged
* EditorApplication.contextualPropertyMenu
* EditorApplication.hierarchyWindowChanged
* EditorSceneManager.sceneSaved
* EditorSceneManager.sceneOpened
* EditorSceneManager.sceneClosing
* EditorSceneManager.newSceneCreated

## Credits
__This plugin wouldn’t exist without [@taciturnaxolotl](https://github.com/taciturnaxolotl)__  
__A huge thanks to [@unsaltedkale](https://github.com/unsaltedkale) for her valuable help in testing the project__
