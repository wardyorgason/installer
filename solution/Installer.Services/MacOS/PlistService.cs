using System.Globalization;
using System.Security;
using System.Text;
using Installer.Dtos.Manifest;
using Installer.Services.Manifest;

namespace Installer.Services.MacOS;

internal sealed class PlistService(IVersionService versions) : IPlistService
{
    public string InfoPlist(AppInfo app, string executableName, string iconName, PlistDictionary extraKeys)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(extraKeys);
        var entries = new List<KeyValuePair<string, PlistValue>>
        {
            new("CFBundleName", new PlistString(app.Name)),
            new("CFBundleDisplayName", new PlistString(app.Name)),
            new("CFBundleIdentifier", new PlistString(app.Id)),
            new("CFBundleExecutable", new PlistString(executableName)),
            new("CFBundlePackageType", new PlistString("APPL")),
            new("CFBundleShortVersionString", new PlistString(versions.ShortVersion(app.Version))),
            new("CFBundleVersion", new PlistString(versions.FullVersion(app.Version))),
            new("CFBundleInfoDictionaryVersion", new PlistString("6.0")),
            new("CFBundleIconFile", new PlistString(iconName)),
            new("NSHighResolutionCapable", new PlistBoolean(true)),
        };
        foreach (var entry in extraKeys.Entries)
        {
            var index = entries.FindIndex(existing => existing.Key == entry.Key);
            if (index >= 0)
            {
                entries[index] = entry;
            }
            else
            {
                entries.Add(entry);
            }
        }

        return Document(new PlistDictionary(entries));
    }

    public string Entitlements(IReadOnlyList<string> entitlements)
    {
        ArgumentNullException.ThrowIfNull(entitlements);
        return Document(new PlistDictionary(entitlements.Select(key => new KeyValuePair<string, PlistValue>(key, new PlistBoolean(true))).ToList()));
    }

    private static string Document(PlistDictionary root)
    {
        var xml = new StringBuilder();
        xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        xml.Append("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n");
        xml.Append("<plist version=\"1.0\">\n");
        Write(xml, root, 0);
        xml.Append("</plist>\n");
        return xml.ToString();
    }

    private static void Write(StringBuilder xml, PlistValue value, int depth)
    {
        var indent = new string(' ', depth * 2);
        switch (value)
        {
            case PlistString text:
                xml.Append(indent).Append("<string>").Append(SecurityElement.Escape(text.Value)).Append("</string>\n");
                break;
            case PlistBoolean flag:
                xml.Append(indent).Append(flag.Value ? "<true/>" : "<false/>").Append('\n');
                break;
            case PlistInteger number:
                xml.Append(indent).Append("<integer>").Append(number.Value.ToString(CultureInfo.InvariantCulture)).Append("</integer>\n");
                break;
            case PlistArray array:
                xml.Append(indent).Append("<array>\n");
                foreach (var item in array.Items)
                {
                    Write(xml, item, depth + 1);
                }

                xml.Append(indent).Append("</array>\n");
                break;
            case PlistDictionary dictionary:
                xml.Append(indent).Append("<dict>\n");
                foreach (var (key, item) in dictionary.Entries)
                {
                    xml.Append(indent).Append("  <key>").Append(SecurityElement.Escape(key)).Append("</key>\n");
                    Write(xml, item, depth + 1);
                }

                xml.Append(indent).Append("</dict>\n");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown plist value.");
        }
    }
}
