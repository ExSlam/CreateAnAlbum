using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Albummodelite
{
    public static class AlbumCoverRenderer
    {
        private static readonly Color[] titleColors =
        {
            Color.white,
            Color.black,
            new Color(0.95f, 0.82f, 0.40f),
            new Color(1.00f, 0.55f, 0.75f),
            new Color(0.70f, 0.45f, 0.95f),
            new Color(0.35f, 0.85f, 0.90f),
            new Color(0.55f, 0.72f, 1.00f)
        };

        public static GameObject Build(
            Transform parent,
            AlbumData album,
            string objectName,
            Vector2 anchoredPosition,
            float size
        )
        {
            if (album == null)
                return null;

            AlbumBackgroundCatalog.EnsureLoaded();
            AlbumFontCatalog.EnsureLoaded();

            GameObject holder = new GameObject(objectName);
            holder.transform.SetParent(parent, false);

            RectTransform holderRect = holder.AddComponent<RectTransform>();
            holderRect.anchorMin = new Vector2(0, 1);
            holderRect.anchorMax = new Vector2(0, 1);
            holderRect.pivot = new Vector2(0, 1);
            holderRect.sizeDelta = new Vector2(size, size);
            holderRect.anchoredPosition = anchoredPosition;

            Image holderImage = holder.AddComponent<Image>();
            holderImage.color = GetThemeFallbackColor(album.ThemeIndex);

            Sprite background = AlbumBackgroundCatalog.Resolve(
                album.BackgroundKey,
                album.BackgroundIndex);
            if (background != null)
            {
                holderImage.sprite = background;
                holderImage.type = Image.Type.Simple;
                holderImage.preserveAspect = false;
                holderImage.color = Color.white;
            }

            Outline outer = holder.AddComponent<Outline>();
            outer.effectColor = new Color(0.72f, 0.70f, 0.80f);
            outer.effectDistance = new Vector2(1.5f, -1.5f);

            holder.AddComponent<RectMask2D>();

            AddThemeOverlay(holder.transform, album);
            DrawMembers(holder.transform, album, size);
            AddBottomFade(holder.transform, album);
            AddFrame(holder.transform, album, size);
            AddTitle(holder.transform, album, size);

            return holder;
        }

        private static void AddTitle(
            Transform parent,
            AlbumData album,
            float size
        )
        {
            Color textColor = titleColors[
                Mathf.Clamp(album.TextColorIndex, 0, titleColors.Length - 1)
            ];

            GameObject titleObj = new GameObject("AlbumTitle");
            titleObj.transform.SetParent(parent, false);

            RectTransform tr = titleObj.AddComponent<RectTransform>();
            ApplyTitlePosition(tr, album.TitlePosition);

            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = AlbumFontCatalog.Resolve(album.FontKey, album.FontIndex);
            titleText.text = string.IsNullOrWhiteSpace(album.Title)
                ? "UNTITLED"
                : album.Title.ToUpperInvariant();
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontSize = GetAutoTitleSize(titleText.text, size);
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = Mathf.Max(9, Mathf.RoundToInt(size * 0.035f));
            titleText.resizeTextMaxSize = titleText.fontSize;
            titleText.color = textColor;
            titleText.fontStyle = FontStyle.Normal;
            titleText.raycastTarget = false;

            ApplyTitleEffect(titleObj, album, textColor);

            // Ornament
            GameObject ornament = new GameObject("Ornament");
            ornament.transform.SetParent(parent, false);

            RectTransform or = ornament.AddComponent<RectTransform>();
            or.anchorMin = new Vector2(0.22f, 0.255f);
            or.anchorMax = new Vector2(0.78f, 0.315f);
            or.offsetMin = Vector2.zero;
            or.offsetMax = Vector2.zero;

            Text ot = ornament.AddComponent<Text>();
            ot.font = AlbumUiResources.GetGameFont();
            ot.text = GetOrnament(album.OrnamentStyle);
            ot.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * 0.03f));
            ot.alignment = TextAnchor.MiddleCenter;
            ot.color = textColor;
            ot.raycastTarget = false;

            if (album.ShowGroupName)
            {
                GameObject subtitle = new GameObject("GroupName");
                subtitle.transform.SetParent(parent, false);

                RectTransform sr = subtitle.AddComponent<RectTransform>();
                sr.anchorMin = new Vector2(0.10f, 0.025f);
                sr.anchorMax = new Vector2(0.90f, 0.085f);
                sr.offsetMin = Vector2.zero;
                sr.offsetMax = Vector2.zero;

                Text st = subtitle.AddComponent<Text>();
                st.font = AlbumFontCatalog.Resolve(album.FontKey, album.FontIndex);
                st.text = string.IsNullOrEmpty(album.GroupName)
                    ? "GROUP"
                    : album.GroupName.ToUpperInvariant();
                st.fontSize = Mathf.Max(7, Mathf.RoundToInt(size * 0.027f));
                st.alignment = TextAnchor.MiddleCenter;
                st.resizeTextForBestFit = true;
                st.resizeTextMinSize = 6;
                st.resizeTextMaxSize = Mathf.Max(8, Mathf.RoundToInt(size * 0.035f));
                st.color = textColor;
                st.raycastTarget = false;
            }
        }

        private static void DrawMembers(
            Transform parent,
            AlbumData album,
            float coverSize
        )
        {
            if (album.Members == null || album.Members.Count == 0)
                return;

            int count = Mathf.Min(album.Members.Count, 8);
            Vector2[] positions = GetLayoutPositions(
                album.LayoutIndex,
                count,
                album.PortraitSpacing
            );

            float positionScale = coverSize / 350f;

            data_girls_textures textureManager =
                Camera.main.GetComponent<mainScript>()
                    .Data.GetComponent<data_girls_textures>();

            List<int> renderOrder = Enumerable.Range(0, count)
                .OrderByDescending(i => positions[i].y)
                .ThenBy(i => Mathf.Abs(positions[i].x - 175f))
                .ToList();

            int centerIndex =
                album.CenterMemberIndex >= 0 &&
                album.CenterMemberIndex < count
                    ? album.CenterMemberIndex
                    : GetAutoCenterIndex(positions);

            foreach (int i in renderOrder)
            {
                data_girls.girls girl = album.Members[i];
                if (girl == null)
                    continue;

                float depth = Mathf.InverseLerp(
                    225f,
                    120f,
                    positions[i].y
                );

                float memberScale =
                    count <= 3 ? 1.00f :
                    count <= 5 ? 0.91f :
                    count <= 6 ? 0.82f :
                    0.73f;

                memberScale *= Mathf.Clamp(album.PortraitScale, 0.70f, 1.40f);

                if ((album.LayoutIndex == 2 || album.LayoutIndex == 3) &&
                    i == centerIndex)
                {
                    memberScale *= Mathf.Clamp(
                        album.CenterEmphasis,
                        1.00f,
                        1.30f
                    );
                }

                Vector2 pos = positions[i];
                pos.y -= 55f;
                pos.y -= depth * 10f;
                pos.y += album.PortraitYOffset;

                GameObject idol = new GameObject("Member_" + i);
                idol.transform.SetParent(parent, false);

                RectTransform r = idol.AddComponent<RectTransform>();
                r.anchorMin = new Vector2(0, 0);
                r.anchorMax = new Vector2(0, 0);
                r.pivot = new Vector2(0.5f, 0f);
                r.sizeDelta = new Vector2(
                    150f * memberScale * positionScale,
                    265f * memberScale * positionScale
                );
                r.anchoredPosition = pos * positionScale;

                Image image = idol.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;

                float alpha = Mathf.Lerp(0.92f, 1.00f, depth);
                float brightness = Mathf.Lerp(0.94f, 1.00f, depth);
                image.color = new Color(
                    brightness,
                    brightness,
                    brightness,
                    alpha
                );

                Shadow shadow = idol.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
                shadow.effectDistance = new Vector2(
                    1.3f * positionScale,
                    -1.3f * positionScale
                );

                textureManager._setFullPortrait(girl, idol);
            }
        }

        private static Vector2[] GetLayoutPositions(
            int layoutIndex,
            int count,
            float spacingScale
        )
        {
            Vector2[] positions = new Vector2[count];

            switch (Mathf.Clamp(layoutIndex, 0, 4))
            {
                case 0:
                    for (int i = 0; i < count; i++)
                    {
                        float x = 175f + ((i - (count - 1) / 2f) * 48f);
                        float y = 175f + Mathf.Abs(i - (count - 1) / 2f) * 8f;
                        positions[i] = new Vector2(x, y);
                    }
                    break;

                case 1:
                    for (int i = 0; i < count; i++)
                    {
                        float spacing = count <= 5 ? 55f : 42f;
                        float x = 175f + ((i - (count - 1) / 2f) * spacing);
                        positions[i] = new Vector2(x, 178f);
                    }
                    break;

                case 2:
                    int backCount = Mathf.CeilToInt(count / 2f);
                    int frontCount = count - backCount;

                    for (int i = 0; i < backCount; i++)
                    {
                        float spacing = backCount <= 1 ? 0f : 72f;
                        float x = 175f + ((i - (backCount - 1) / 2f) * spacing);
                        positions[i] = new Vector2(x, 215f);
                    }

                    for (int i = 0; i < frontCount; i++)
                    {
                        float spacing = frontCount <= 1 ? 0f : 74f;
                        float x = 175f + ((i - (frontCount - 1) / 2f) * spacing);
                        positions[backCount + i] = new Vector2(x, 135f);
                    }
                    break;

                case 3:
                    if (count == 1)
                        positions[0] = new Vector2(175f, 155f);
                    else if (count == 2)
                    {
                        positions[0] = new Vector2(120f, 180f);
                        positions[1] = new Vector2(230f, 180f);
                    }
                    else if (count == 3)
                    {
                        positions[0] = new Vector2(95f, 205f);
                        positions[1] = new Vector2(175f, 145f);
                        positions[2] = new Vector2(255f, 205f);
                    }
                    else if (count == 4)
                    {
                        positions[0] = new Vector2(75f, 210f);
                        positions[1] = new Vector2(135f, 170f);
                        positions[2] = new Vector2(215f, 170f);
                        positions[3] = new Vector2(275f, 210f);
                    }
                    else
                    {
                        positions[0] = new Vector2(62f, 218f);
                        positions[1] = new Vector2(118f, 178f);
                        positions[2] = new Vector2(175f, 135f);
                        positions[3] = new Vector2(232f, 178f);
                        positions[4] = new Vector2(288f, 218f);

                        if (count >= 6)
                            positions[5] = new Vector2(175f, 190f);
                        if (count >= 7)
                            positions[6] = new Vector2(92f, 145f);
                        if (count >= 8)
                            positions[7] = new Vector2(258f, 145f);
                    }
                    break;

                default:
                    for (int i = 0; i < count; i++)
                    {
                        int row = i % 2;
                        int column = i / 2;

                        float xBase = row == 0 ? 105f : 135f;
                        float x = xBase + column * 65f;
                        float y = row == 0 ? 205f : 125f;

                        positions[i] = new Vector2(x, y);
                    }
                    break;
            }

            float spacingMultiplier =
                Mathf.Clamp(spacingScale, 0.70f, 1.35f);

            for (int i = 0; i < positions.Length; i++)
            {
                positions[i].x =
                    175f +
                    ((positions[i].x - 175f) * spacingMultiplier);
            }

            return positions;
        }

        private static int GetAutoCenterIndex(Vector2[] positions)
        {
            int index = 0;
            float best = float.MaxValue;

            for (int i = 0; i < positions.Length; i++)
            {
                float score =
                    Mathf.Abs(positions[i].x - 175f) +
                    positions[i].y * 0.05f;

                if (score < best)
                {
                    best = score;
                    index = i;
                }
            }

            return index;
        }

        private static void AddBottomFade(Transform parent, AlbumData album)
        {
            Color color = GetThemeFallbackColor(album.ThemeIndex);

            GameObject fade = new GameObject("BottomFade");
            fade.transform.SetParent(parent, false);

            RectTransform r = fade.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 0.08f);
            r.anchorMax = new Vector2(1f, 0.38f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            Image img = fade.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite = CreateVerticalFadeSprite(
                color,
                0f,
                Mathf.Clamp01(0.62f * album.EffectsIntensity)
            );
        }

        private static void AddThemeOverlay(Transform parent, AlbumData album)
        {
            GameObject overlay = new GameObject("ThemeOverlay");
            overlay.transform.SetParent(parent, false);

            RectTransform r = overlay.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            Image img = overlay.AddComponent<Image>();
            img.raycastTarget = false;

            switch (album.ThemeIndex)
            {
                case 1:
                    img.color = new Color(0.05f, 0.02f, 0.10f, 0.30f);
                    break;
                case 2:
                    img.color = new Color(0.28f, 0.00f, 0.42f, 0.20f);
                    break;
                case 3:
                    img.color = new Color(0.65f, 0.43f, 0.20f, 0.18f);
                    break;
                case 4:
                    img.color = new Color(1f, 1f, 1f, 0.18f);
                    break;
                default:
                    img.color = new Color(0.55f, 0.45f, 1f, 0.08f);
                    break;
            }
        }

        private static void AddFrame(
            Transform parent,
            AlbumData album,
            float size
        )
        {
            if (album.FrameStyle == 0)
                return;

            Color frameColor;

            switch (album.ThemeIndex)
            {
                case 1:
                    frameColor = new Color(0.82f, 0.82f, 0.88f, 0.72f);
                    break;
                case 2:
                    frameColor = new Color(0.92f, 0.48f, 1f, 0.82f);
                    break;
                case 3:
                    frameColor = new Color(0.88f, 0.72f, 0.42f, 0.78f);
                    break;
                default:
                    frameColor = new Color(1f, 0.78f, 0.96f, 0.72f);
                    break;
            }

            float thickness = Mathf.Max(
                1f,
                size * (album.FrameStyle == 4 ? 0.009f : 0.005f)
            );

            AddFrameLine(parent,
                new Vector2(0.035f, 0.955f),
                new Vector2(0.965f, 0.955f),
                thickness,
                frameColor);

            AddFrameLine(parent,
                new Vector2(0.035f, 0.045f),
                new Vector2(0.965f, 0.045f),
                thickness,
                frameColor);

            AddFrameLine(parent,
                new Vector2(0.035f, 0.045f),
                new Vector2(0.035f, 0.955f),
                thickness,
                frameColor);

            AddFrameLine(parent,
                new Vector2(0.965f, 0.045f),
                new Vector2(0.965f, 0.955f),
                thickness,
                frameColor);
        }

        private static void AddFrameLine(
            Transform parent,
            Vector2 start,
            Vector2 end,
            float thickness,
            Color color
        )
        {
            GameObject line = new GameObject("FrameLine");
            line.transform.SetParent(parent, false);

            RectTransform r = line.AddComponent<RectTransform>();

            if (Mathf.Abs(start.y - end.y) < 0.001f)
            {
                r.anchorMin = start;
                r.anchorMax = end;
                r.sizeDelta = new Vector2(0, thickness);
            }
            else
            {
                r.anchorMin = start;
                r.anchorMax = end;
                r.sizeDelta = new Vector2(thickness, 0);
            }

            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            Image img = line.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = color;
        }

        private static void ApplyTitleEffect(
            GameObject obj,
            AlbumData album,
            Color textColor
        )
        {
            if (album.TitleEffect == 0)
                return;

            if (album.TitleEffect == 1)
            {
                Shadow shadow = obj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
                shadow.effectDistance = new Vector2(1.2f, -1.2f);
            }
            else
            {
                Outline outline = obj.AddComponent<Outline>();
                outline.effectColor = album.TitleEffect == 3
                    ? new Color(textColor.r, textColor.g, textColor.b, 0.65f)
                    : new Color(0f, 0f, 0f, 0.65f);
                outline.effectDistance = album.TitleEffect == 3
                    ? new Vector2(2f, -2f)
                    : new Vector2(1.2f, -1.2f);
            }
        }

        private static void ApplyTitlePosition(
            RectTransform rect,
            int titlePosition
        )
        {
            if (titlePosition == 0)
            {
                rect.anchorMin = new Vector2(0.08f, 0.73f);
                rect.anchorMax = new Vector2(0.92f, 0.92f);
            }
            else if (titlePosition == 1)
            {
                rect.anchorMin = new Vector2(0.08f, 0.40f);
                rect.anchorMax = new Vector2(0.92f, 0.59f);
            }
            else
            {
                rect.anchorMin = new Vector2(0.08f, 0.075f);
                rect.anchorMax = new Vector2(0.92f, 0.255f);
            }

            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static int GetAutoTitleSize(string title, float size)
        {
            int length = string.IsNullOrEmpty(title) ? 0 : title.Length;
            float ratio = 0.12f;

            if (length >= 20)
                ratio = 0.070f;
            else if (length >= 16)
                ratio = 0.080f;
            else if (length >= 12)
                ratio = 0.092f;
            else if (length >= 8)
                ratio = 0.105f;

            return Mathf.RoundToInt(size * ratio);
        }

        private static string GetOrnament(int style)
        {
            if (style == 1)
                return "──  ◆  ◇  ◆  ──";
            if (style == 2)
                return "──  ✦  ★  ✦  ──";

            return "────  ♛  ────";
        }

        private static Sprite CreateVerticalFadeSprite(
            Color color,
            float topAlpha,
            float bottomAlpha
        )
        {
            const int height = 64;

            Texture2D tex = new Texture2D(
                1,
                height,
                TextureFormat.RGBA32,
                false
            );

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                float smooth = t * t * (3f - 2f * t);
                float alpha = Mathf.Lerp(bottomAlpha, topAlpha, smooth);

                tex.SetPixel(
                    0,
                    y,
                    new Color(color.r, color.g, color.b, alpha)
                );
            }

            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f)
            );
        }

        private static Color GetThemeFallbackColor(int theme)
        {
            switch (theme)
            {
                case 1:
                    return new Color(0.08f, 0.06f, 0.13f);
                case 2:
                    return new Color(0.11f, 0.04f, 0.18f);
                case 3:
                    return new Color(0.77f, 0.68f, 0.56f);
                case 4:
                    return new Color(0.93f, 0.93f, 0.93f);
                default:
                    return new Color(0.72f, 0.63f, 0.90f);
            }
        }


    }
}
