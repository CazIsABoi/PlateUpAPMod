using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using System.Collections.Generic;
using UnityEngine;
using UnityColor = UnityEngine.Color;

namespace KitchenPlateupAP
{
    /// <summary>
    /// Shows short Archipelago item-transfer notifications. Message-log callbacks
    /// can run off Unity's thread, so popups enter a queue before display.
    /// </summary>
    public class CheckPopupManager : MonoBehaviour
    {
        private enum TransferDirection
        {
            Sent,
            Received
        }

        private struct PendingPopup
        {
            public TransferDirection Direction;
            public string ItemName;
            public string PlayerName;
            public string DisplayText;
        }

        private class ActivePopup
        {
            public PendingPopup Data;
            public float CreatedAt;
        }

        private const int MaxVisiblePopups = 5;
        private const float DisplayDuration = 6f;
        private const float FadeDuration = 1f;
        private const float PopupWidth = 420f;
        private const float PopupHeight = 58f;
        private const float PopupGap = 8f;
        private const float ScreenMargin = 18f;

        private static readonly object PendingLock = new object();
        private static readonly Queue<PendingPopup> PendingPopups = new Queue<PendingPopup>();

        private readonly List<ActivePopup> activePopups = new List<ActivePopup>();
        private GUIStyle messageStyle;
        private Texture2D backgroundTexture;
        private Texture2D accentTexture;
        private Texture2D archipelagoIcon;

        public static void AddFromMessage(LogMessage message)
        {
            if (!(message is ItemSendLogMessage transfer) || transfer.Item == null)
                return;

            if (transfer.IsSenderTheActivePlayer && !transfer.IsReceiverTheActivePlayer)
            {
                Enqueue(
                    TransferDirection.Sent,
                    GetItemName(transfer.Item),
                    GetPlayerName(transfer.Receiver));
            }
            else if (transfer.IsReceiverTheActivePlayer)
            {
                Enqueue(
                    TransferDirection.Received,
                    GetItemName(transfer.Item),
                    GetPlayerName(transfer.Sender));
            }
        }

        public static void AddGoalDayCompleted(string dishName, int dayTarget)
        {
            lock (PendingLock)
            {
                PendingPopups.Enqueue(new PendingPopup
                {
                    Direction = TransferDirection.Sent,
                    DisplayText = $"Completed <b>{EscapeRichText(dishName)}</b> — Day <b>{dayTarget}</b> goal reached"
                });
            }
        }

        private static string GetItemName(ItemInfo item)
        {
            if (!string.IsNullOrWhiteSpace(item.ItemDisplayName)) return item.ItemDisplayName;
            if (!string.IsNullOrWhiteSpace(item.ItemName)) return item.ItemName;
            return "Unknown item";
        }

        private static string GetPlayerName(PlayerInfo player)
        {
            if (player == null) return "Unknown player";
            if (!string.IsNullOrWhiteSpace(player.Alias)) return player.Alias;
            if (!string.IsNullOrWhiteSpace(player.Name)) return player.Name;
            return "Unknown player";
        }

        private static void Enqueue(TransferDirection direction, string itemName, string playerName)
        {
            lock (PendingLock)
            {
                PendingPopups.Enqueue(new PendingPopup
                {
                    Direction = direction,
                    ItemName = itemName,
                    PlayerName = playerName
                });
            }
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;

            lock (PendingLock)
            {
                while (PendingPopups.Count > 0)
                {
                    if (activePopups.Count >= MaxVisiblePopups)
                        activePopups.RemoveAt(0);

                    activePopups.Add(new ActivePopup
                    {
                        Data = PendingPopups.Dequeue(),
                        CreatedAt = now
                    });
                }
            }

            activePopups.RemoveAll(popup => now - popup.CreatedAt >= DisplayDuration);
        }

        private void OnGUI()
        {
            if (activePopups.Count == 0) return;
            EnsureStyles();

            float width = Mathf.Min(PopupWidth, Screen.width - ScreenMargin * 2f);
            float x = Screen.width - width - ScreenMargin;
            float now = Time.realtimeSinceStartup;

            for (int i = 0; i < activePopups.Count; i++)
            {
                ActivePopup popup = activePopups[i];
                float age = now - popup.CreatedAt;
                float fade = age > DisplayDuration - FadeDuration
                    ? Mathf.Clamp01((DisplayDuration - age) / FadeDuration)
                    : 1f;
                float slide = Mathf.SmoothStep(22f, 0f, Mathf.Clamp01(age / 0.25f));
                float y = ScreenMargin + i * (PopupHeight + PopupGap) - slide;
                Rect popupRect = new Rect(x, y, width, PopupHeight);

                GUI.color = new UnityColor(0f, 0f, 0f, 0.82f * fade);
                GUI.DrawTexture(popupRect, backgroundTexture);

                UnityColor accent = popup.Data.Direction == TransferDirection.Sent
                    ? new UnityColor(0.25f, 0.65f, 1f, fade)
                    : new UnityColor(0.3f, 0.9f, 0.45f, fade);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(popupRect.x, popupRect.y, 5f, popupRect.height), accentTexture);

                if (archipelagoIcon != null)
                {
                    GUI.color = new UnityColor(1f, 1f, 1f, fade);
                    GUI.DrawTexture(
                        new Rect(popupRect.x + 14f, popupRect.y + 10f, 38f, 38f),
                        archipelagoIcon,
                        ScaleMode.ScaleToFit,
                        true);
                }

                string text = popup.Data.DisplayText;
                if (string.IsNullOrEmpty(text))
                {
                    string verb = popup.Data.Direction == TransferDirection.Sent ? "Sent" : "Received";
                    string preposition = popup.Data.Direction == TransferDirection.Sent ? "to" : "from";
                    text = $"{verb} <b>{EscapeRichText(popup.Data.ItemName)}</b> {preposition} <b>{EscapeRichText(popup.Data.PlayerName)}</b>";
                }

                messageStyle.normal.textColor = new UnityColor(1f, 1f, 1f, fade);
                float textX = archipelagoIcon != null ? popupRect.x + 63f : popupRect.x + 17f;
                GUI.Label(new Rect(textX, popupRect.y + 6f, popupRect.xMax - textX - 12f, popupRect.height - 12f), text, messageStyle);
            }

            GUI.color = UnityColor.white;
        }

        private void EnsureStyles()
        {
            if (messageStyle == null)
            {
                messageStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 16,
                    richText = true,
                    wordWrap = true
                };
            }

            if (backgroundTexture == null)
            {
                backgroundTexture = new Texture2D(1, 1);
                backgroundTexture.SetPixel(0, 0, UnityColor.white);
                backgroundTexture.Apply();
            }

            if (accentTexture == null)
            {
                accentTexture = new Texture2D(1, 1);
                accentTexture.SetPixel(0, 0, UnityColor.white);
                accentTexture.Apply();
            }

            if (archipelagoIcon == null && Mod.Bundle != null)
            {
                archipelagoIcon = Mod.Bundle.LoadAsset<Texture2D>("Archipelago");
                if (archipelagoIcon == null)
                {
                    Sprite iconSprite = Mod.Bundle.LoadAsset<Sprite>("Archipelago");
                    archipelagoIcon = iconSprite?.texture;
                }

                if (archipelagoIcon == null)
                {
                    GameObject pedestalPrefab = Mod.Bundle.LoadAsset<GameObject>("ArchipelagoPedestal");
                    if (pedestalPrefab != null)
                    {
                        Renderer[] renderers = pedestalPrefab.GetComponentsInChildren<Renderer>(true);
                        foreach (Renderer renderer in renderers)
                        {
                            Texture2D texture = renderer.sharedMaterial?.mainTexture as Texture2D;
                            if (texture == null) continue;

                            archipelagoIcon = texture;
                            break;
                        }
                    }
                }
            }
        }

        private static string EscapeRichText(string text)
        {
            return (text ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private void OnDestroy()
        {
            if (backgroundTexture != null) Destroy(backgroundTexture);
            if (accentTexture != null) Destroy(accentTexture);
        }
    }
}
