#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class IOSLocationPlistPostProcessor
{
    const string WhenInUseUsageKey = "NSLocationWhenInUseUsageDescription";
    const string AlwaysUsageKey = "NSLocationAlwaysAndWhenInUseUsageDescription";
    const string LegacyAlwaysUsageKey = "NSLocationAlwaysUsageDescription";

    const string UsageDescription =
        "This app needs your location to track travel distance while you use it, and in the background or when closed for significant location updates.";

    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget != BuildTarget.iOS)
        {
            return;
        }

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        plist.root.SetString(WhenInUseUsageKey, UsageDescription);
        plist.root.SetString(AlwaysUsageKey, UsageDescription);
        plist.root.SetString(LegacyAlwaysUsageKey, UsageDescription);

        PlistElementArray backgroundModes = plist.root.CreateArray("UIBackgroundModes");
        backgroundModes.AddString("location");

        // ProMotion: allow frame durations shorter than 1/60s (e.g. 120 Hz on iPhone 15 Pro).
        plist.root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);

        plist.WriteToFile(plistPath);
        Debug.Log("IOSLocationPlistPostProcessor: set location usage keys and UIBackgroundModes=location in Info.plist");
    }
}
#endif
