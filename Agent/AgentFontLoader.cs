using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BlueBrick.Agent
{
    internal static class AgentFontLoader
    {
        private static PrivateFontCollection _fontCollection = new PrivateFontCollection();
        private static bool _fontsLoaded = false;

        public static FontFamily SpaceGrotesk { get; private set; }
        public static FontFamily IbmPlexSans { get; private set; }

        public static void Initialize(AgentConfig config)
        {
            if (config?.UI?.Fonts != null)
            {
                LoadFonts(config.UI.Fonts.SpaceGroteskPath, config.UI.Fonts.IbmPlexSansPath);
            }
        }

        public static FontFamily GetFamily(string preferred, string fallback)
        {
            if (preferred == "Space Grotesk" && SpaceGrotesk != null) return SpaceGrotesk;
            if (preferred == "IBM Plex Sans" && IbmPlexSans != null) return IbmPlexSans;

            // Fallback to system search
            var families = FontFamily.Families;
            var match = families.FirstOrDefault(f => f.Name == preferred);
            return match ?? new FontFamily(fallback);
        }

        public static void LoadFonts(string spaceGroteskPath, string ibmPlexSansPath)
        {
            if (_fontsLoaded) return;

            SpaceGrotesk = LoadFont(spaceGroteskPath, "Space Grotesk");
            IbmPlexSans = LoadFont(ibmPlexSansPath, "IBM Plex Sans");

            _fontsLoaded = true;
        }

        private static FontFamily LoadFont(string relativePath, string fallbackName)
        {
            try
            {
                // Get the base path of the executable/addin
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string baseDir = Path.GetDirectoryName(assemblyPath);
                
                // Try relative to assembly
                string fullPath = Path.Combine(baseDir, relativePath);
                
                // If not found, try relative to current directory (for dev/tests)
                if (!File.Exists(fullPath))
                {
                    fullPath = Path.GetFullPath(relativePath);
                }

                if (File.Exists(fullPath))
                {
                    _fontCollection.AddFontFile(fullPath);
                    
                    // The last added font family or find by name
                    foreach (var family in _fontCollection.Families)
                    {
                        if (family.Name.Contains(fallbackName) || fallbackName.Contains(family.Name))
                        {
                            return family;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to system font if loading fails
            }

            return new FontFamily(GetSystemFallback(fallbackName));
        }

        private static string GetSystemFallback(string name)
        {
            return "Segoe UI"; // Default fallback for Windows
        }
    }
}
