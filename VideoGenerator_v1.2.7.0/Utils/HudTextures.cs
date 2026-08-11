using SixLabors.ImageSharp;
using Color = SixLabors.ImageSharp.Color;

namespace VideoGenerator.Utils
{
    /// <summary>
    /// Centraliza la configuración visual y las "texturas" lógicas del HUD.
    /// Sincronizado con la versión Python v1.2.0.0.
    /// </summary>
    public static class HudTextures
    {
        // Colores base del HUD
        public static readonly Color RibbonBackgroundColor = Color.FromRgba(0, 0, 0, 150);
        public static readonly Color NeonGreen = Color.ParseHex("#00E676"); // Mantenido por compatibilidad si se usa
        public static readonly Color TextWhite = Color.White;

        // Dimensiones y métricas (Arquitectura HUD Python)
        public const int CanvasWidth = 1920;
        public const int CanvasHeight = 1080;
        
        public const int RibbonHeight = 120;
        public const int TextAnchorY = 958;
        public const int RibbonY = TextAnchorY - (RibbonHeight / 2); // 898
        
        // Iconos
        public const int IconSize = 180;
        public const int IconX = 50;
        public const int IconMarginBottom = 20;
        public const int IconY = RibbonY - IconSize - IconMarginBottom; // 698

        // Bordes
        public const float TechnicalBorderWidth = 2f;
        public const float NeonBorderWidth = 1f;

        // Tipografía
        public const float DefaultFontSize = 52f;
        public const float TextVerticalOffsetCorrection = 0f; // Python usa anclaje MM
    }
}
