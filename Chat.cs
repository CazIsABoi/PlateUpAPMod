using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.MessageLog.Parts;
using Archipelago.MultiClient.Net.Models;
using UnityColor = UnityEngine.Color;

namespace KitchenPlateupAP
{
    public class ChatManager : MonoBehaviour
    {
        private static ChatManager Instance;

        private enum ChatCategory { Normal, System }
        private class ChatMessageEntry
        {
            public string text;
            public ChatCategory category;
            public UnityColor color;
        }

        private static readonly List<ChatMessageEntry> messages = new List<ChatMessageEntry>();
        private static readonly object messagesLock = new object();

        private static string inputBuffer = string.Empty;
        private static bool focusRequested = false;
        private static bool defocusRequested = false;

        private static float lastMessageTime = 0f;
        private const float FADE_DELAY = 5f;

        private static GUIStyle styleNormal;
        private static GUIStyle styleSystem;
        private static GUIStyle styleInput;
        private static GUIStyle styleButton;
        private static Texture2D backgroundTex;
        private static Texture2D inputBackgroundTex;

        private static readonly UnityColor LocalColor = UnityColor.yellow;
        private static readonly UnityColor SystemColor = new UnityColor(1f, 0.75f, 0f);

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
            AddMessage(ChatCategory.System, text, SystemColor);
        }

        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            SubscribeEvents();
        }

        void Update()
        {
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
            if (Time.time - lastMessageTime > FADE_DELAY && !IsInputFocused()) opacity = 0.25f;

            float chatWidth = 550f;
            float chatHeight = 450f;
            Rect chatRect = new Rect(10f, Screen.height - chatHeight - 10f, chatWidth, chatHeight);

            if (backgroundTex == null)
            {
                backgroundTex = new Texture2D(1, 1);
                backgroundTex.SetPixel(0, 0, UnityColor.white);
                backgroundTex.Apply();
            }

            GUI.color = new UnityColor(0f, 0f, 0f, 0.4f * opacity);
            GUI.DrawTexture(chatRect, backgroundTex);
            GUI.color = UnityColor.white;

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
                    var style = entry.category == ChatCategory.System ? styleSystem : styleNormal;
                    style.normal.textColor = new UnityColor(entry.color.r, entry.color.g, entry.color.b, opacity);

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

            GUI.color = new UnityColor(1f, 1f, 1f, 0.4f * opacity);
            GUI.DrawTexture(inputRect, inputBackgroundTex);
            GUI.color = UnityColor.white;

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
            if (GUI.Button(buttonRect, "Send", styleButton) || (Event.current.isKey && Event.current.keyCode == KeyCode.Return && IsInputFocused()))
            {
                TrySendMessage();
            }

            GUI.EndGroup();
        }

        private void InitializeGUIStyles()
        {
            styleNormal = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
            styleSystem = new GUIStyle(styleNormal);
            styleInput = new GUIStyle(GUI.skin.textField) { fontSize = 16, normal = { textColor = UnityColor.white } };
            styleButton = new GUIStyle(GUI.skin.button) { fontSize = 14 };

            inputBackgroundTex = new Texture2D(1, 1);
            inputBackgroundTex.SetPixel(0, 0, UnityColor.gray);
            inputBackgroundTex.Apply();
        }

        private bool IsInputFocused() => GUI.GetNameOfFocusedControl() == "ChatInputField";

        private static void AddMessage(ChatCategory category, string messageText, UnityColor color)
        {
            string timestamp = DateTime.Now.ToString("HH:mm");
            string formatted = $"[{timestamp}] {messageText}";

            lock (messagesLock)
            {
                messages.Add(new ChatMessageEntry { text = formatted, category = category, color = color });
                if (messages.Count > 15) messages.RemoveAt(0);
            }
            lastMessageTime = Time.time;
        }

        private void TrySendMessage()
        {
            string message = inputBuffer.Trim();
            if (message.Length > 0)
            {
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
            ArchipelagoConnectionManager.Session.Socket.SendPacket(new SayPacket { Text = text });
        }

        private void SubscribeEvents()
        {
            var session = ArchipelagoConnectionManager.Session;
            if (session != null && ArchipelagoConnectionManager.ConnectionSuccessful)
            {
                session.Socket.PacketReceived += packet =>
                {
                    if (packet is PrintJsonPacket print)
                    {
                        string formatted = BuildFormattedPacketMessage(print.Data, out UnityColor color);
                        AddMessage(ChatCategory.Normal, formatted, color);
                    }
                };
            }
        }

        private static string BuildFormattedPacketMessage(IReadOnlyList<JsonMessagePart> parts, out UnityColor finalColor)
        {
            StringBuilder sb = new StringBuilder();
            UnityColor lastColor = UnityColor.white;

            foreach (var part in parts)
            {
                sb.Append(part.Text);
                if (part.Color.HasValue)
                {
                    lastColor = part.Color.Value switch
                    {
                        JsonMessagePartColor.Red => UnityColor.red,
                        JsonMessagePartColor.Green => UnityColor.green,
                        JsonMessagePartColor.Blue => UnityColor.blue,
                        JsonMessagePartColor.Magenta => UnityColor.magenta,
                        JsonMessagePartColor.Yellow => UnityColor.yellow,
                        JsonMessagePartColor.Cyan => UnityColor.cyan,
                        _ => UnityColor.white
                    };
                }
            }
            finalColor = lastColor;
            return sb.ToString();
        }
    }
}
