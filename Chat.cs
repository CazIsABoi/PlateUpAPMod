using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.MessageLog.Parts;
using Archipelago.MultiClient.Net.Enums;
using APColor = Archipelago.MultiClient.Net.Models.Color;

namespace KitchenPlateupAP
{
    public class ChatManager : MonoBehaviour
    {
        private static ChatManager Instance;

        private enum ChatCategory { Normal, Server, System }
        private class ChatMessageEntry
        {
            public string text;
            public ChatCategory category;
            public string sender;
            public UnityEngine.Color color;
        }

        private static readonly List<ChatMessageEntry> messages = new List<ChatMessageEntry>();
        private static readonly object messagesLock = new object();

        private static string inputBuffer = string.Empty;
        private static bool focusRequested = false;
        private static bool defocusRequested = false;

        private static float lastMessageTime = 0f;
        private const float FADE_DELAY = 5f;

        private static bool subscribedToLog = false;
        private static bool subscribedToPackets = false;
        private static string localPlayerName = null;

        private static GUIStyle styleNormal;
        private static GUIStyle styleServer;
        private static GUIStyle styleSystem;
        private static GUIStyle styleInput;
        private static GUIStyle styleButton;
        private static Texture2D backgroundTex;
        private static Texture2D inputBackgroundTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitOnLoad()
        {
            EnsureInstanceExists();
        }

        private static void EnsureInstanceExists()
        {
            if (Instance != null) return;
            GameObject obj = new GameObject("ChatManager");
            Instance = obj.AddComponent<ChatManager>();
            DontDestroyOnLoad(obj);
        }

        public static void AddSystemMessage(string text)
        {
            EnsureInstanceExists();
            AddMessage(ChatCategory.System, text, sender: null, UnityEngine.Color.white);
        }

        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            TrySubscribeLog();
            TrySubscribePackets();
        }

        void Update()
        {
            TrySubscribeLog();
            TrySubscribePackets();

            if (Input.GetKeyDown(KeyCode.T))
            {
                if (!IsInputFocused()) focusRequested = true;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsInputFocused())
                {
                    inputBuffer = string.Empty;
                    defocusRequested = true;
                }
            }
        }

        void OnGUI()
        {
            if (styleNormal == null) InitializeGUIStyles();

            float opacity = 1f;
            bool noRecentActivity = (Time.time - lastMessageTime > FADE_DELAY);
            if (noRecentActivity && !IsInputFocused()) opacity = 0.25f;

            float chatWidth = 550f;
            float chatHeight = 450f;
            float x = 10f;
            float y = Screen.height - chatHeight - 10f;
            Rect chatRect = new Rect(x, y, chatWidth, chatHeight);

            if (backgroundTex == null)
            {
                backgroundTex = new Texture2D(1, 1);
                backgroundTex.SetPixel(0, 0, UnityEngine.Color.white);
                backgroundTex.Apply();
            }

            GUI.color = new UnityEngine.Color(0f, 0f, 0f, 0.4f * opacity);
            GUI.DrawTexture(chatRect, backgroundTex);
            GUI.color = UnityEngine.Color.white;

            GUI.BeginGroup(chatRect);

            float contentX = 5f;
            float contentY = 5f;
            float contentWidth = chatWidth - 10f;
            lock (messagesLock)
            {
                int startIndex = Math.Max(0, messages.Count - 15);
                for (int i = startIndex; i < messages.Count; i++)
                {
                    ChatMessageEntry entry = messages[i];
                    GUIStyle style = styleNormal;
                    if (entry.category == ChatCategory.System) style = styleSystem;
                    if (entry.category == ChatCategory.Server) style = styleServer;

                    style.normal.textColor = new UnityEngine.Color(entry.color.r, entry.color.g, entry.color.b, opacity);

                    GUIContent msgContent = new GUIContent(entry.text);
                    float msgHeight = style.CalcHeight(msgContent, contentWidth);
                    GUI.Label(new Rect(contentX, contentY, contentWidth, msgHeight), msgContent, style);
                    contentY += msgHeight;
                }
            }

            float inputHeight = 30f;
            float inputY = chatRect.height - inputHeight - 5f;
            float inputWidth = chatWidth - 70f;
            Rect inputRect = new Rect(contentX, inputY, inputWidth, inputHeight);

            GUI.color = new UnityEngine.Color(1f, 1f, 1f, 0.4f * opacity);
            GUI.DrawTexture(inputRect, inputBackgroundTex);
            GUI.color = UnityEngine.Color.white;

            if (focusRequested)
            {
                GUI.FocusControl("ChatInputField");
                focusRequested = false;
            }
            if (defocusRequested)
            {
                GUI.FocusControl(null);
                defocusRequested = false;
            }

            GUI.SetNextControlName("ChatInputField");
            inputBuffer = GUI.TextField(inputRect, inputBuffer, styleInput);

            Rect buttonRect = new Rect(inputRect.xMax + 5f, inputY, 60f, inputHeight);
            if (GUI.Button(buttonRect, "Send", styleButton))
            {
                TrySendMessage();
            }

            GUI.EndGroup();
        }

        private void InitializeGUIStyles()
        {
            styleNormal = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
            styleSystem = new GUIStyle(styleNormal);
            styleServer = new GUIStyle(styleNormal);
            styleInput = new GUIStyle(GUI.skin.textField) { fontSize = 16, normal = { textColor = UnityEngine.Color.white } };
            styleButton = new GUIStyle(GUI.skin.button) { fontSize = 14 };

            inputBackgroundTex = new Texture2D(1, 1);
            inputBackgroundTex.SetPixel(0, 0, UnityEngine.Color.gray);
            inputBackgroundTex.Apply();
        }

        private bool IsInputFocused() => GUI.GetNameOfFocusedControl() == "ChatInputField";

        private static void AddMessage(ChatCategory category, string messageText, string sender, UnityEngine.Color color)
        {
            string timestamp = DateTime.Now.ToString("HH:mm");
            string formatted = category switch
            {
                ChatCategory.Normal => $"[{timestamp}] {sender}: {messageText}",
                ChatCategory.Server => $"[{timestamp}] [Server] {messageText}",
                _ => $"[{timestamp}] {messageText}"
            };

            lock (messagesLock)
            {
                messages.Add(new ChatMessageEntry { text = formatted, category = category, sender = sender, color = color });
                if (messages.Count > 15) messages.RemoveAt(0);
            }
            lastMessageTime = Time.time;
        }

        private void TrySendMessage()
        {
            string message = inputBuffer.Trim();
            if (message.Length > 0)
            {
                Mod.Logger.LogInfo("[ChatBox] Sending message: " + message);
                SendMessageToArchipelago(message);
                inputBuffer = string.Empty;
            }
            defocusRequested = true;
        }

        private void SendMessageToArchipelago(string text)
        {
            if (!ArchipelagoConnectionManager.ConnectionSuccessful || ArchipelagoConnectionManager.Session == null)
            {
                AddSystemMessage("Not connected to Archipelago server.");
                return;
            }

            var session = ArchipelagoConnectionManager.Session;
            localPlayerName = session.Players.GetPlayerAlias(session.ConnectionInfo.Slot);
            session.Socket.SendPacket(new SayPacket { Text = text });
        }

        private void TrySubscribeLog()
        {
            var session = ArchipelagoConnectionManager.Session;
            if (!subscribedToLog && session != null && ArchipelagoConnectionManager.ConnectionSuccessful)
            {
                session.MessageLog.OnMessageReceived += OnArchipelagoMessageReceived;
                subscribedToLog = true;
            }
        }

        private void TrySubscribePackets()
        {
            var session = ArchipelagoConnectionManager.Session;
            if (!subscribedToPackets && session != null && ArchipelagoConnectionManager.ConnectionSuccessful)
            {
                session.Socket.PacketReceived += packet => OnPacketReceived(packet);
                subscribedToPackets = true;
            }
        }

        private void OnPacketReceived(ArchipelagoPacketBase packet)
        {
            if (packet is PrintJsonPacket print)
            {
                string formatted = BuildFormattedPacketMessage(print.Data, out UnityEngine.Color color);
                AddMessage(ChatCategory.Server, formatted, sender: null, color);
            }
        }

        private void OnArchipelagoMessageReceived(LogMessage logMessage)
        {
            if (logMessage is ChatLogMessage chatMsg)
            {
                string formatted = BuildFormattedMessage(chatMsg.Parts, out UnityEngine.Color color);
                AddMessage(ChatCategory.Normal, formatted, sender: "", color);
            }
            else if (logMessage is ServerChatLogMessage serverMsg)
            {
                string formatted = BuildFormattedMessage(serverMsg.Parts, out UnityEngine.Color color);
                AddMessage(ChatCategory.Server, formatted, sender: null, color);
            }
        }

        private static string BuildFormattedMessage(IReadOnlyList<MessagePart> parts, out UnityEngine.Color finalColor)
        {
            StringBuilder sb = new StringBuilder();
            UnityEngine.Color lastColor = UnityEngine.Color.white;

            foreach (var part in parts)
            {
                sb.Append(part.Text);
                if (part.Color != default)
                {
                    APColor apColor = part.Color;
                    lastColor = new UnityEngine.Color32(apColor.R, apColor.G, apColor.B, 255);
                }
            }

            finalColor = lastColor;
            return sb.ToString();
        }

        private static string BuildFormattedPacketMessage(IReadOnlyList<JsonMessagePart> parts, out UnityEngine.Color finalColor)
        {
            StringBuilder sb = new StringBuilder();
            UnityEngine.Color lastColor = UnityEngine.Color.white;

            foreach (var part in parts)
            {
                sb.Append(part.Text);
                if (part.Color != null)
                {
                    lastColor = JsonMessagePartColorToUnityColor(part.Color.Value);
                }
            }

            finalColor = lastColor;
            return sb.ToString();
        }

        private static UnityEngine.Color JsonMessagePartColorToUnityColor(JsonMessagePartColor color)
        {
            return color switch
            {
                JsonMessagePartColor.Red => UnityEngine.Color.red,
                JsonMessagePartColor.Green => UnityEngine.Color.green,
                JsonMessagePartColor.Blue => UnityEngine.Color.blue,
                JsonMessagePartColor.Magenta => UnityEngine.Color.magenta,
                JsonMessagePartColor.Yellow => UnityEngine.Color.yellow,
                JsonMessagePartColor.Cyan => UnityEngine.Color.cyan,
                JsonMessagePartColor.White => UnityEngine.Color.white,
                _ => UnityEngine.Color.white
            };
        }
    }
}
