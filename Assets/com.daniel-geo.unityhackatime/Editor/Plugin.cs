#if (UNITY_EDITOR)

using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using System.Threading.Tasks;

namespace WakaTime {
  [InitializeOnLoad]
  public class Plugin {
    public const string API_KEY_PREF = "WakaTime/APIKey";
    public const string API_URL_PREF = "WakaTime/APIUrl";
    public const string ENABLED_PREF = "WakaTime/Enabled";
    public const string DEBUG_PREF = "WakaTime/Debug";
    public const string WAKATIME_PROJECT_FILE = ".wakatime-project";

    public static string ProjectName { get; private set; }

    private static string _apiKey = "";
    private static string _apiUrl = "https://hackatime.hackclub.com/api/hackatime/v1/";
    private static bool _enabled = true;
    private static bool _debug = true;

    private const string URL_PREFIX = "https://hackatime.hackclub.com/api/hackatime/v1/";
    private const int HEARTBEAT_COOLDOWN = 5;
    private const float MIN_HEARTBEAT_INTERVAL_SECONDS = 5f;

    private static HeartbeatResponse _lastHeartbeat;
    private static float _lastHeartbeatCompletedAt;
    private static bool _heartbeatRequestInFlight;
    private static bool _cooldownActive;
    private static bool _pendingHeartbeatRequested;
    private static bool _pendingFromSave;

    static Plugin() {
      Initialize();
    }

    public static void Initialize() {
      if (EditorPrefs.HasKey(ENABLED_PREF))
        _enabled = EditorPrefs.GetBool(ENABLED_PREF);

      if (EditorPrefs.HasKey(DEBUG_PREF))
        _debug = EditorPrefs.GetBool(DEBUG_PREF);

      if (!_enabled) {
        if (_debug) Debug.Log("<HackaTime> Explicitly disabled, skipping initialization...");
        return;
      }

      if (EditorPrefs.HasKey(API_KEY_PREF)) {
        _apiKey = EditorPrefs.GetString(API_KEY_PREF);
      }

      if (EditorPrefs.HasKey(API_URL_PREF)) {
        _apiUrl = EditorPrefs.GetString(API_URL_PREF);
      }

      if (_apiKey == string.Empty) {
        Debug.LogWarning("<HackaTime> API key is not set, skipping initialization...");
        return;
      }

      if (_apiUrl == string.Empty) {
        Debug.LogWarning("<HackaTime> API URL is not set, skipping initialization...");
        return;
      }

      ProjectName = GetProjectName();

      if (_debug) Debug.Log("<HackaTime> Initializing...");

      SendHeartbeat();
      LinkCallbacks();
    }

    /// <summary>
    /// Reads .wakatime-project file
    /// <seealso cref="https://wakatime.com/faq#rename-projects"/>
    /// </summary>
    /// <returns>Lines of .wakatime-project or null if file not found</returns>
    public static string[] GetProjectFile() =>
      !File.Exists(WAKATIME_PROJECT_FILE) ? null : File.ReadAllLines(WAKATIME_PROJECT_FILE);

    /// <summary>
    /// Rewrites o creates new .wakatime-project file with given lines
    /// <seealso cref="https://wakatime.com/faq#rename-projects"/>
    /// </summary>
    /// <example>
    /// <code>
    /// project-override-name
    /// branch-override-name
    /// </code>
    /// </example>
    /// <param name="content"></param>
    public static void SetProjectFile(string[] content) {
      File.WriteAllLines(WAKATIME_PROJECT_FILE, content);
    }

    [Serializable]
    struct Response<T> {
      public string error;
      public T data;
    }

    [Serializable]
    struct HeartbeatResponse {
      public string id;
      public string entity;
      public string type;
      public float time;
    }

    struct Heartbeat {
      public string entity;
      public string type;
      public float time;
      public string project;
      public string branch;
      public string plugin;
      public string language;
      public bool is_write;
      public bool is_debugging;

      public Heartbeat(string file, bool save = false) {
        entity = file == string.Empty ? "Unsaved Scene" : file;
        type = "file";
        time = (float) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        project = ProjectName;
        plugin = "unity-hackatime";
        branch = "main";
        language = "unity";
        is_write = save;
        is_debugging = _debug;
      }
    }

    static async void SendHeartbeat(bool fromSave = false) {
      if (_debug) Debug.Log("<HackaTime> Sending heartbeat...");

      if (_heartbeatRequestInFlight || _cooldownActive) {
        _pendingHeartbeatRequested = true;
        _pendingFromSave |= fromSave;
        if (_debug) Debug.Log("<HackaTime> Queue heartbeat after cooldown");
        return;
      }

      var currentScene = EditorSceneManager.GetActiveScene().path;
      var file = currentScene != string.Empty
        ? Application.dataPath + "/" + currentScene.Substring("Assets/".Length)
        : string.Empty;

      var heartbeat = new Heartbeat(file, fromSave);

      if ((heartbeat.time - _lastHeartbeat.time < HEARTBEAT_COOLDOWN) && !fromSave &&
        (heartbeat.entity == _lastHeartbeat.entity)) {
        if (_debug) Debug.Log("<HackaTime> Skip this heartbeat");
        return;
      }

      var heartbeatJSON = JsonUtility.ToJson(heartbeat);

      var request = UnityWebRequest.Post(_apiUrl + "users/current/heartbeats?api_key=" + _apiKey, string.Empty);
      request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(heartbeatJSON));
      request.SetRequestHeader("Content-Type", "application/json");

      _heartbeatRequestInFlight = true;

      var op = request.SendWebRequest();
      var tcs = new TaskCompletionSource<bool>();
      op.completed += _ => tcs.TrySetResult(true);
      await tcs.Task;

      _lastHeartbeatCompletedAt = (float) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
      _heartbeatRequestInFlight = false;

      if (request.downloadHandler.text == string.Empty) {
        Debug.LogWarning(
          "<HackaTime> Network is unreachable. Consider disabling completely if you're working offline");
      } else {
        if (_debug)
          Debug.Log("<HackaTime> Got response\n" + request.downloadHandler.text);
        var response =
          JsonUtility.FromJson<Response<HeartbeatResponse>>(
            request.downloadHandler.text);

        if (response.error != null) {
          if (response.error == "Duplicate") {
            if (_debug) Debug.LogWarning("<HackaTime> Duplicate heartbeat");
          }
          else {
            Debug.LogError(
              "<HackaTime> Failed to send heartbeat to HackaTime!\n" +
              response.error);
          }
        }
        else {
          if (_debug) Debug.Log("<HackaTime> Sent heartbeat!");
          _lastHeartbeat = response.data;
        }
      }

      _cooldownActive = true;
      await Task.Delay(TimeSpan.FromSeconds(MIN_HEARTBEAT_INTERVAL_SECONDS));
      _cooldownActive = false;
      if (_pendingHeartbeatRequested) {
        var pf = _pendingFromSave;
        _pendingHeartbeatRequested = false;
        _pendingFromSave = false;
        SendHeartbeat(pf);
      }
    }

    [DidReloadScripts]
    static void OnScriptReload() {
      Initialize();
    }

    static void OnPlaymodeStateChanged(PlayModeStateChange change) {
      SendHeartbeat();
    }

    static void OnPropertyContextMenu(GenericMenu menu, SerializedProperty property) {
      SendHeartbeat();
    }

    static void OnHierarchyWindowChanged() {
      SendHeartbeat();
    }

    static void OnSceneSaved(Scene scene) {
      SendHeartbeat(true);
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode) {
      SendHeartbeat();
    }

    static void OnSceneClosing(Scene scene, bool removingScene) {
      SendHeartbeat();
    }

    static void OnSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode) {
      SendHeartbeat();
    }

    static void LinkCallbacks(bool clean = false) {
      if (clean) {
        EditorApplication.playModeStateChanged -= OnPlaymodeStateChanged;
        EditorApplication.contextualPropertyMenu -= OnPropertyContextMenu;
        #if UNITY_2018_1_OR_NEWER
          EditorApplication.hierarchyChanged -= OnHierarchyWindowChanged;
        #else
          EditorApplication.hierarchyWindowChanged -= OnHierarchyWindowChanged;
        #endif
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneClosing -= OnSceneClosing;
        EditorSceneManager.newSceneCreated -= OnSceneCreated;
      }

      EditorApplication.playModeStateChanged += OnPlaymodeStateChanged;
      EditorApplication.contextualPropertyMenu += OnPropertyContextMenu;
      #if UNITY_2018_1_OR_NEWER
        EditorApplication.hierarchyChanged += OnHierarchyWindowChanged;
      #else
        EditorApplication.hierarchyWindowChanged += OnHierarchyWindowChanged;
      #endif
      EditorSceneManager.sceneSaved += OnSceneSaved;
      EditorSceneManager.sceneOpened += OnSceneOpened;
      EditorSceneManager.sceneClosing += OnSceneClosing;
      EditorSceneManager.newSceneCreated += OnSceneCreated;
    }

    /// <summary>
    /// Project name for sending <see cref="Heartbeat"/>
    /// </summary>
    /// <returns><see cref="Application.productName"/> or first line of .wakatime-project</returns>
    private static string GetProjectName() =>
      File.Exists(WAKATIME_PROJECT_FILE)
        ? File.ReadAllLines(WAKATIME_PROJECT_FILE)[0]
        : Application.productName;
  }
}

#endif
