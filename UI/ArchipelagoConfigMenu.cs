using System;
using UnityEngine;

namespace KitchenPlateupAP
{
    /// <summary>
    /// Lightweight in-game editor for the Archipelago connection details. It is opened
    /// from PreferenceSystem so users never need to manually edit the JSON file.
    /// </summary>
    public sealed class ArchipelagoConfigMenu : MonoBehaviour
    {
        private const string AddressControl = "PlateupAP.ServerAddress";
        private const string PortControl = "PlateupAP.ServerPort";
        private const string PlayerControl = "PlateupAP.PlayerName";
        private const string PasswordControl = "PlateupAP.Password";

        private string _address;
        private string _port;
        private string _playerName;
        private string _password;
        private string _status;
        private GUIStyle _titleStyle;
        private GUIStyle _statusStyle;
        private Texture2D _backgroundTexture;

        public static void Show()
        {
            var existing = FindObjectOfType<ArchipelagoConfigMenu>();
            if (existing != null)
            {
                existing.enabled = true;
                existing.LoadSavedValues();
                return;
            }

            var menuObject = new GameObject("PlateupAP Archipelago Configuration");
            DontDestroyOnLoad(menuObject);
            menuObject.AddComponent<ArchipelagoConfigMenu>();
        }

        private void Awake()
        {
            LoadSavedValues();
        }

        private void LoadSavedValues()
        {
            PlateupAPConfig config = Mod.LoadArchipelagoConfig();
            _address = config.address ?? "";
            _port = config.port.ToString();
            _playerName = config.playername ?? "";
            _password = config.password ?? "";
            _status = "";
        }

        private void OnGUI()
        {
            EnsureStyles();

            float width = Mathf.Min(620f, Screen.width - 32f);
            float height = 390f;
            Rect window = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

            GUI.ModalWindow(GetInstanceID(), window, DrawWindow, "Archipelago Server Configuration");
        }

        private void DrawWindow(int windowId)
        {
            const float margin = 24f;
            const float labelWidth = 140f;
            const float fieldHeight = 30f;
            float fieldWidth = 390f;
            float y = 50f;

            // PreferenceSystem's window skin is deliberately translucent. Draw an
            // opaque panel first so this editor remains readable over its menu.
            GUI.DrawTexture(new Rect(1f, 1f, Screen.width, Screen.height), _backgroundTexture);
            GUI.Label(new Rect(margin, 16f, 560f, 26f), "Enter your server details, then save or connect.", _titleStyle);
            DrawField("Server address", AddressControl, ref _address, y, labelWidth, fieldWidth, fieldHeight);
            y += 46f;
            DrawField("Port", PortControl, ref _port, y, labelWidth, fieldWidth, fieldHeight);
            y += 46f;
            DrawField("Player name", PlayerControl, ref _playerName, y, labelWidth, fieldWidth, fieldHeight);
            y += 46f;

            GUI.Label(new Rect(margin, y + 6f, labelWidth, fieldHeight), "Password");
            GUI.SetNextControlName(PasswordControl);
            _password = GUI.PasswordField(new Rect(margin + labelWidth, y, fieldWidth, fieldHeight), _password, '*');
            y += 54f;

            if (GUI.Button(new Rect(margin, y, 120f, 34f), "Save"))
                Save(connect: false);
            if (GUI.Button(new Rect(margin + 132f, y, 120f, 34f), "Save & Connect"))
                Save(connect: true);
            if (GUI.Button(new Rect(width: 100f, x: fieldWidth + labelWidth - 80f, y: y, height: 34f), "Cancel"))
                Destroy(gameObject);

            if (!string.IsNullOrEmpty(_status))
                GUI.Label(new Rect(margin, y + 45f, fieldWidth + labelWidth, 48f), _status, _statusStyle);

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Destroy(gameObject);
                Event.current.Use();
            }
        }

        private static void DrawField(string label, string controlName, ref string value, float y, float labelWidth, float fieldWidth, float fieldHeight)
        {
            const float margin = 24f;
            GUI.Label(new Rect(margin, y + 6f, labelWidth, fieldHeight), label);
            GUI.SetNextControlName(controlName);
            value = GUI.TextField(new Rect(margin + labelWidth, y, fieldWidth, fieldHeight), value ?? string.Empty);
        }

        private void Save(bool connect)
        {
            if (!int.TryParse(_port, out int port))
            {
                _status = "Port must be a whole number between 1 and 65535.";
                return;
            }

            var config = new PlateupAPConfig
            {
                address = (_address ?? "").Trim(),
                port = port,
                playername = (_playerName ?? "").Trim(),
                password = _password ?? ""
            };

            if (!Mod.SaveArchipelagoConfig(config, out string error))
            {
                _status = error;
                return;
            }

            if (!connect)
            {
                _status = "Saved. Choose Save & Connect whenever you are ready.";
                return;
            }

            if (string.IsNullOrWhiteSpace(config.playername))
            {
                _status = "Player name is required to connect.";
                return;
            }

            _status = "Saved. Connecting to Archipelago…";
            Mod.Instance?.UpdateArchipelagoConfig(config);
        }

        private void EnsureStyles()
        {
            if (_backgroundTexture == null)
            {
                _backgroundTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _backgroundTexture.SetPixel(0, 0, new Color(0.045f, 0.055f, 0.075f, 1f));
                _backgroundTexture.Apply();
            }

            if (_titleStyle == null)
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
            if (_statusStyle == null)
                _statusStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    normal = { textColor = new Color(0.75f, 0.9f, 1f, 1f) }
                };
        }
    }
}
