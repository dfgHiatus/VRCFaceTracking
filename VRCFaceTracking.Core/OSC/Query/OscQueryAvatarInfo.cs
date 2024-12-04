using VRCFaceTracking.Core.Contracts;

namespace VRCFaceTracking.Core.OSC.Query;

public class OscQueryAvatarInfo : IAvatarInfo
{
    public string Name { get; internal set; }

    public string Id { get; }

    public IParameterDefinition[] Parameters { get; }
    
    public OscQueryAvatarInfo(OscQueryNode rootNode)
    {
        Name = "Half-baked OSCQuery impl";
        if (!rootNode.Contents.TryGetValue("change", out var value))
        {
            // We likely queried while an avatar was still loading. Return without parsing.
            return;
        }
        Id = (string)value.Value[0];
        
        //TODO: Figure out a way to reconstruct the traditional address pattern instead of the whole thing.
        IEnumerable<IParameterDefinition> ConstructParameterArray(Dictionary<string, OscQueryNode> entries)
        {
            return entries
                .SelectMany(entry =>
                    entry.Value.Contents != null ? ConstructParameterArray(entry.Value.Contents) : !string.IsNullOrEmpty(entry.Value.OscType) ? new[] { new OscQueryParameterDef(entry.Value.FullPath, entry.Value) } : Array.Empty<OscQueryParameterDef>()
                );
        }
        
        Parameters = rootNode.Contents["parameters"].Contents
            .SelectMany(entry => 
                entry.Value.Contents != null ? ConstructParameterArray(entry.Value.Contents) : !string.IsNullOrEmpty(entry.Value.OscType) ? new[] { new OscQueryParameterDef(entry.Value.FullPath, entry.Value) } : Array.Empty<OscQueryParameterDef>()
            )
            .ToArray();
    }
}