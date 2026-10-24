// The Editor finds project plugins only through these two attributes, which VRage's source generator
// normally emits. We build without that generator, so they are written by hand.
[assembly: Keen.VRage.Library.Reflection.IndexMetadata]
[module: Keen.VRage.Library.Reflection.ModuleIndexedAttributes(
    typeof(Keen.VRage.Plugin.Editor.ProjectPluginAttribute),
    new[] { typeof(Veronica.VeronicaPlugin) },
    new string[0])]
