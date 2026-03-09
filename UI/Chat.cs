using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.MessageLog.Parts;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Kitchen;
using KitchenPlateupAP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Entities;
using UnityEngine;
using APLogMessage = Archipelago.MultiClient.Net.MessageLog.Messages.LogMessage;
using UnityColor = UnityEngine.Color;

namespace KitchenPlateupAP
{
    public class ChatManager : MonoBehaviour
    {
        private static ChatManager Instance;

        private enum ChatCategory { Normal, System }

        private struct Segment
        {
            public string Text;
            public UnityColor? Color; // null => inherit
        }

        private class ChatMessageEntry
        {
            public ChatCategory category;
            public string PlainText;
            public UnityColor PlainColor;
            public List<Segment> Segments;
        }

        private struct LeaseStatus
        {
            public bool IsPrepPhase;
            public int CurrentDay;
            public int LeaseInterval;
            public int Owned;
            public int Required;
            public int DaysUntilNextRequirement;
            public bool IsGateActive;

            public bool HasFutureRequirement => DaysUntilNextRequirement >= 0;
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

        // Color definitions
        private static readonly UnityColor TurquoisePlayerColor = new UnityColor(0f, 0.8f, 0.8f); // local player name
        private static readonly UnityColor FillerItemColor = UnityColor.green; // None
        private static readonly UnityColor UsefulItemColor = UnityColor.blue; // NeverExclude
        private static readonly UnityColor ProgressionItemColor = new UnityColor(1f, 0.84f, 0f); // Advancement (gold)
        private static readonly UnityColor SystemColor = new UnityColor(1f, 0.75f, 0f);

        // Footer styles/textures
        private static GUIStyle footerStyle;
        private static Texture2D footerBgTex;
        private static GUIStyle leaseBadgeStyle;
        private static GUIStyle rerollBadgeStyle;

        // Colors reused
        private static readonly UnityColor FooterBg = new UnityColor(0f, 0f, 0f, 0.85f);
        private static readonly UnityColor FooterTitle = new UnityColor(1f, 0.85f, 0.2f, 1f);
        private static readonly UnityColor FooterGreen = new UnityColor(0.1f, 0.85f, 0.1f, 1f);
        private static readonly UnityColor FooterRed = new UnityColor(0.9f, 0.2f, 0.2f, 1f);
        private static Vector2 dishesScrollPos = Vector2.zero;

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
            InitializeFooterStyles();

            Rect footerRect = GetGlobalFooterRect();
            bool footerInteracting = IsFooterInteracting(footerRect);

            float opacity = 1f;
            if (Time.time - lastMessageTime > FADE_DELAY && !IsInputFocused() && !footerInteracting)
            {
                opacity = 0.25f;
            }

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

            float inputHeight = 30f;
            float inputY = chatRect.height - inputHeight - 5f;
            float inputWidth = chatWidth - 70f;
            Rect inputRect = new Rect(contentX, inputY, inputWidth, inputHeight);
            bool inputHovered = inputRect.Contains(Event.current.mousePosition);

            // Helper to build display text with color tags
            string BuildDisplayText(ChatMessageEntry entry)
            {
                if (entry.Segments != null)
                {
                    var sbLocal = new StringBuilder();
                    foreach (var seg in entry.Segments)
                    {
                        if (seg.Color.HasValue)
                        {
                            UnityColor c = seg.Color.Value;
                            string hex = ColorUtility.ToHtmlStringRGBA(new UnityColor(c.r, c.g, c.b, opacity));
                            sbLocal.Append("<color=#").Append(hex).Append(">").Append(seg.Text).Append("</color>");
                        }
                        else
                        {
                            sbLocal.Append(seg.Text);
                        }
                    }
                    return sbLocal.ToString();
                }
                else
                {
                    UnityColor plain = entry.PlainColor;
                    string hex = ColorUtility.ToHtmlStringRGBA(new UnityColor(plain.r, plain.g, plain.b, opacity));
                    return "<color=#" + hex + ">" + entry.PlainText + "</color>";
                }
            }

            // Compute which recent messages fit above the input box
            lock (messagesLock)
            {
                float availableHeight = inputY - contentY - 5f; // reserve spacing above input
                var indices = new List<int>();
                float used = 0f;
                for (int i = messages.Count - 1; i >= 0; i--)
                {
                    ChatMessageEntry e = messages[i];
                    var style = e.category == ChatCategory.System ? styleSystem : styleNormal;
                    string text = BuildDisplayText(e);
                    float h = style.CalcHeight(new GUIContent(text), contentWidth);
                    if (indices.Count == 0 || used + h <= availableHeight)
                    {
                        indices.Add(i);
                        used += h;
                    }
                    else
                    {
                        break;
                    }
                }
                if (indices.Count == 0 && messages.Count > 0)
                {
                    // Ensure we at least show the most recent line
                    indices.Add(messages.Count - 1);
                }
                indices.Reverse();

                foreach (int idx in indices)
                {
                    ChatMessageEntry entry = messages[idx];
                    var style = entry.category == ChatCategory.System ? styleSystem : styleNormal;
                    style.normal.textColor = new UnityColor(1f, 1f, 1f, opacity);

                    string displayText = BuildDisplayText(entry);
                    GUIContent msgContent = new GUIContent(displayText);
                    float msgHeight = style.CalcHeight(msgContent, contentWidth);
                    GUI.Label(new Rect(contentX, contentY, contentWidth, msgHeight), msgContent, style);
                    contentY += msgHeight;
                }
            }

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

            if (inputHovered && !IsInputFocused() && Event.current.type == UnityEngine.EventType.Repaint)
            {
                GUI.FocusControl("ChatInputField");
            }

            GUI.SetNextControlName("ChatInputField");
            inputBuffer = GUI.TextField(inputRect, inputBuffer, styleInput);

            Rect buttonRect = new Rect(inputRect.xMax + 5f, inputY, 60f, inputHeight);
            if (GUI.Button(buttonRect, "Send", styleButton) || Event.current.isKey && Event.current.keyCode == KeyCode.Return && IsInputFocused())
            {
                TrySendMessage();
            }

            GUI.EndGroup();

            if (!IsInKitchen())
            {
                DrawGlobalFooterHUD(opacity, footerRect);
            }

            // Only show the lease tracker when day leases are enabled
            if (Mod.DayLeasesEnabled)
            {
                DrawLeaseCountdownBadge(opacity);
            }

            DrawRerollCostBadge(opacity);
        }

        private void InitializeGUIStyles()
        {
            styleNormal = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
            styleNormal.richText = true;
            styleSystem = new GUIStyle(styleNormal);
            styleInput = new GUIStyle(GUI.skin.textField) { fontSize = 16, normal = { textColor = UnityColor.white } };
            styleButton = new GUIStyle(GUI.skin.button) { fontSize = 14 };

            inputBackgroundTex = new Texture2D(1, 1);
            inputBackgroundTex.SetPixel(0, 0, UnityColor.gray);
            inputBackgroundTex.Apply();
        }

        private void InitializeFooterStyles()
        {
            if (footerStyle == null)
            {
                footerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    richText = true,
                    wordWrap = true,
                    normal = { textColor = UnityColor.white }
                };
            }
            if (footerBgTex == null)
            {
                footerBgTex = new Texture2D(1, 1);
                footerBgTex.SetPixel(0, 0, UnityColor.white);
                footerBgTex.Apply();
            }
        }

        private bool IsInputFocused() => GUI.GetNameOfFocusedControl() == "ChatInputField";

        private static void AddMessage(ChatCategory category, string messageText, UnityColor color)
        {
            string timestamp = DateTime.Now.ToString("HH:mm");
            string formatted = $"[{timestamp}] {messageText}";

            lock (messagesLock)
            {
                messages.Add(new ChatMessageEntry { PlainText = formatted, PlainColor = color, category = category });
                if (messages.Count > 15) messages.RemoveAt(0);
            }
            lastMessageTime = Time.time;
        }

        private static void AddRichMessage(ChatCategory category, List<Segment> segments)
        {
            // Prepend timestamp segment
            string timestamp = DateTime.Now.ToString("HH:mm");
            var rich = new List<Segment> { new Segment { Text = $"[{timestamp}] ", Color = null } };
            rich.AddRange(segments);
            lock (messagesLock)
            {
                messages.Add(new ChatMessageEntry { Segments = rich, category = category });
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
                try
                {
                    session.MessageLog.OnMessageReceived += OnStructuredLogMessage;
                }
                catch { }
            }
        }
        private void OnStructuredLogMessage(APLogMessage msg)
        {
            try
            {
                var session = ArchipelagoConnectionManager.Session;
                if (session == null) return;
                var segments = new List<Segment>();

                foreach (var part in msg.Parts)
                {
                    if (part is PlayerMessagePart playerPart)
                    {
                        UnityColor? color = playerPart.IsActivePlayer ? TurquoisePlayerColor : (UnityColor?)null;
                        segments.Add(new Segment { Text = playerPart.Text, Color = color });
                    }
                    else if (part is ItemMessagePart itemPart)
                    {
                        string itemName = session.Items.GetItemName(itemPart.ItemId) ?? itemPart.Text;
                        UnityColor color = ClassifyItemColor(itemPart.Flags);
                        segments.Add(new Segment { Text = itemName, Color = color });
                    }
                    else
                    {
                        segments.Add(new Segment { Text = part.Text, Color = null });
                    }
                }

                AddRichMessage(ChatCategory.Normal, segments);
            }
            catch (Exception ex)
            {
                AddSystemMessage("Message parse error: " + ex.Message);
            }
        }

        private static UnityColor ClassifyItemColor(ItemFlags flags)
        {
            if ((flags & ItemFlags.Advancement) != 0) return ProgressionItemColor;
            if ((flags & ItemFlags.NeverExclude) != 0) return UsefulItemColor;
            // Treat traps as filler unless special handling desired
            return FillerItemColor;
        }

        private static string BuildFormattedPacketMessage(IReadOnlyList<JsonMessagePart> parts, out UnityColor finalColor)
        {
            // Legacy path (raw PrintJsonPacket). We still resolve ItemId / PlayerId when possible.
            var session = ArchipelagoConnectionManager.Session;
            StringBuilder sb = new StringBuilder();
            UnityColor lastColor = UnityColor.white;

            foreach (var part in parts)
            {
                string text = part.Text;
                try
                {
                    if (session != null)
                    {
                        switch (part.Type)
                        {
                            case JsonMessagePartType.ItemId:
                                // Convert item id to name
                                if (long.TryParse(part.Text, out long itemId))
                                {
                                    string name = session.Items.GetItemName(itemId);
                                    if (!string.IsNullOrEmpty(name)) text = name;
                                }
                                break;
                            case JsonMessagePartType.PlayerId:
                                if (int.TryParse(part.Text, out int slot))
                                {
                                    string alias = session.Players.GetPlayerAlias(slot) ?? part.Text;
                                    text = alias;
                                }
                                break;
                            default: break;
                        }
                    }
                }
                catch { }

                sb.Append(text);
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

        // Bottom-left footer inside the chat group area
        private void DrawFooterHUD(float opacity, float contentX, Rect chatRect)
        {
            float footerWidth = 360f;
            float footerPadding = 8f;
            float footerY = chatRect.height - 75f; // sits above input field
            Rect footerRect = new Rect(contentX, footerY, footerWidth, 70f);

            // Background
            GUI.color = new UnityColor(FooterBg.r, FooterBg.g, FooterBg.b, FooterBg.a * opacity);
            GUI.DrawTexture(footerRect, footerBgTex);
            GUI.color = UnityColor.white;

            // Build lines
            string titleHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(FooterTitle.r, FooterTitle.g, FooterTitle.b, opacity));
            string greenHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(FooterGreen.r, FooterGreen.g, FooterGreen.b, opacity));
            string redHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(FooterRed.r, FooterRed.g, FooterRed.b, opacity));

            string dishName = ResolveFirstDishName();
            bool? unlocked = IsDishUnlockedLocal(dishName);
            string dishColor = unlocked == false ? redHex : greenHex;

            string line1 = $"<b><color=#{titleHex}>First dish:</color></b> <color=#{dishColor}>{dishName}</color>";

            // Lease info only during prep when gated
            string leaseLine = BuildLeaseLine(opacity);
            string combined = leaseLine == null ? line1 : line1 + "\n" + leaseLine;

            GUI.Label(new Rect(footerRect.x + footerPadding, footerRect.y + 6f, footerRect.width - 2 * footerPadding, footerRect.height - 12f), combined, footerStyle);
        }

        private void DrawGlobalFooterHUD(float opacity, Rect footerRect)
        {
            float padding = 8f;
            float lineHeight = 22f;

            if (footerBgTex == null) InitializeFooterStyles();
            GUI.color = new UnityColor(FooterBg.r, FooterBg.g, FooterBg.b, FooterBg.a * opacity);
            GUI.DrawTexture(footerRect, footerBgTex);
            GUI.color = UnityColor.white;

            string titleHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(FooterTitle.r, FooterTitle.g, FooterTitle.b, opacity));
            string greenHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(FooterGreen.r, FooterGreen.g, FooterGreen.b, opacity));
            string redHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(FooterRed.r, FooterRed.g, FooterRed.b, opacity));

            string dishName = ResolveFirstDishName();
            bool? unlocked = IsDishUnlockedLocal(dishName);
            bool firstUnlocked = unlocked ?? !LockedDishes.IsLockingEnabled();
            string dishColor = firstUnlocked ? greenHex : redHex;

            string line1 = $"<b><color=#{titleHex}>First dish:</color></b> <color=#{dishColor}>{dishName}</color>";

            List<(string Name, bool? Unlocked)> dishStatuses = GetDishStatuses(dishName);
            string leaseLine = Mod.DayLeasesEnabled ? BuildLeaseLine(opacity) : null;

            float currentY = footerRect.y + padding;
            if (footerStyle == null) InitializeFooterStyles();

            // First dish line
            GUI.Label(new Rect(footerRect.x + padding, currentY, footerRect.width - 2f * padding, lineHeight), line1, footerStyle);
            currentY += lineHeight + 4f;

            // Lease line (drawn right after first dish if present, only when enabled)
            if (!string.IsNullOrEmpty(leaseLine))
            {
                GUI.Label(new Rect(footerRect.x + padding, currentY, footerRect.width - 2f * padding, lineHeight + 4f), leaseLine, footerStyle);
                currentY += lineHeight + 6f;
            }

            // Other dishes header + scroll list fills the rest
            if (dishStatuses.Count > 0)
            {
                string header = $"<b><color=#{titleHex}>Other dishes:</color></b>";
                GUI.Label(new Rect(footerRect.x + padding, currentY, footerRect.width - 2f * padding, lineHeight), header, footerStyle);
                currentY += lineHeight + 2f;

                float listHeight = Mathf.Max(40f, footerRect.yMax - currentY - padding);
                Rect scrollRect = new Rect(footerRect.x + padding, currentY, footerRect.width - 2f * padding, listHeight);
                float contentHeight = dishStatuses.Count * lineHeight;

                dishesScrollPos = GUI.BeginScrollView(scrollRect, dishesScrollPos, new Rect(0f, 0f, scrollRect.width - 16f, contentHeight));
                float itemY = 0f;
                foreach (var (name, unlockedStatus) in dishStatuses)
                {
                    bool isUnlocked = unlockedStatus ?? (!LockedDishes.IsLockingEnabled());
                    string colorHex = isUnlocked ? greenHex : redHex;
                    string dishLine = $"<color=#{colorHex}>{name}</color>";
                    GUI.Label(new Rect(0f, itemY, scrollRect.width - 20f, lineHeight), dishLine, footerStyle);
                    itemY += lineHeight;
                }
                GUI.EndScrollView();
            }
        }

        private string ResolveFirstDishName()
        {
            try
            {
                string persistedPath = Path.Combine(Application.persistentDataPath, "last_selected_dishes.txt");
                if (File.Exists(persistedPath))
                {
                    foreach (string line in File.ReadAllLines(persistedPath))
                    {
                        string candidate = line?.Trim();
                        if (!string.IsNullOrEmpty(candidate))
                        {
                            return candidate;
                        }
                    }
                }

                if (LockedDishes.IsLockingEnabled())
                {
                    int firstUnlocked = LockedDishes.GetAvailableDishes()?.FirstOrDefault() ?? 0;
                    if (firstUnlocked != 0 && ProgressionMapping.dishDictionary.TryGetValue(firstUnlocked, out string resolved))
                    {
                        return resolved;
                    }
                }
            }
            catch
            {
                // Ignore and fall through to default.
            }

            return "Unknown";
        }

        private List<(string Name, bool? Unlocked)> GetDishStatuses(string firstDishName)
        {
            var result = new List<(string Name, bool? Unlocked)>();
            try
            {
                HashSet<int> unlocked = null;
                if (LockedDishes.IsLockingEnabled())
                {
                    unlocked = new HashSet<int>(LockedDishes.GetAvailableDishes() ?? Enumerable.Empty<int>());
                }

                foreach (var pair in ProgressionMapping.dishDictionary)
                {
                    string name = pair.Value;
                    if (!string.IsNullOrEmpty(firstDishName) &&
                        string.Equals(name, firstDishName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool? isUnlocked = unlocked != null ? unlocked.Contains(pair.Key) : (bool?)null;
                    result.Add((name, isUnlocked));
                }

                result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                // Keep whatever we already accumulated.
            }

            return result;
        }

        private bool? IsDishUnlockedLocal(string dishName)
        {
            if (!LockedDishes.IsLockingEnabled() || string.IsNullOrWhiteSpace(dishName))
            {
                return null;
            }

            int dishId = 0;
            foreach (var pair in ProgressionMapping.dishDictionary)
            {
                if (string.Equals(pair.Value, dishName, StringComparison.OrdinalIgnoreCase))
                {
                    dishId = pair.Key;
                    break;
                }
            }

            if (dishId == 0)
            {
                return null;
            }

            return LockedDishes.GetAvailableDishes()?.Contains(dishId) ?? false;
        }

        private string BuildLeaseLine(float opacity)
        {
            if (!TryGetLeaseStatus(out var lease) || !lease.IsGateActive)
            {
                return null;
            }

            string titleHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(FooterTitle.r, FooterTitle.g, FooterTitle.b, opacity));
            string haveHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(1f, 1f, 1f, opacity));
            string needHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(FooterRed.r, FooterRed.g, FooterRed.b, opacity));

            return $"<b><color=#{titleHex}>Day Lease required</color></b>  Day {lease.CurrentDay}: have <color=#{haveHex}>{lease.Owned}</color> / need <color=#{needHex}>{lease.Required}</color>";
        }

        private void DrawLeaseCountdownBadge(float opacity)
        {
            if (!TryGetLeaseStatus(out var lease) || !lease.IsPrepPhase)
            {
                return;
            }

            InitializeFooterStyles();
            InitializeLeaseBadgeVisuals();

            float width = 320f;
            float height = 68f;
            float marginRight = 35f;
            float top = Mathf.Max(140f, Screen.height * 0.15f);
            Rect rect = new Rect(Screen.width - width - marginRight, top, width, height);
            float padding = 10f;

            GUI.color = new UnityColor(FooterBg.r, FooterBg.g, FooterBg.b, FooterBg.a * opacity);
            GUI.DrawTexture(rect, footerBgTex);
            GUI.color = UnityColor.white;

            string titleHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(FooterTitle.r, FooterTitle.g, FooterTitle.b, opacity));
            string greenHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(FooterGreen.r, FooterGreen.g, FooterGreen.b, opacity));
            string redHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(FooterRed.r, FooterRed.g, FooterRed.b, opacity));
            string haveHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(1f, 1f, 1f, opacity));
            string needHex = lease.IsGateActive ? redHex : greenHex;

            string line1;
            if (lease.IsGateActive)
            {
                line1 = $"<b><color=#{titleHex}>Day Lease</color></b> – <color=#{redHex}>Lease required now!</color>";
            }
            else if (!lease.HasFutureRequirement)
            {
                line1 = $"<b><color=#{titleHex}>Day Lease</color></b> – <color=#{greenHex}>All leases covered</color>";
            }
            else
            {
                int days = lease.DaysUntilNextRequirement;
                string plural = days == 1 ? string.Empty : "s";
                string dayText = days == 0
                    ? "Lease needed after today"
                    : $"{days} day{plural} until lease is needed";
                line1 = $"<b><color=#{titleHex}>Day Lease</color></b> – {dayText}";
            }

            string line2 = $"Have <color=#{haveHex}>{lease.Owned}</color> / Need <color=#{needHex}>{lease.Required}</color>";
            Rect textRect = new Rect(rect.x + padding, rect.y + 6f, rect.width - 2f * padding, rect.height - 12f);
            GUI.Label(textRect, line1 + "\n" + line2, leaseBadgeStyle);
        }

        /// <summary>
        /// Draws a small HUD badge (top-right, below the lease badge) showing the
        /// current blueprint reroll cost, so the player can see which checks are pending.
        /// Only visible during prep phase (night time) while in a kitchen.
        /// </summary>
        private void DrawRerollCostBadge(float opacity)
        {
            if (!TryGetRerollCost(out int rerollCost))
                return;

            InitializeFooterStyles();

            if (rerollBadgeStyle == null)
            {
                rerollBadgeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    richText = true,
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = UnityColor.white }
                };
            }

            float width = 320f;
            float height = 46f;
            float marginRight = 35f;

            // Position below the lease badge (which sits at ~15% from top) when leases are shown,
            // otherwise take the lease badge's slot directly.
            float leaseBlockHeight = Mod.DayLeasesEnabled ? 68f + 8f : 0f;
            float top = Mathf.Max(140f, Screen.height * 0.15f) + leaseBlockHeight;

            Rect rect = new Rect(Screen.width - width - marginRight, top, width, height);
            float padding = 10f;

            GUI.color = new UnityColor(FooterBg.r, FooterBg.g, FooterBg.b, FooterBg.a * opacity);
            GUI.DrawTexture(rect, footerBgTex);
            GUI.color = UnityColor.white;

            string titleHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(FooterTitle.r, FooterTitle.g, FooterTitle.b, opacity));
            string valueHex = ColorUtility.ToHtmlStringRGBA(new UnityColor(1f, 1f, 1f, opacity));

            string label = $"<b><color=#{titleHex}>Reroll Cost:</color></b> <color=#{valueHex}>{rerollCost}g</color>";
            Rect textRect = new Rect(rect.x + padding, rect.y + 8f, rect.width - 2f * padding, rect.height - 16f);
            GUI.Label(textRect, label, rerollBadgeStyle);
        }

        /// <summary>
        /// Reads <see cref="SRerollCost"/> from the ECS world.
        /// Returns false when not in a kitchen or the singleton does not exist.
        /// </summary>
        private static bool TryGetRerollCost(out int cost)
        {
            cost = 0;
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return false;

                var em = world.EntityManager;

                bool inKitchen = em.CreateEntityQuery(typeof(SKitchenMarker)).CalculateEntityCount() > 0;
                if (!inKitchen) return false;

                bool isPrep = em.CreateEntityQuery(typeof(SIsNightTime)).CalculateEntityCount() > 0;
                if (!isPrep) return false;

                var query = em.CreateEntityQuery(typeof(SRerollCost));
                if (query.CalculateEntityCount() == 0) return false;

                cost = query.GetSingleton<SRerollCost>().Cost;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void InitializeLeaseBadgeVisuals()
        {
            if (leaseBadgeStyle == null)
            {
                leaseBadgeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    richText = true,
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = UnityColor.white }
                };
            }
        }

        private bool TryGetLeaseStatus(out LeaseStatus status)
        {
            status = default;
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null)
                {
                    return false;
                }

                var em = world.EntityManager;
                bool inKitchen = em.CreateEntityQuery(typeof(SKitchenMarker)).CalculateEntityCount() > 0;
                if (!inKitchen)
                {
                    return false;
                }

                bool isPrep = em.CreateEntityQuery(typeof(SIsNightTime)).CalculateEntityCount() > 0;
                if (!isPrep)
                {
                    return false;
                }

                var dayQuery = em.CreateEntityQuery(typeof(SDay));
                if (dayQuery.CalculateEntityCount() == 0)
                {
                    return false;
                }

                int currentDay = dayQuery.GetSingleton<SDay>().Day;
                if (currentDay < 1)
                {
                    return false;
                }

                var session = ArchipelagoConnectionManager.Session;
                if (!ArchipelagoConnectionManager.ConnectionSuccessful ||
                    session == null ||
                    session.Items == null ||
                    session.Items.AllItemsReceived == null)
                {
                    return false;
                }

                int owned = session.Items.AllItemsReceived.Count(i => (int)i.ItemId == 15);
                int goal = TryReadModInt("goal", true, 0);
                int interval = Mathf.Clamp(TryReadModInt("dayLeaseInterval", true, 5), 1, 30);
                int overallDaysCompleted = goal == 1 ? TryReadModInt("overallDaysCompleted", true, 0) : 0;
                int timesFranchised = TryReadModInt("timesFranchised", false, 1);

                int required = ComputeRequiredLeaseCount(goal, interval, currentDay, overallDaysCompleted, timesFranchised);

                WarningLevel warning = WarningLevel.Safe;
                var warnQuery = em.CreateEntityQuery(typeof(SStartDayWarnings));
                if (warnQuery.CalculateEntityCount() > 0)
                {
                    warning = warnQuery.GetSingleton<SStartDayWarnings>().SellingRequiredAppliance;
                }

                int daysUntilNext = ComputeDaysUntilNextRequirement(goal, interval, currentDay, overallDaysCompleted, required, timesFranchised);

                status = new LeaseStatus
                {
                    IsPrepPhase = isPrep,
                    CurrentDay = currentDay,
                    LeaseInterval = interval,
                    Owned = owned,
                    Required = required,
                    DaysUntilNextRequirement = daysUntilNext,
                    IsGateActive = warning == WarningLevel.Error || required > owned
                };
                return true;
            }
            catch
            {
                status = default;
                return false;
            }
        }

        private static int TryReadModInt(string fieldName, bool isStatic, int fallback)
        {
            try
            {
                var flags = System.Reflection.BindingFlags.NonPublic |
                            (isStatic ? System.Reflection.BindingFlags.Static : System.Reflection.BindingFlags.Instance);
                var field = typeof(Mod).GetField(fieldName, flags);
                if (field == null)
                {
                    return fallback;
                }

                object target = isStatic ? null : (object)Mod.Instance;
                if (!isStatic && target == null)
                {
                    return fallback;
                }

                if (field.GetValue(target) is int value)
                {
                    return value;
                }
            }
            catch
            {
            }

            return fallback;
        }

        private static int ComputeRequiredLeaseCount(int goal, int interval, int currentDay, int overallDaysCompleted, int timesFranchised)
        {
            if (goal == 0)
            {
                if (currentDay > 15)
                {
                    return 0;
                }

                int segmentsPerFranchise = Mathf.CeilToInt(15f / interval);
                int baseOffset = segmentsPerFranchise * Mathf.Max(0, timesFranchised - 1);
                int withinRun = Mathf.Min(segmentsPerFranchise - 1, (currentDay - 1) / interval);

                if (timesFranchised == 1 && currentDay <= interval)
                {
                    return 0;
                }

                return baseOffset + withinRun;
            }

            int nextOverallDay = Math.Max(1, overallDaysCompleted + 1);
            return (nextOverallDay - 1) / interval;
        }

        private static int ComputeDaysUntilNextRequirement(int goal, int interval, int currentDay, int overallDaysCompleted, int required, int timesFranchised)
        {
            if (goal == 0)
            {
                if (currentDay > 15)
                {
                    return -1;
                }

                int segmentsPerFranchise = Mathf.CeilToInt(15f / interval);
                int withinRun = Mathf.Min(segmentsPerFranchise - 1, (currentDay - 1) / interval);
                int nextSegment = withinRun + 1;

                if (nextSegment >= segmentsPerFranchise)
                {
                    return -1;
                }

                int nextDay = (nextSegment * interval) + 1;
                if (nextDay > 15)
                {
                    return -1;
                }

                return Math.Max(0, nextDay - currentDay);
            }

            int currentOverallDay = Math.Max(1, overallDaysCompleted + 1);
            int nextThreshold = ((required + 1) * interval) + 1;
            if (nextThreshold <= currentOverallDay)
            {
                return 0;
            }

            return nextThreshold - currentOverallDay;
        }

        private Rect GetGlobalFooterRect()
        {
            const float footerWidth = 360f;
            const float lineHeight = 22f;
            const float margin = 14f;
            const float padding = 8f;
            const float headerLines = 3f;
            const float gameStatusBarHeight = 50f;

            int dishCount = Mathf.Max(0, ProgressionMapping.dishDictionary.Count - 1);
            float dishRows = Mathf.Clamp(dishCount, 4f, 16f);
            float idealHeight = (headerLines * (lineHeight + 4f)) + (dishRows * lineHeight) + (padding * 2f) + 10f;

            // Cap to 45% of screen height so it never goes off-screen
            float maxHeight = Screen.height * 0.45f;
            float footerHeight = Mathf.Min(idealHeight, maxHeight);

            float bottomOffset = gameStatusBarHeight + margin;
            float x = Screen.width - footerWidth - margin;
            float y = Screen.height - footerHeight - bottomOffset;
            return new Rect(x, y, footerWidth, footerHeight);
        }

        private bool IsFooterInteracting(Rect footerRect)
        {
            if (!footerRect.Contains(Event.current.mousePosition))
            {
                return false;
            }

            if (Event.current.type == UnityEngine.EventType.ScrollWheel ||
                Event.current.type == UnityEngine.EventType.MouseDrag ||
                Event.current.type == UnityEngine.EventType.MouseDown ||
                Input.GetMouseButton(0) ||
                Input.GetMouseButton(1) ||
                Input.GetMouseButton(2))
            {
                return true;
            }

            return true;
        }

        private bool IsInKitchen()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null)
                    return false;

                return world.EntityManager.CreateEntityQuery(typeof(SKitchenMarker)).CalculateEntityCount() > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}